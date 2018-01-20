using ASC.Business.Interfaces;
using ASC.DataAccess.Interfaces;
using ASC.Models.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASC.Business
{
    public class ServiceRequestMessageOperations : IServiceRequestMessageOperations
    {
        private readonly IUnitOfWork _unitOfWork;
        public ServiceRequestMessageOperations(IUnitOfWork unitOfWork)
        {
            this._unitOfWork = unitOfWork;
        }

        public async Task CreateServiceRequestMessageAsync(ServiceRequestMessage message)
        {
            using (this._unitOfWork)
            {
                await this._unitOfWork.Repository<ServiceRequestMessage>().AddAsync(message);
                this._unitOfWork.CommitTransaction();
            }
        }

        public async Task<List<ServiceRequestMessage>> GetServiceRequestMessageAsync(string serviceRequestId)
        {
            IEnumerable<ServiceRequestMessage> serviceRequestMessages = await this._unitOfWork
                .Repository<ServiceRequestMessage>().FindAllByPartititionKeyAsync(serviceRequestId);
            return serviceRequestMessages.ToList();
        }
    }
}
