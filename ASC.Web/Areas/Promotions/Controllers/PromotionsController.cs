using ASC.Business.Interfaces;
using ASC.Models.BaseTypes;
using ASC.Models.Models;
using ASC.Utilities;
using ASC.Web.Areas.Promotions.Models;
using ASC.Web.Controllers;
using ASC.Web.Data;
using ASC.Web.ServiceHub;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ASC.Web.Areas.Promotions.Controllers
{
    [Area("Promotions")]
    public class PromotionsController : BaseController
    {
        private readonly IPromotionOperations _promotionOperations;
        private readonly IMapper _mapper;
        private readonly IMasterDataCacheOperations _masterData;
        private readonly IConnectionManager _signalRConnectionManager;

        public PromotionsController(IPromotionOperations promotionOperations,
            IMapper mapper,
            IMasterDataCacheOperations masterData,
            IConnectionManager signalRConnectionManager)
        {
            this._promotionOperations = promotionOperations;
            this._mapper = mapper;
            this._masterData = masterData;
            this._signalRConnectionManager = signalRConnectionManager;
        }

        [HttpGet]
        public async Task<IActionResult> Promotion()
        {
            List<Promotion> promotions = await this._promotionOperations.GetAllPromotionsAsync();
            List<PromotionViewModel> promotionsViewModel = this._mapper
                .Map<List<Promotion>, List<PromotionViewModel>>(promotions);

            // Get All Master Keys and hold them in ViewBag for Select tag
            MasterDataCache masterData = await this._masterData.GetMasterDataCacheAsync();
            ViewBag.PromotionTypes = masterData.Values
                .Where(p => p.PartitionKey == MasterKeys.PromotionType.ToString()).ToList();

            // Hold all Promotions in session
            HttpContext.Session.SetSession("Promotions", promotionsViewModel);

            return View(new PromotionsViewModel
            {
                Promotions = promotionsViewModel != null ? promotionsViewModel : null,
                IsEdit = false,
                PromotionInContext = new PromotionViewModel(),
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Promotion(PromotionsViewModel promotions)
        {
            promotions.Promotions = HttpContext.Session.GetSession<List<PromotionViewModel>>("Promotions");
            if (!ModelState.IsValid)
                return View(promotions);

            Promotion promotion = this._mapper
                .Map<PromotionViewModel, Promotion>(promotions.PromotionInContext);
            if (promotions.IsEdit)
            {
                // Update Promotions
                await this._promotionOperations
                    .UpdatePromotionAsync(promotions.PromotionInContext.RowKey, promotion);
            }
            else
            {
                // Insert Promotion
                promotion.RowKey = Guid.NewGuid().ToString();
                await this._promotionOperations.CreatePromotionAsync(promotion);

                if (!promotion.IsDeleted)
                {
                    // Broadcast the message to all clients associated with new promotion
                    this._signalRConnectionManager.GetHubContext<ServiceMessagesHub>()
                        .Clients.All.publishPromotion(promotion);
                }
            }

            return RedirectToAction("Promotion");
        }

        [HttpGet]
        public async Task<IActionResult> Promotions()
        {
            List<Promotion> promotions = await this._promotionOperations.GetAllPromotionsAsync();
            List<Promotion> filteredPromotions = new List<Promotion>();
            if (promotions != null)
                filteredPromotions = promotions.Where(p => !p.IsDeleted)
                    .OrderByDescending(p => p.Timestamp).ToList();
            return View(filteredPromotions);
        }
    }
}
