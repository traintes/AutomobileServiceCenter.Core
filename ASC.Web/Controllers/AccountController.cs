﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ASC.Web.Models;
using ASC.Web.Models.AccountViewModels;
using ASC.Web.Services;
using ASC.Utilities;
using ASC.Models.BaseTypes;
using ElCamino.AspNetCore.Identity.AzureTable.Model;
using Microsoft.AspNetCore.Http.Authentication;

namespace ASC.Web.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ISmsSender _smsSender;
        private readonly string _externalCookieScheme;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IOptions<IdentityCookieOptions> identityCookieOptions,
            IEmailSender emailSender,
            ISmsSender smsSender,
            ILogger<AccountController> logger)
        {
            this._userManager = userManager;
            this._signInManager = signInManager;
            this._externalCookieScheme = identityCookieOptions.Value.ExternalCookieAuthenticationScheme;
            this._emailSender = emailSender;
            this._smsSender = smsSender;
            this._logger = logger;
        }

        //
        // GET: /Account/Login
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string returnUrl = null)
        {
            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.Authentication.SignOutAsync(_externalCookieScheme);

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        //
        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                // This doesn't count login failures toward account lockout
                // To enable password failures to trigger account lockout, set lockoutOnFailure: true
                ApplicationUser user = await this._userManager.FindByEmailAsync(model.Email);
                if(user == null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return View(model);
                }

                bool isActive = Boolean.Parse(user.Claims
                    .SingleOrDefault(p => p.ClaimType == "IsActive").ClaimValue);
                if (!isActive)
                {
                    ModelState.AddModelError(string.Empty, "Account has been locked.");
                    return View(model);
                }

                Microsoft.AspNetCore.Identity.SignInResult result = await this._signInManager
                    .PasswordSignInAsync(user.UserName, model.Password, model.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    this._logger.LogInformation(1, "User logged in.");
                    if (!string.IsNullOrWhiteSpace(returnUrl))
                        return RedirectToLocal(returnUrl);
                    else
                        return RedirectToAction("Dashboard", "Dashboard", new { Area = "ServiceRequests" });
                }
                if (result.RequiresTwoFactor)
                {
                    return RedirectToAction(nameof(SendCode), new
                    {
                        ReturnUrl = returnUrl,
                        RememberMe = model.RememberMe,
                    });
                }
                if (result.IsLockedOut)
                {
                    this._logger.LogWarning(2, "User account locked out.");
                    return View("Lockout");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return View(model);
                }
            }
            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // GET: /Account/Register
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        //
        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    // For more information on how to enable account confirmation and password reset please visit https://go.microsoft.com/fwlink/?LinkID=532713
                    // Send an email with this link
                    //var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    //var callbackUrl = Url.Action(nameof(ConfirmEmail), "Account", new { userId = user.Id, code = code }, protocol: HttpContext.Request.Scheme);
                    //await _emailSender.SendEmailAsync(model.Email, "Confirm your account",
                    //    $"Please confirm your account by clicking this link: <a href='{callbackUrl}'>link</a>");
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    _logger.LogInformation(3, "User created a new account with password.");
                    return RedirectToLocal(returnUrl);
                }
                AddErrors(result);
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await this._signInManager.SignOutAsync();
            this._logger.LogInformation(4, "User logged out.");
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }

        //
        // POST: /Account/ExternalLogin
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string returnUrl = null)
        {
            // Request a redirect to the external login provider.
            string redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account",
                new { ReturnUrl = returnUrl });
            AuthenticationProperties properties = this._signInManager
                .ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }

        //
        // GET: /Account/ExternalLoginCallback
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallback(string returnUrl = null, string remoteError = null)
        {
            // TODO: This method is quite different in the sample. Maybe this solves the issues!
            if (remoteError != null)
            {
                ModelState.AddModelError(string.Empty, $"Error from external provider: {remoteError}");
                return View(nameof(Login));
            }

            ExternalLoginInfo info = await this._signInManager.GetExternalLoginInfoAsync();
            if (info == null)
                return RedirectToAction(nameof(Login));

            // Sign in the user with this external login provider if the user already has a login.
            Microsoft.AspNetCore.Identity.SignInResult result = await this._signInManager
                .ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false);
            if (result.Succeeded)
            {
                this._logger.LogInformation(5, "User logged in with {Name} provider.", info.LoginProvider);
                return RedirectToAction("Dashboard", "Dashboard", new { Area = "ServiceRequests" });
            }
            if (result.RequiresTwoFactor)
                return RedirectToAction(nameof(SendCode), new { ReturnUrl = returnUrl });
            if (result.IsLockedOut)
                return View("Lockout");
            else
            {
                // If the user does not have an account, then ask the user to create an account.
                ViewData["ReturnUrl"] = returnUrl;
                ViewData["LoginProvider"] = info.LoginProvider;
                string email = info.Principal.FindFirstValue(ClaimTypes.Email);
                return View("ExternalLoginConfirmation",
                    new ExternalLoginConfirmationViewModel { Email = email });
            }
        }

        //
        // POST: /Account/ExternalLoginConfirmation
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExternalLoginConfirmation(ExternalLoginConfirmationViewModel model,
            string returnUrl = null)
        {
            if (ModelState.IsValid)
            {
                // Get the information about the user from the external login provider
                ExternalLoginInfo info = await this._signInManager.GetExternalLoginInfoAsync();
                if (info == null)
                    return View("ExternalLoginFailure");

                ApplicationUser user = new ApplicationUser {
                    UserName = model.Email,
                    Email = model.Email,
                    EmailConfirmed = true,
                };

                IdentityResult result = await this._userManager.CreateAsync(user);
                await this._userManager.AddClaimAsync(user,
                    new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", user.Email));
                await this._userManager.AddClaimAsync(user, new Claim("IsActive", "True"));
                if (!result.Succeeded)
                {
                    result.Errors.ToList().ForEach(p => ModelState.AddModelError("", p.Description));
                    return View("ExternalLoginConfirmation", model);
                }

                // Assign user to Engineer Role
                IdentityResult roleResult = await this._userManager.AddToRoleAsync(user, Roles.User.ToString());
                if (!roleResult.Succeeded)
                {
                    roleResult.Errors.ToList().ForEach(p => ModelState.AddModelError("", p.Description));
                    return View("ExternalLoginConfirmation", model);
                }

                if (result.Succeeded)
                {
                    result = await this._userManager.AddLoginAsync(user, info);
                    if (result.Succeeded)
                    {
                        await this._signInManager.SignInAsync(user, isPersistent: false);
                        this._logger.LogInformation(6, "User created an account using {Name} provider.",
                            info.LoginProvider);
                        return RedirectToAction("Dashboard", "Dashboard", new { Area = "ServiceRequests" });
                    }
                }
                AddErrors(result);
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        // GET: /Account/ConfirmEmail
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail(string userId, string code)
        {
            if (userId == null || code == null)
            {
                return View("Error");
            }
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return View("Error");
            }
            var result = await _userManager.ConfirmEmailAsync(user, code);
            return View(result.Succeeded ? "ConfirmEmail" : "Error");
        }

        //
        // GET: /Account/ForgotPassword
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        //
        // POST: /Account/ForgotPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                ApplicationUser user = await this._userManager.FindByEmailAsync(model.Email);
                if (user == null || !(await this._userManager.IsEmailConfirmedAsync(user)))
                {
                    // Don't reveal that the user does not exist or is not confirmed
                    return View("ResetPasswordEmailConfirmation");
                }

                // Send an email with this link
                string code = await this._userManager.GeneratePasswordResetTokenAsync(user);
                string callbackUrl = Url.Action(nameof(ResetPassword), "Account", new
                {
                    userId = user.Id,
                    code = code,
                }, protocol: HttpContext.Request.Scheme);
                await this._emailSender.SendEmailAsync(model.Email, "Reset Password",
                    $"Please reset your password by clicking here: <a href='{callbackUrl}'>link</a>.");
                return View("ResetPasswordEmailConfirmation");
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // GET: /Account/ForgotPasswordConfirmation
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        //
        // POST: /Account/ResetPassword
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> InitiateResetPassword()
        {
            // Find User
            string userEmail = HttpContext.User.GetCurrentUserDetails().Email;
            ApplicationUser user = await this._userManager.FindByEmailAsync(userEmail);

            // Generate User code
            string code = await this._userManager.GeneratePasswordResetTokenAsync(user);
            string callbackUrl = Url.Action(nameof(ResetPassword), "Account", new {
                userId = user.Id,
                code = code,
            }, protocol: HttpContext.Request.Scheme);

            // Send Email
            await this._emailSender.SendEmailAsync(userEmail, "Reset Password",
               $"Please reset your password by clicking here: <a href='{callbackUrl}'>link</a>");
            return View("ResetPasswordEmailConfirmation");
        }

        //
        // GET: /Account/ResetPassword
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string code = null)
        {
            return code == null ? View("Error") : View();
        }

        //
        // POST: /Account/ResetPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            ApplicationUser user = await this._userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return RedirectToAction(nameof(AccountController.ResetPasswordConfirmation), "Account");
            }

            IdentityResult result = await this._userManager.ResetPasswordAsync(user, model.Code, model.Password);
            if (result.Succeeded)
            {
                if (HttpContext.User.Identity.IsAuthenticated)
                    await this._signInManager.SignOutAsync();

                return RedirectToAction(nameof(AccountController.ResetPasswordConfirmation), "Account");
            }

            AddErrors(result);
            return View();
        }

        //
        // GET: /Account/ResetPasswordConfirmation
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        //
        // GET: /Account/SendCode
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult> SendCode(string returnUrl = null, bool rememberMe = false)
        {
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return View("Error");
            }
            var userFactors = await _userManager.GetValidTwoFactorProvidersAsync(user);
            var factorOptions = userFactors.Select(purpose => new SelectListItem { Text = purpose, Value = purpose }).ToList();
            return View(new SendCodeViewModel { Providers = factorOptions, ReturnUrl = returnUrl, RememberMe = rememberMe });
        }

        //
        // POST: /Account/SendCode
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendCode(SendCodeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View();
            }

            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return View("Error");
            }

            // Generate the token and send it
            var code = await _userManager.GenerateTwoFactorTokenAsync(user, model.SelectedProvider);
            if (string.IsNullOrWhiteSpace(code))
            {
                return View("Error");
            }

            var message = "Your security code is: " + code;
            if (model.SelectedProvider == "Email")
            {
                await _emailSender.SendEmailAsync(await _userManager.GetEmailAsync(user), "Security Code", message);
            }
            else if (model.SelectedProvider == "Phone")
            {
                await _smsSender.SendSmsAsync(await _userManager.GetPhoneNumberAsync(user), message);
            }

            return RedirectToAction(nameof(VerifyCode), new { Provider = model.SelectedProvider, ReturnUrl = model.ReturnUrl, RememberMe = model.RememberMe });
        }

        //
        // GET: /Account/VerifyCode
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyCode(string provider, bool rememberMe, string returnUrl = null)
        {
            // Require that the user has already logged in via username/password or external login
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return View("Error");
            }
            return View(new VerifyCodeViewModel { Provider = provider, ReturnUrl = returnUrl, RememberMe = rememberMe });
        }

        //
        // POST: /Account/VerifyCode
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyCode(VerifyCodeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // The following code protects for brute force attacks against the two factor codes.
            // If a user enters incorrect codes for a specified amount of time then the user account
            // will be locked out for a specified amount of time.
            var result = await _signInManager.TwoFactorSignInAsync(model.Provider, model.Code, model.RememberMe, model.RememberBrowser);
            if (result.Succeeded)
            {
                return RedirectToLocal(model.ReturnUrl);
            }
            if (result.IsLockedOut)
            {
                _logger.LogWarning(7, "User account locked out.");
                return View("Lockout");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Invalid code.");
                return View(model);
            }
        }

        //
        // GET: /Account/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        #region Helpers
        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction(nameof(HomeController.Index), "Home");
            }
        }

        #endregion
    }
}
