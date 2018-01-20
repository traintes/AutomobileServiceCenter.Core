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
    public class PromotionOperations : IPromotionOperations
    {
        private readonly IUnitOfWork _unitOfWork;

        public PromotionOperations(IUnitOfWork unitOfWork)
        {
            this._unitOfWork = unitOfWork;
        }

        public async Task CreatePromotionAsync(Promotion promotion)
        {
            using (this._unitOfWork)
            {
                await this._unitOfWork.Repository<Promotion>().AddAsync(promotion);
                this._unitOfWork.CommitTransaction();
            }
        }

        public async Task<List<Promotion>> GetAllPromotionsAsync()
        {
            IEnumerable<Promotion> promotions = await this._unitOfWork.Repository<Promotion>()
                .FindAllAsync();
            return promotions.ToList();
        }

        public async Task<Promotion> UpdatePromotionAsync(string rowKey, Promotion promotion)
        {
            Promotion originalPromotion = await this._unitOfWork.Repository<Promotion>()
                .FindAsync(promotion.PartitionKey, rowKey);
            if (originalPromotion != null)
            {
                originalPromotion.Header = promotion.Header;
                originalPromotion.Content = promotion.Content;
                originalPromotion.IsDeleted = promotion.IsDeleted;
            }

            using (this._unitOfWork)
            {
                await this._unitOfWork.Repository<Promotion>().UpdateAsync(originalPromotion);
                this._unitOfWork.CommitTransaction();
            }

            return originalPromotion;
        }
    }
}
