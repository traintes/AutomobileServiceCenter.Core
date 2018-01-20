using ASC.Business.Interfaces;
using ASC.Models.Models;
using ASC.Utilities;
using ASC.Web.Configuration;
using ASC.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Infrastructure;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ASC.Web.ServiceHub
{
    public class ServiceMessagesHub : Hub
    {
        private readonly IConnectionManager _signalRConnectionManager;
        private readonly IHttpContextAccessor _userHttpContext;
        private readonly IServiceRequestOperations _serviceRequestOperations;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOnlineUsersOperations _onlineUserOperations;
        private readonly IOptions<ApplicationSettings> _options;
        private readonly string _serviceRequestId;

        public ServiceMessagesHub(IConnectionManager signalRConnectionManager,
            IHttpContextAccessor userHttpContext,
            IServiceRequestOperations serviceRequestOperations,
            UserManager<ApplicationUser> userManager,
            IOnlineUsersOperations onlineUserOperations,
            IOptions<ApplicationSettings> options)
        {
            this._signalRConnectionManager = signalRConnectionManager;
            this._userHttpContext = userHttpContext;
            this._serviceRequestOperations = serviceRequestOperations;
            this._userManager = userManager;
            this._onlineUserOperations = onlineUserOperations;
            this._options = options;

            this._serviceRequestId = this._userHttpContext.HttpContext.Request.Headers["ServiceRequestId"];
        }

        public override async Task OnConnected()
        {
            if (!string.IsNullOrWhiteSpace(this._serviceRequestId))
            {
                await this._onlineUserOperations.CreateOnlineUserAsync(
                    this._userHttpContext.HttpContext.User.GetCurrentUserDetails().Email);
                await this.UpdateServiceRequestClients();
            }

            await base.OnConnected();
        }

        public override async Task OnDisconnected(bool stopCalled)
        {
            if (!string.IsNullOrWhiteSpace(this._serviceRequestId))
            {
                await this._onlineUserOperations.DeleteOnlineUserAsync(
                    this._userHttpContext.HttpContext.User.GetCurrentUserDetails().Email);
                await this.UpdateServiceRequestClients();
            }

            await base.OnDisconnected(stopCalled);
        }

        private async Task UpdateServiceRequestClients()
        {
            // Get Hub Context
            IHubContext hubContext = this._signalRConnectionManager.GetHubContext<ServiceMessagesHub>();

            // Get Service Request Details
            ServiceRequest serviceRequest = await this._serviceRequestOperations
                .GetServiceRequestByRowKey(this._serviceRequestId);

            // Get Customer and Service Engineer names
            string customerName = (await this._userManager
                .FindByEmailAsync(serviceRequest.PartitionKey)).UserName;
            string serviceEngineerName = string.Empty;
            if (!string.IsNullOrWhiteSpace(serviceRequest.ServiceEngineer))
                serviceEngineerName = (await this._userManager
                    .FindByEmailAsync(serviceRequest.ServiceEngineer)).UserName;
            string adminName = (await this._userManager
                .FindByEmailAsync(this._options.Value.AdminEmail)).UserName;

            // Check Admin, Service Engineer and Customer are connected.
            bool isAdminOnline = await this._onlineUserOperations
                .GetOnlineUserAsync(this._options.Value.AdminEmail);
            bool isServiceEngineerOnline = false;
            if (!string.IsNullOrWhiteSpace(serviceRequest.ServiceEngineer))
                isServiceEngineerOnline = await this._onlineUserOperations
                    .GetOnlineUserAsync(serviceRequest.ServiceEngineer);
            bool isCustomerOnline = await this._onlineUserOperations
                .GetOnlineUserAsync(serviceRequest.PartitionKey);

            List<string> users = new List<string>();
            if (isAdminOnline)
                users.Add(adminName);
            if (!string.IsNullOrWhiteSpace(serviceEngineerName) && isServiceEngineerOnline)
                users.Add(serviceEngineerName);
            if (isCustomerOnline)
                users.Add(customerName);

            // Send notifications
            hubContext.Clients.Users(users).online(new
            {
                isAd = isAdminOnline,
                isSe = isServiceEngineerOnline,
                isCu = isCustomerOnline,
            });
        }
    }
}
