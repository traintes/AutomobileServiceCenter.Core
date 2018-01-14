using ASC.Business.Interfaces;
using ASC.Models.BaseTypes;
using ASC.Models.Models;
using ASC.Utilities;
using ASC.Web.Areas.ServiceRequests.Models;
using ASC.Web.Controllers;
using ASC.Web.Data;
using ASC.Web.Models;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ASC.Web.Areas.ServiceRequests.Controllers
{
    [Area("ServiceRequests")]
    public class ServiceRequestController : BaseController
    {
        private readonly IServiceRequestOperations _serviceRequestOperations;
        private readonly IMapper _mapper;
        private readonly IMasterDataCacheOperations _masterData;
        private readonly UserManager<ApplicationUser> _userManager;

        public ServiceRequestController(IServiceRequestOperations operations, IMapper mapper,
            IMasterDataCacheOperations masterData, UserManager<ApplicationUser> userManager)
        {
            this._serviceRequestOperations = operations;
            this._mapper = mapper;
            this._masterData = masterData;
            this._userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> ServiceRequest()
        {
            MasterDataCache masterData = await this._masterData.GetMasterDataCacheAsync();
            ViewBag.VehicleTypes = masterData.Values
                .Where(p => p.PartitionKey == MasterKeys.VehicleType.ToString()).ToList();
            ViewBag.VehicleNames = masterData.Values
                .Where(p => p.PartitionKey == MasterKeys.VehicleName.ToString()).ToList();
            return View(new NewServiceRequestViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> ServiceRequest(NewServiceRequestViewModel request)
        {
            if (!ModelState.IsValid)
            {
                MasterDataCache masterData = await this._masterData.GetMasterDataCacheAsync();
                ViewBag.VehicleTypes = masterData.Values
                    .Where(p => p.PartitionKey == MasterKeys.VehicleType.ToString()).ToList();
                ViewBag.VehicleNames = masterData.Values
                    .Where(p => p.PartitionKey == MasterKeys.VehicleName.ToString()).ToList();
                return View(request);
            }

            // Map the view model to Azure model
            ServiceRequest serviceRequest = this._mapper
                .Map<NewServiceRequestViewModel, ServiceRequest>(request);

            // Set RowKey, PartitionKey, RequestedDate, Status properties
            serviceRequest.PartitionKey = HttpContext.User.GetCurrentUserDetails().Email;
            serviceRequest.RowKey = Guid.NewGuid().ToString();
            serviceRequest.RequestedDate = request.RequestedDate;
            serviceRequest.Status = Status.New.ToString();

            await this._serviceRequestOperations.CreateServiceRequestAsync(serviceRequest);

            return RedirectToAction("Dashboard", "Dashboard", new { Area = "ServiceRequests" });
        }

        [HttpGet]
        public async Task<IActionResult> ServiceRequestDetails(string id)
        {
            ServiceRequest serviceRequestDetails = await this._serviceRequestOperations
                .GetServiceRequestByRowKey(id);

            // Access Check
            if (HttpContext.User.IsInRole(Roles.Engineer.ToString()) &&
                serviceRequestDetails.ServiceEngineer != HttpContext.User.GetCurrentUserDetails().Email)
                throw new UnauthorizedAccessException();

            if (HttpContext.User.IsInRole(Roles.User.ToString()) &&
                serviceRequestDetails.PartitionKey != HttpContext.User.GetCurrentUserDetails().Email)
                throw new UnauthorizedAccessException();

            List<ServiceRequest> serviceRequestAuditDetails = await this._serviceRequestOperations
                .GetServiceRequestAuditByPartitionKey($"{serviceRequestDetails.PartitionKey}-{id}");

            // Select List Data
            MasterDataCache masterData = await this._masterData.GetMasterDataCacheAsync();
            ViewBag.VehicleTypes = masterData.Values
                .Where(p => p.PartitionKey == MasterKeys.VehicleType.ToString()).ToList();
            ViewBag.VehicleNames = masterData.Values
                .Where(p => p.PartitionKey == MasterKeys.VehicleName.ToString()).ToList();
            ViewBag.Status = Enum.GetValues(typeof(Status)).Cast<Status>().Select(v => v.ToString()).ToList();
            ViewBag.ServiceEngineers = await this._userManager.GetUsersInRoleAsync(Roles.Engineer.ToString());

            return View(new ServiceRequestDetailViewModel
            {
                ServiceRequest = this._mapper
                    .Map<ServiceRequest, UpdateServiceRequestViewModel>(serviceRequestDetails),
                ServiceRequestAudit = serviceRequestAuditDetails.OrderByDescending(p => p.Timestamp).ToList(),
            });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateServiceRequestDetails(
            UpdateServiceRequestViewModel serviceRequest)
        {
            ServiceRequest originalServiceRequest = await this._serviceRequestOperations
                .GetServiceRequestByRowKey(serviceRequest.RowKey);
            originalServiceRequest.RequestedServices = serviceRequest.RequestedServices;

            // Update Status only if user roles is either Admin or Engineer
            // Or Customer can update the status if it is only in Pending Customer Approval.
            if (HttpContext.User.IsInRole(Roles.Admin.ToString()) ||
                HttpContext.User.IsInRole(Roles.Engineer.ToString()) ||
                (HttpContext.User.IsInRole(Roles.User.ToString()) &&
                originalServiceRequest.Status == Status.PendingCustomerApproval.ToString()))
            {
                originalServiceRequest.Status = serviceRequest.Status;
            }

            // Update Service Engineer field only if user role is Admin
            if (HttpContext.User.IsInRole(Roles.Admin.ToString()))
                originalServiceRequest.ServiceEngineer = serviceRequest.ServiceEngineer;

            await this._serviceRequestOperations.UpdateServiceRequestAsync(originalServiceRequest);

            return RedirectToAction("ServiceRequestDetails", "ServiceRequest", new
            {
                Area = "ServiceRequests",
                Id = serviceRequest.RowKey,
            });
        }

        public async Task<IActionResult> CheckDenialService(DateTime requestedDate)
        {
            List<ServiceRequest> serviceRequests = await this._serviceRequestOperations
                .GetServiceRequestsByRequestedDateAndStatus(DateTime.UtcNow.AddDays(-90),
                    new List<string>() { Status.Denied.ToString() },
                    HttpContext.User.GetCurrentUserDetails().Email);

            if (serviceRequests.Any())
                return Json(data: "There is a denied service request for you in last 90 days. " +
                    "Please contact ASC Admin.");
            return Json(data: true);
        }
    }
}
