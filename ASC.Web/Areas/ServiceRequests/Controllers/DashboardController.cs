using ASC.Business.Interfaces;
using ASC.Models.BaseTypes;
using ASC.Models.Models;
using ASC.Utilities;
using ASC.Web.Areas.ServiceRequests.Models;
using ASC.Web.Controllers;
using ASC.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ASC.Web.Areas.ServiceRequests.Controllers
{
    [Area("ServiceRequests")]
    public class DashboardController : BaseController
    {
        private readonly IServiceRequestOperations _serviceRequestOperations;
        private readonly IMasterDataCacheOperations _masterData;

        public DashboardController(IServiceRequestOperations operations,
            IMasterDataCacheOperations masterData)
        {
            this._serviceRequestOperations = operations;
            this._masterData = masterData;
        }

        public async Task<IActionResult> Dashboard()
        {
            // List of Status which were to be queried
            List<string> status = new List<string>
            {
                Status.New.ToString(),
                Status.InProgress.ToString(),
                Status.Initiated.ToString(),
                Status.RequestForInformation.ToString(),
            };

            List<ServiceRequest> serviceRequests = new List<ServiceRequest>();
            List<ServiceRequest> auditServiceRequests = new List<ServiceRequest>();
            Dictionary<string, int> activeServiceRequests = new Dictionary<string, int>();

            if (HttpContext.User.IsInRole(Roles.Admin.ToString()))
            {
                serviceRequests = await this._serviceRequestOperations
                    .GetServiceRequestsByRequestedDateAndStatus(DateTime.UtcNow.AddDays(-7), status);
                auditServiceRequests = await this._serviceRequestOperations.GetServiceRequestsFromAudit();

                List<ServiceRequest> serviceEngineerServiceRequests = await this._serviceRequestOperations
                    .GetActiveServiceRequests(new List<string>
                    {
                        Status.InProgress.ToString(),
                        Status.Initiated.ToString(),
                    });
                if (serviceEngineerServiceRequests.Any())
                {
                    activeServiceRequests = serviceEngineerServiceRequests
                        .GroupBy(x => x.ServiceEngineer)
                        .ToDictionary(p => p.Key, p => p.Count());
                }
            }
            else if (HttpContext.User.IsInRole(Roles.Engineer.ToString()))
            {
                serviceRequests = await this._serviceRequestOperations
                    .GetServiceRequestsByRequestedDateAndStatus(DateTime.UtcNow.AddDays(-7), status,
                    serviceEngineerEmail: HttpContext.User.GetCurrentUserDetails().Email);
                auditServiceRequests = await this._serviceRequestOperations
                    .GetServiceRequestsFromAudit(HttpContext.User.GetCurrentUserDetails().Email);
            }
            else
                serviceRequests = await this._serviceRequestOperations
                    .GetServiceRequestsByRequestedDateAndStatus(DateTime.UtcNow.AddYears(-1),
                    email: HttpContext.User.GetCurrentUserDetails().Email);

            return View(new DashboardViewModel
            {
                ServiceRequests = serviceRequests.OrderByDescending(p => p.RequestedDate).ToList(),
                AuditServiceRequests = auditServiceRequests.OrderByDescending(p => p.Timestamp).ToList(),
                ActiveServiceRequests = activeServiceRequests,
            });
        }

        //    // TODO Remove!
        //    public IActionResult TestException()
        //    {
        //        int i = 0;
        //        // Should throw Divide by zero error
        //        int j = 1 / i;
        //        return View();
        //    }
    }
}
