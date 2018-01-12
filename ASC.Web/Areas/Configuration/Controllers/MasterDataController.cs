using ASC.Business.Interfaces;
using ASC.Models.Models;
using ASC.Utilities;
using ASC.Web.Areas.Configuration.Models;
using ASC.Web.Controllers;
using ASC.Web.Data;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ASC.Web.Areas.Configuration.Controllers
{
    [Area("Configuration")]
    [Authorize(Roles = "Admin")]
    public class MasterDataController : BaseController
    {
        private readonly IMasterDataOperations _masterData;
        private readonly IMapper _mapper;
        private readonly IMasterDataCacheOperations _masterDataCache;

        public MasterDataController(IMasterDataOperations masterData, IMapper mapper,
            IMasterDataCacheOperations masterDataCache)
        {
            this._masterData = masterData;
            this._mapper = mapper;
            this._masterDataCache = masterDataCache;
        }

        [HttpGet]
        public async Task<IActionResult> MasterKeys()
        {
            List<MasterDataKey> masterKeys = await this._masterData.GetAllMasterKeysAsync();
            List<MasterDataKeyViewModel> masterKeysViewModel = this._mapper
                .Map<List<MasterDataKey>, List<MasterDataKeyViewModel>>(masterKeys);

            // Hold all Master Keys in session
            HttpContext.Session.SetSession("MasterKeys", masterKeysViewModel);

            return View(new MasterKeysViewModel
            {
                //MasterKeys = masterKeysViewModel == null ? null : masterKeysViewModel.ToList(),
                //MasterKeys = masterKeysViewModel.Count != 0 ? masterKeysViewModel : null,
                MasterKeys = masterKeysViewModel != null ? masterKeysViewModel : null,
                IsEdit = false,
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MasterKeys(MasterKeysViewModel masterKeys)
        {
            masterKeys.MasterKeys = HttpContext.Session
                .GetSession<List<MasterDataKeyViewModel>>("MasterKeys");
            if (!ModelState.IsValid)
                return View(masterKeys);

            MasterDataKey masterKey = this._mapper
                .Map<MasterDataKeyViewModel, MasterDataKey>(masterKeys.MasterKeyInContext);
            if (masterKeys.IsEdit)
            {
                // Update Master Key
                await this._masterData
                    .UpdateMasterKeyAsync(masterKeys.MasterKeyInContext.PartitionKey, masterKey);
            }
            else
            {
                // Insert Master Key
                masterKey.RowKey = Guid.NewGuid().ToString();
                masterKey.PartitionKey = masterKey.Name;
                await this._masterData.InsertMasterKeyAsync(masterKey);
            }

            await this._masterDataCache.CreateMasterDataCacheAsync();
            return RedirectToAction("MasterKeys");
        }

        [HttpGet]
        public async Task<IActionResult> MasterValues()
        {
            // Get All Master Keys and hold them in ViewBag for Select tag
            ViewBag.MasterKeys = await this._masterData.GetAllMasterKeysAsync();

            return View(new MasterValuesViewModel
            {
                MasterValues = new List<MasterDataValueViewModel>(),
                IsEdit = false,
            });
        }

        [HttpGet]
        public async Task<IActionResult> MasterValuesByKey(string key)
        {
            // Get Master values based on master key.
            return Json(new { data = await this._masterData.GetAllMasterValuesByKeyAsync(key) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MasterValues(bool isEdit, MasterDataValueViewModel masterValue)
        {
            if (!ModelState.IsValid)
                return Json("Error");

            MasterDataValue masterDataValue = this._mapper
                .Map<MasterDataValueViewModel, MasterDataValue>(masterValue);
            if (isEdit)
            {
                // Update Master Value
                await this._masterData.UpdateMasterValueAsync(masterDataValue.PartitionKey,
                    masterDataValue.RowKey, masterDataValue);
            }
            else
            {
                // Insert Master Value
                masterDataValue.RowKey = Guid.NewGuid().ToString();
                await this._masterData.InsertMasterValueAsync(masterDataValue);
            }

            await this._masterDataCache.CreateMasterDataCacheAsync();
            return Json(true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadExcel()
        {
            IFormFileCollection files = Request.Form.Files;
            // Validations
            if (!files.Any())
                return Json(new { Error = true, Text = "Upload a file" });

            IFormFile excelFile = files.First();
            if(excelFile.Length <= 0)
                return Json(new { Error = true, Text = "Upload a file" });

            // Parse Excel Data
            List<MasterDataValue> masterData = await ParseMasterDataExcel(excelFile);
            bool result = await this._masterData.UploadBulkMasterData(masterData);

            await this._masterDataCache.CreateMasterDataCacheAsync();
            return Json(new { Success = result });
        }

        private async Task<List<MasterDataValue>> ParseMasterDataExcel(IFormFile excelFile)
        {
            List<MasterDataValue> masterValueList = new List<MasterDataValue>();
            using (MemoryStream memoryStream = new MemoryStream())
            {
                // Get MemoryStream from Excel file
                await excelFile.CopyToAsync(memoryStream);

                // Create a ExcelPackage object from MemoryStream
                using(ExcelPackage package = new ExcelPackage(memoryStream))
                {
                    // Get the first Excel sheet from the Workbook
                    ExcelWorksheet worksheet = package.Workbook.Worksheets[1];
                    int rowCount = worksheet.Dimension.Rows;

                    // Iterate all the rows and create the list of MasterDataValue
                    // Ignore first row as it is header
                    for (int row = 2; row <= rowCount; row++)
                    {
                        MasterDataValue masterDataValue = new MasterDataValue();
                        masterDataValue.RowKey = Guid.NewGuid().ToString();
                        masterDataValue.PartitionKey = worksheet.Cells[row, 1].Value.ToString();
                        masterDataValue.Name = worksheet.Cells[row, 2].Value.ToString();
                        masterDataValue.IsActive = Boolean
                            .Parse(worksheet.Cells[row, 3].Value.ToString());
                        masterValueList.Add(masterDataValue);
                    }
                }
            }

            return masterValueList;
        }
    }
}
