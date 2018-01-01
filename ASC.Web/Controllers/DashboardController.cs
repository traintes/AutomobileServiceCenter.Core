using ASC.Web.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ASC.Web.Controllers
{
    public class DashboardController : BaseController
    {
        private IOptions<ApplicationSettings> _settings;

        public DashboardController(IOptions<ApplicationSettings> settings)
        {
            this._settings = settings;
        }

        public IActionResult Dashboard()
        {
            return View();
        }
    }
}
