using ASC.Business.Interfaces;
using ASC.Models.Models;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ASC.Web.Data
{
    public class MasterDataCache
    {
        public List<MasterDataKey> Keys { get; set; }
        public List<MasterDataValue> Values { get; set; }
    }

    public interface IMasterDataCacheOperations
    {
        Task<MasterDataCache> GetMasterDataCacheAsync();
        Task CreateMasterDataCacheAsync();
    }

    public class MasterDataCacheOperations : IMasterDataCacheOperations
    {
        private readonly IDistributedCache _cache;
        private readonly IMasterDataOperations _masterData;
        private readonly string MasterDataCacheName = "MasterDataCache";

        public MasterDataCacheOperations(IDistributedCache cache, IMasterDataOperations masterData)
        {
            this._cache = cache;
            this._masterData = masterData;
        }

        public async Task CreateMasterDataCacheAsync()
        {
            MasterDataCache masterDataCache = new MasterDataCache
            {
                Keys = (await this._masterData.GetAllMasterKeysAsync())
                    .Where(p => p.IsActive == true).ToList(),
                Values = (await this._masterData.GetAllMasterValuesAsync())
                    .Where(p => p.IsActive == true).ToList(),
            };

            await this._cache
                .SetStringAsync(this.MasterDataCacheName, JsonConvert.SerializeObject(masterDataCache));
        }

        public async Task<MasterDataCache> GetMasterDataCacheAsync()
        {
            return JsonConvert.DeserializeObject<MasterDataCache>(await this._cache
                .GetStringAsync(this.MasterDataCacheName));
        }
    }
}
