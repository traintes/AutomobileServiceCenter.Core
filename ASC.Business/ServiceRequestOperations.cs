using ASC.Business.Interfaces;
using ASC.DataAccess.Interfaces;
using ASC.Models.Models;
using ASC.Models.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASC.Business
{
    public class ServiceRequestOperations : IServiceRequestOperations
    {
        private readonly IUnitOfWork _unitOfWork;

        public ServiceRequestOperations(IUnitOfWork unitOfWork)
        {
            this._unitOfWork = unitOfWork;
        }

        public async Task CreateServiceRequestAsync(ServiceRequest request)
        {
            using (this._unitOfWork)
            {
                await this._unitOfWork.Repository<ServiceRequest>().AddAsync(request);
                this._unitOfWork.CommitTransaction();
            }
        }

        public async Task<List<ServiceRequest>> GetServiceRequestsByRequestedDateAndStatus(
            DateTime? requestedDate,
            List<string> status = null,
            string email = "",
            string serviceEngineerEmail = "")
        {
            string query = Queries.GetDashboardQuery(requestedDate, status, email, serviceEngineerEmail);
            IEnumerable<ServiceRequest> serviceRequests = await this._unitOfWork.Repository<ServiceRequest>()
                .FindAllByQuery(query);
            return serviceRequests.ToList();
        }

        public async Task<List<ServiceRequest>> GetServiceRequestsFromAudit(string serviceEngineerEmail = "")
        {
            string query = Queries.GetDashboardAuditQuery(serviceEngineerEmail);
            IEnumerable<ServiceRequest> serviceRequests = await this._unitOfWork.Repository<ServiceRequest>()
                .FindAllInAuditByQuery(query);
            return serviceRequests.ToList();
        }

        public async Task<List<ServiceRequest>> GetActiveServiceRequests(List<string> status)
        {
            string query = Queries.GetDashboardServiceEngineersQuery(status);
            IEnumerable<ServiceRequest> serviceRequests = await this._unitOfWork.Repository<ServiceRequest>()
                .FindAllByQuery(query);
            return serviceRequests.ToList();
        }

        public async Task<ServiceRequest> GetServiceRequestByRowKey(string id)
        {
            string query = Queries.GetServiceRequestDetailsQuery(id);
            IEnumerable<ServiceRequest> serviceRequests = await this._unitOfWork.Repository<ServiceRequest>()
                .FindAllByQuery(query);
            return serviceRequests.FirstOrDefault();
        }

        public async Task<List<ServiceRequest>> GetServiceRequestAuditByPartitionKey(string id)
        {
            string query = Queries.GetServiceRequestAuditDetailsQuery(id);
            IEnumerable<ServiceRequest> serviceRequests = await this._unitOfWork.Repository<ServiceRequest>()
                .FindAllInAuditByQuery(query);
            return serviceRequests.ToList();
        }

        public async Task<ServiceRequest> UpdateServiceRequestAsync(ServiceRequest request)
        {
            using (this._unitOfWork)
            {
                await this._unitOfWork.Repository<ServiceRequest>().UpdateAsync(request);
                this._unitOfWork.CommitTransaction();

                return request;
            }
        }

        public async Task<ServiceRequest> UpdateServiceRequestStatusAsync(string rowKey, string partitionKey, string status)
        {
            using (this._unitOfWork)
            {
                ServiceRequest serviceRequest = await this._unitOfWork.Repository<ServiceRequest>()
                    .FindAsync(partitionKey, rowKey);
                if (serviceRequest == null)
                    throw new NullReferenceException();

                serviceRequest.Status = status;

                await this._unitOfWork.Repository<ServiceRequest>().UpdateAsync(serviceRequest);
                this._unitOfWork.CommitTransaction();

                return serviceRequest;
            }
        }
    }
}
