using ASC.Models.BaseTypes;
using ASC.Utilities;
using ASC.Web.Areas.Accounts.Models;
using ASC.Web.Models;
using ASC.Web.Services;
using ElCamino.AspNetCore.Identity.AzureTable.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ASC.Web.Areas.Accounts.Controllers
{
    [Area("Accounts")]
    [Authorize]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender)
        {
            this._userManager = userManager;
            this._signInManager = signInManager;
            this._emailSender = emailSender;
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ServiceEngineers()
        {
            IList<ApplicationUser> serviceEngineers = await this._userManager
                .GetUsersInRoleAsync(Roles.Engineer.ToString());

            // Hold all service engineers in session
            HttpContext.Session.SetSession("ServiceEngineers", serviceEngineers);

            return View(new ServiceEngineerViewModel
            {
                ServiceEngineers = serviceEngineers != null ? serviceEngineers.ToList() : null,
                Registration = new ServiceEngineerRegistrationViewModel() { IsEdit = false },
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ServiceEngineers(ServiceEngineerViewModel serviceEngineer)
        {
            serviceEngineer.ServiceEngineers = HttpContext.Session
                .GetSession<List<ApplicationUser>>("ServiceEngineers");

            if (!ModelState.IsValid)
                return View(serviceEngineer);

            if (serviceEngineer.Registration.IsEdit)
            {
                // Update user
                ApplicationUser user = await this._userManager
                    .FindByEmailAsync(serviceEngineer.Registration.Email);
                user.UserName = serviceEngineer.Registration.UserName;
                IdentityResult result = await this._userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    result.Errors.ToList().ForEach(p => ModelState.AddModelError("", p.Description));
                    return View(serviceEngineer);
                }

                // Update Password
                string token = await this._userManager.GeneratePasswordResetTokenAsync(user);
                IdentityResult passwordResult = await this._userManager
                    .ResetPasswordAsync(user, token, serviceEngineer.Registration.Password);
                if (!passwordResult.Succeeded)
                {
                    passwordResult.Errors.ToList().ForEach(p => ModelState.AddModelError("", p.Description));
                    return View(serviceEngineer);
                }

                // Update claims
                // user = await _userManager.FindByEmailAsync(serviceEngineer.Registration.Email);
                IdentityUserClaim isActiveClaim = user.Claims.SingleOrDefault(p => p.ClaimType == "IsActive");
                IdentityResult removeClaimResult = await this._userManager.RemoveClaimAsync(user,
                    new Claim(isActiveClaim.ClaimType, isActiveClaim.ClaimValue));
                IdentityResult addClaimResult = await this._userManager.AddClaimAsync(user,
                    new Claim(isActiveClaim.ClaimType, serviceEngineer.Registration.IsActive.ToString()));
            }
            else
            {
                // Create User
                ApplicationUser user = new ApplicationUser
                {
                    UserName = serviceEngineer.Registration.UserName,
                    Email = serviceEngineer.Registration.Email,
                    EmailConfirmed = true,
                };

                IdentityResult result = await this._userManager
                    .CreateAsync(user, serviceEngineer.Registration.Password);
                await this._userManager.AddClaimAsync(user,
                    new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
                    serviceEngineer.Registration.Email));
                await this._userManager.AddClaimAsync(user,
                    new Claim("IsActive", serviceEngineer.Registration.IsActive.ToString()));
                if (!result.Succeeded)
                {
                    result.Errors.ToList().ForEach(p => ModelState.AddModelError("", p.Description));
                    return View(serviceEngineer);
                }

                // Assign user to Engineer Role
                IdentityResult roleResult = await this._userManager.AddToRoleAsync(user, Roles.Engineer.ToString());
                if (!roleResult.Succeeded)
                {
                    roleResult.Errors.ToList().ForEach(p => ModelState.AddModelError("", p.Description));
                    return View(serviceEngineer);
                }
            }

            if (serviceEngineer.Registration.IsActive)
            {
                await this._emailSender.SendEmailAsync(serviceEngineer.Registration.Email,
                    "Account Created/Modified",
                    $"Email: {serviceEngineer.Registration.Email}\nPassword: {serviceEngineer.Registration.Password}");
            }
            else
            {
                await this._emailSender.SendEmailAsync(serviceEngineer.Registration.Email,
                    "Account Deactivated", "Your account has been deactivated.");
            }

            return RedirectToAction("ServiceEngineers");
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Customers()
        {
            IList<ApplicationUser> users = await this._userManager.GetUsersInRoleAsync(Roles.User.ToString());

            // Hold all service engineers in session
            HttpContext.Session.SetSession("Customers", users);

            return View(new CustomersViewModel
            {
                Customers = users != null ? users.ToList() : null,
                Registration = new CustomerRegistrationViewModel(),
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Customers(CustomersViewModel customer)
        {
            customer.Customers = HttpContext.Session.GetSession<List<ApplicationUser>>("Customers");
            if (!ModelState.IsValid)
                return View(customer);

            ApplicationUser user = await this._userManager.FindByEmailAsync(customer.Registration.Email);

            // Update claims
            // user = await _userManager.FindByEmailAsync(customer.Registration.Email);
            IdentityUserClaim isActiveClaim = user.Claims.SingleOrDefault(p => p.ClaimType == "IsActive");
            IdentityResult removeClaimResult = await this._userManager.RemoveClaimAsync(user,
                new Claim(isActiveClaim.ClaimType, isActiveClaim.ClaimValue));
            IdentityResult addClaimResult = await this._userManager.AddClaimAsync(user,
                new Claim(isActiveClaim.ClaimType, customer.Registration.IsActive.ToString()));

            if (!customer.Registration.IsActive)
                await this._emailSender.SendEmailAsync(customer.Registration.Email, "Account Deactivated",
                    "Your account has been deactivated.");

            return RedirectToAction("Customers");
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            ApplicationUser user = await this._userManager
                .FindByEmailAsync(HttpContext.User.GetCurrentUserDetails().Email);
            return View(new ProfileViewModel
            {
                Username = user.UserName,
                IsEditSuccess = false,
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel profile)
        {
            ApplicationUser user = await this._userManager
                .FindByEmailAsync(HttpContext.User.GetCurrentUserDetails().Email);
            user.UserName = profile.Username;
            IdentityResult result = await this._userManager.UpdateAsync(user);
            await this._signInManager.SignOutAsync();
            await this._signInManager.SignInAsync(user, false);

            profile.IsEditSuccess = result.Succeeded;
            AddErrors(result);

            if (ModelState.ErrorCount > 0)
                return View(profile);
            return RedirectToAction("Profile");
        }

        #region Helpers
        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
        #endregion
    }
}
