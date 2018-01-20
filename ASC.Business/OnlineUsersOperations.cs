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
    public class OnlineUsersOperations : IOnlineUsersOperations
    {
        private readonly IUnitOfWork _unitOfWork;

        public OnlineUsersOperations(IUnitOfWork unitOfWork)
        {
            this._unitOfWork = unitOfWork;
        }

        public async Task CreateOnlineUserAsync(string name)
        {
            using (this._unitOfWork)
            {
                IEnumerable<OnlineUser> user = await this._unitOfWork.Repository<OnlineUser>()
                    .FindAllByPartititionKeyAsync(name);
                if (user.Any())
                {
                    OnlineUser updateUser = user.FirstOrDefault();
                    updateUser.IsDeleted = false;
                    await this._unitOfWork.Repository<OnlineUser>().UpdateAsync(updateUser);
                }
                else
                {
                    await this._unitOfWork.Repository<OnlineUser>().AddAsync(new OnlineUser(name)
                    {
                        IsDeleted = false,
                    });
                }

                this._unitOfWork.CommitTransaction();
            }
        }

        public async Task DeleteOnlineUserAsync(string name)
        {
            using (this._unitOfWork)
            {
                IEnumerable<OnlineUser> user = await this._unitOfWork.Repository<OnlineUser>()
                    .FindAllByPartititionKeyAsync(name);
                if (user.Any())
                    await this._unitOfWork.Repository<OnlineUser>().DeleteAsync(user.FirstOrDefault());

                this._unitOfWork.CommitTransaction();
            }
        }

        public async Task<bool> GetOnlineUserAsync(string name)
        {
            IEnumerable<OnlineUser> user = await this._unitOfWork.Repository<OnlineUser>()
                .FindAllByPartititionKeyAsync(name);
            return user.Any() && user.FirstOrDefault().IsDeleted != true;
        }
    }
}
