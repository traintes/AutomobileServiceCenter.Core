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
    public class MasterDataOperations : IMasterDataOperations
    {
        private readonly IUnitOfWork _unitOfWork;
        public MasterDataOperations(IUnitOfWork unitOfWork)
        {
            this._unitOfWork = unitOfWork;
        }

        public async Task<List<MasterDataKey>> GetAllMasterKeysAsync()
        {
            IEnumerable<MasterDataKey> masterKeys = await this._unitOfWork
                .Repository<MasterDataKey>().FindAllAsync();
            return masterKeys.ToList();
        }

        public async Task<List<MasterDataKey>> GetMasterKeyByNameAsync(string name)
        {
            IEnumerable<MasterDataKey> masterKeys = await this._unitOfWork
                .Repository<MasterDataKey>().FindAllByPartititionKeyAsync(name);
            return masterKeys.ToList();
        }

        public async Task<bool> InsertMasterKeyAsync(MasterDataKey key)
        {
            using (this._unitOfWork)
            {
                await this._unitOfWork.Repository<MasterDataKey>().AddAsync(key);
                this._unitOfWork.CommitTransaction();
                return true;
            }
        }

        public async Task<List<MasterDataValue>> GetAllMasterValuesByKeyAsync(string key)
        {
            IEnumerable<MasterDataValue> masterKeys = await this._unitOfWork
                .Repository<MasterDataValue>().FindAllByPartititionKeyAsync(key);
            return masterKeys.ToList();
        }

        public async Task<MasterDataValue> GetMasterValueByNameAsync(string key, string name)
        {
            MasterDataValue masterValues = await this._unitOfWork
                .Repository<MasterDataValue>().FindAsync(key, name);
            return masterValues;
        }

        public async Task<bool> InsertMasterValueAsync(MasterDataValue value)
        {
            using (this._unitOfWork)
            {
                await this._unitOfWork.Repository<MasterDataValue>().AddAsync(value);
                this._unitOfWork.CommitTransaction();
                return true;
            }
        }

        public async Task<bool> UpdateMasterKeyAsync(string originalPartitionKey, MasterDataKey key)
        {
            using (this._unitOfWork)
            {
                MasterDataKey masterKey = await this._unitOfWork.Repository<MasterDataKey>()
                    .FindAsync(originalPartitionKey, key.RowKey);
                masterKey.IsActive = key.IsActive;
                masterKey.IsDeleted = key.IsDeleted;
                masterKey.Name = key.Name;

                await this._unitOfWork.Repository<MasterDataKey>().UpdateAsync(masterKey);
                this._unitOfWork.CommitTransaction();
                return true;
            }
        }

        public async Task<bool> UpdateMasterValueAsync(string originalPartitionKey, string originalRowKey, MasterDataValue value)
        {
            using (this._unitOfWork)
            {
                MasterDataValue masterValue = await this._unitOfWork.Repository<MasterDataValue>()
                    .FindAsync(originalPartitionKey, originalRowKey);
                masterValue.IsActive = value.IsActive;
                masterValue.IsDeleted = value.IsDeleted;
                masterValue.Name = value.Name;

                await this._unitOfWork.Repository<MasterDataValue>().UpdateAsync(masterValue);
                this._unitOfWork.CommitTransaction();
                return true;
            }
        }

        public async Task<bool> UploadBulkMasterData(List<MasterDataValue> values)
        {
            using (this._unitOfWork)
            {
                foreach(MasterDataValue value in values)
                {
                    // Find, if null insert MasterKey
                    List<MasterDataKey> masterKey =
                        await this.GetMasterKeyByNameAsync(value.PartitionKey);
                    if (!masterKey.Any())
                    {
                        await this._unitOfWork.Repository<MasterDataKey>()
                            .AddAsync(new MasterDataKey()
                            {
                                Name = value.PartitionKey,
                                RowKey = Guid.NewGuid().ToString(),
                                PartitionKey = value.PartitionKey,
                            });
                    }

                    // Find, if null Insert MasterValue
                    List<MasterDataValue> masterValuesByKey = 
                        await this.GetAllMasterValuesByKeyAsync(value.PartitionKey);
                    MasterDataValue masterValue = masterValuesByKey
                        .FirstOrDefault(p => p.Name == value.Name);
                    if (masterValue == null)
                        await this._unitOfWork.Repository<MasterDataValue>().AddAsync(value);
                    else
                    {
                        masterValue.IsActive = value.IsActive;
                        masterValue.IsDeleted = value.IsDeleted;
                        masterValue.Name = value.Name;
                        await this._unitOfWork.Repository<MasterDataValue>()
                            .UpdateAsync(masterValue);
                    }
                }

                this._unitOfWork.CommitTransaction();
                return true;
            }
        }

        public async Task<List<MasterDataValue>> GetAllMasterValuesAsync()
        {
            IEnumerable<MasterDataValue> masterValues = await this._unitOfWork
                .Repository<MasterDataValue>().FindAllAsync();
            return masterValues.ToList();
        }
    }
}
