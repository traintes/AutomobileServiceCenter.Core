using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ASC.Utilities;
using ASC.Web.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ASC.Web.Controllers
{
    public class HomeController : AnonymousController
    {
        private IOptions<ApplicationSettings> _settings;

        public HomeController(IOptions<ApplicationSettings> settings)
        {
            this._settings = settings;
        }

        public IActionResult Index()
        {
            // Set Session
            HttpContext.Session.SetSession("Test", this._settings.Value);
            // Get Session
            var settings = HttpContext.Session.GetSession<ApplicationSettings>("Test");

            // Usage of IOptions
            ViewBag.Title = this._settings.Value.ApplicationTitle;

            return View();
        }

        [HttpPost]
        public IActionResult SetCulture(string culture, string returnUrl)
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTime.UtcNow.AddYears(1) }
            );

            return LocalRedirect(returnUrl);
        }

        //public IActionResult About()
        //{
        //    ViewData["Message"] = "Your application description page.";

        //    return View();
        //}

        //public IActionResult Contact()
        //{
        //    ViewData["Message"] = "Your contact page.";

        //    return View();
        //}

        public IActionResult Error(string id)
        {
            if (id == "404")
                return View("NotFound");

            if (id == "401" && User.Identity.IsAuthenticated)
                return View("AccessDenied");
            else
                return RedirectToAction("Login", "Account");

            //return View();
        }
    }
}
