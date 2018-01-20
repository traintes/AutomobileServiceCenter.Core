using ASC.Business.Interfaces;
using ASC.Models.BaseTypes;
using ASC.Models.Models;
using ASC.Utilities;
using ASC.Web.Areas.ServiceRequests.Models;
using ASC.Web.Configuration;
using ASC.Web.Controllers;
using ASC.Web.Data;
using ASC.Web.Models;
using ASC.Web.ServiceHub;
using ASC.Web.Services;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR.Infrastructure;
using Microsoft.Extensions.Options;
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
        private readonly IServiceRequestMessageOperations _serviceRequestMessageOperations;
        private readonly IConnectionManager _signalRConnectionManager;
        private readonly IMapper _mapper;
        private readonly IMasterDataCacheOperations _masterData;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly IOptions<ApplicationSettings> _options;
        private readonly IOnlineUsersOperations _onlineUsersOperations;
        private readonly ISmsSender _smsSender;

        public ServiceRequestController(IServiceRequestOperations operations,
            IServiceRequestMessageOperations messageOperations,
            IConnectionManager signalRConnectionManager,
            IMapper mapper,
            IMasterDataCacheOperations masterData,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            IOptions<ApplicationSettings> options,
            IOnlineUsersOperations onlineUsersOperations,
            ISmsSender smsSender)
        {
            this._serviceRequestOperations = operations;
            this._serviceRequestMessageOperations = messageOperations;
            this._signalRConnectionManager = signalRConnectionManager;
            this._mapper = mapper;
            this._masterData = masterData;
            this._userManager = userManager;
            this._emailSender = emailSender;
            this._options = options;
            this._onlineUsersOperations = onlineUsersOperations;
            this._smsSender = smsSender;
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

            bool isServiceRequestStatusUpdated = false;
            // Update Status only if user roles is either Admin or Engineer
            // Or Customer can update the status if it is only in Pending Customer Approval.
            if (HttpContext.User.IsInRole(Roles.Admin.ToString()) ||
                HttpContext.User.IsInRole(Roles.Engineer.ToString()) ||
                (HttpContext.User.IsInRole(Roles.User.ToString()) &&
                originalServiceRequest.Status == Status.PendingCustomerApproval.ToString()))
            {
                if (originalServiceRequest.Status != serviceRequest.Status)
                    isServiceRequestStatusUpdated = true;

                originalServiceRequest.Status = serviceRequest.Status;
            }

            // Update Service Engineer field only if user role is Admin
            if (HttpContext.User.IsInRole(Roles.Admin.ToString()))
                originalServiceRequest.ServiceEngineer = serviceRequest.ServiceEngineer;

            await this._serviceRequestOperations.UpdateServiceRequestAsync(originalServiceRequest);

            if ((HttpContext.User.IsInRole(Roles.Admin.ToString()) ||
                HttpContext.User.IsInRole(Roles.Engineer.ToString())) &&
                originalServiceRequest.Status == Status.PendingCustomerApproval.ToString())
            {
                await this._emailSender.SendEmailAsync(originalServiceRequest.PartitionKey,
                    "Your Service Request is almost completed!",
                    "Please visit the ASC application and review your Service request.");
            }

            if (isServiceRequestStatusUpdated)
                await this.SendSmsAndWebNotifications(originalServiceRequest);

            return RedirectToAction("ServiceRequestDetails", "ServiceRequest", new
            {
                Area = "ServiceRequests",
                Id = serviceRequest.RowKey,
            });
        }

        private async Task SendSmsAndWebNotifications(ServiceRequest serviceRequest)
        {
            // Send SMS Notification
            string phoneNumber = (await this._userManager
                .FindByEmailAsync(serviceRequest.PartitionKey)).PhoneNumber;
            // TODO: Sending SMS is temporarily disabled!
            //if (!string.IsNullOrWhiteSpace(phoneNumber))
            //    await this._smsSender.SendSmsAsync(phoneNumber,
            //        string.Format("Service Request Status updated to {0}", serviceRequest.Status));

            // Get Customer name
            string customerName = (await this._userManager
                .FindByEmailAsync(serviceRequest.PartitionKey)).UserName;

            // Send web notifications
            this._signalRConnectionManager.GetHubContext<ServiceMessagesHub>()
                .Clients.User(customerName).publishNotification(new
                {
                    status = serviceRequest.Status
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

        [HttpGet]
        public IActionResult SearchServiceRequests()
        {
            return View(new SearchServiceRequestsViewModel());
        }

        [HttpGet]
        public async Task<IActionResult> SearchServiceRequestResults(string email, DateTime? requestedDate)
        {
            List<ServiceRequest> results = new List<ServiceRequest>();
            if (string.IsNullOrEmpty(email) && !requestedDate.HasValue)
                return Json(new { data = results });

            if (HttpContext.User.IsInRole(Roles.Admin.ToString()))
                results = await this._serviceRequestOperations
                    .GetServiceRequestsByRequestedDateAndStatus(requestedDate, null, email);
            else
            {
                results = await this._serviceRequestOperations
                    .GetServiceRequestsByRequestedDateAndStatus(requestedDate, null, email,
                        HttpContext.User.GetCurrentUserDetails().Email);
            }

            return Json(new { data = results.OrderByDescending(p => p.RequestedDate).ToList() });
        }

        [HttpGet]
        public async Task<IActionResult> ServiceRequestMessages(string serviceRequestId)
        {
            return Json((await this._serviceRequestMessageOperations
                .GetServiceRequestMessageAsync(serviceRequestId))
                .OrderBy(p => p.MessageDate));
        }

        [HttpPost]
        public async Task<IActionResult> CreateServiceRequestMessage(ServiceRequestMessage message)
        {
            // Message and Service Request Id (Service request Id is the partition key for a message)
            if (string.IsNullOrWhiteSpace(message.Message) || 
                string.IsNullOrWhiteSpace(message.PartitionKey))
                return Json(false);

            // Get Service Request Details
            ServiceRequest serviceRequestDetails = await this._serviceRequestOperations
                .GetServiceRequestByRowKey(message.PartitionKey);

            // Populate message details
            message.FromEmail = HttpContext.User.GetCurrentUserDetails().Email;
            message.FromDisplayName = HttpContext.User.GetCurrentUserDetails().Name;
            message.MessageDate = DateTime.UtcNow;
            message.RowKey = Guid.NewGuid().ToString();

            // Get Customer and Service Engineer names
            string customerName = (await this._userManager
                .FindByEmailAsync(serviceRequestDetails.PartitionKey)).UserName;
            string serviceEngineerName = string.Empty;
            if (!string.IsNullOrWhiteSpace(serviceRequestDetails.ServiceEngineer))
                serviceEngineerName = (await this._userManager
                    .FindByEmailAsync(serviceRequestDetails.ServiceEngineer)).UserName;
            string adminName = (await this._userManager
                .FindByEmailAsync(this._options.Value.AdminEmail)).UserName;

            // Save the message to Azure Storage
            await this._serviceRequestMessageOperations.CreateServiceRequestMessageAsync(message);

            List<string> users = new List<string> { customerName, adminName };
            if (!string.IsNullOrWhiteSpace(serviceEngineerName))
                users.Add(serviceEngineerName);

            // Broadcast the message to all clients associated with Service Request
            this._signalRConnectionManager.GetHubContext<ServiceMessagesHub>()
                .Clients.Users(users).publishMessage(message);

            // Return true
            return Json(true);
        }

        [HttpGet]
        public async Task<IActionResult> MarkOfflineUser()
        {
            // Delete the current logged-in user from OnlineUsers entity
            await this._onlineUsersOperations
                .DeleteOnlineUserAsync(HttpContext.User.GetCurrentUserDetails().Email);

            string serviceRequestId = HttpContext.Request.Headers["ServiceRequestId"];
            // Get Service Request Details
            ServiceRequest serviceRequest = await this._serviceRequestOperations
                .GetServiceRequestByRowKey(serviceRequestId);

            // Get Customer and Service Engineer name
            string customerName = (await this._userManager
                .FindByEmailAsync(serviceRequest.PartitionKey)).UserName;
            string serviceEngineerName = string.Empty;
            if (!string.IsNullOrWhiteSpace(serviceRequest.ServiceEngineer))
                serviceEngineerName = (await this._userManager
                    .FindByEmailAsync(serviceRequest.ServiceEngineer)).UserName;
            string adminName = (await this._userManager
                .FindByEmailAsync(this._options.Value.AdminEmail)).UserName;

            // Check Admin, Service Engineer and Customer are connected.
            bool isAdminOnline = await this._onlineUsersOperations
                .GetOnlineUserAsync(this._options.Value.AdminEmail);
            bool isServiceEngineerOnline = false;
            if (!string.IsNullOrWhiteSpace(serviceRequest.ServiceEngineer))
                isServiceEngineerOnline = await this._onlineUsersOperations
                    .GetOnlineUserAsync(serviceRequest.ServiceEngineer);
            bool isCustomerOnline = await this._onlineUsersOperations
                .GetOnlineUserAsync(serviceRequest.PartitionKey);

            List<string> users = new List<string>();
            if (isAdminOnline)
                users.Add(adminName);
            if (!string.IsNullOrWhiteSpace(serviceEngineerName) && isServiceEngineerOnline)
                users.Add(serviceEngineerName);
            if (isCustomerOnline)
                users.Add(customerName);

            // Send notifications
            this._signalRConnectionManager.GetHubContext<ServiceMessagesHub>().Clients.Users(users)
                .online(new
                    {
                        isAd = isAdminOnline,
                        isSe = isServiceEngineerOnline,
                        isCu = isCustomerOnline,
                    });

            return Json(true);
        }
    }
}
