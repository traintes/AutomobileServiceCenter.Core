using ASC.Business.Interfaces;
using ASC.DataAccess.Interfaces;
using ASC.Models.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ASC.Business
{
    public class LogDataOperations : ILogDataOperations
    {
        private readonly IUnitOfWork _unitOfWork;

        public LogDataOperations(IUnitOfWork unitOfWork)
        {
            this._unitOfWork = unitOfWork;
        }

        public async Task CreateExceptionLogAsync(string id, string message, string stacktrace)
        {
            using (this._unitOfWork)
            {
                await this._unitOfWork.Repository<ExceptionLog>().AddAsync(new ExceptionLog()
                {
                    RowKey = id,
                    PartitionKey = "Exception",
                    Message = message,
                    Stacktrace = stacktrace,
                });

                this._unitOfWork.CommitTransaction();
            }
        }

        public async Task CreateLogAsync(string category, string message)
        {
            using (this._unitOfWork)
            {
                await this._unitOfWork.Repository<Log>().AddAsync(new Log()
                {
                    RowKey = Guid.NewGuid().ToString(),
                    PartitionKey = category,
                    Message = message,
                });

                this._unitOfWork.CommitTransaction();
            }
        }

        public async Task CreateUserActivityAsync(string email, string action)
        {
            using (this._unitOfWork)
            {
                await this._unitOfWork.Repository<UserActivity>().AddAsync(new UserActivity()
                {
                    RowKey = Guid.NewGuid().ToString(),
                    PartitionKey = email,
                    Action = action,
                });

                this._unitOfWork.CommitTransaction();
            }
        }
    }
}
