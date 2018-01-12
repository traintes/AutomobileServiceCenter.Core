using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ASC.Web.Filters
{
    public class CustomExceptionFilter : ExceptionFilterAttribute
    {
        private readonly ILogger<CustomExceptionFilter> _logger;
        private readonly IModelMetadataProvider _modelMetadataProvider;

        public CustomExceptionFilter(ILogger<CustomExceptionFilter> logger,
            IModelMetadataProvider modelMetadataProvider)
        {
            this._logger = logger;
            this._modelMetadataProvider = modelMetadataProvider;
        }

        public override async Task OnExceptionAsync(ExceptionContext context)
        {
            string logId = Guid.NewGuid().ToString();
            this._logger.LogError(new EventId(1000, logId), context.Exception, context.Exception.Message);

            ViewResult result = new ViewResult { ViewName = "CustomError" };
            result.ViewData = new ViewDataDictionary(this._modelMetadataProvider, context.ModelState);
            result.ViewData.Add("ExceptionId", logId);
            context.Result = result;
        }
    }
}
