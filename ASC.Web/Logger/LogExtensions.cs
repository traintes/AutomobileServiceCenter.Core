using ASC.Business.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ASC.Web.Logger
{
    public class AzureStorageLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly Func<string, LogLevel, bool> _filter;
        private readonly ILogDataOperations _logOperations;

        public AzureStorageLogger(string categoryName, Func<string, LogLevel, bool> filter,
            ILogDataOperations logOperations)
        {
            this._categoryName = categoryName;
            this._filter = filter;
            this._logOperations = logOperations;
        }

        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel)
            => (this._filter == null || this._filter(this._categoryName, logLevel));

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (!this.IsEnabled(logLevel))
                return;

            if (exception == null)
                this._logOperations.CreateLogAsync(logLevel.ToString(), formatter(state, exception));
            else
                this._logOperations.CreateExceptionLogAsync(eventId.Name, exception.Message,
                    exception.StackTrace);
        }
    }

    public class AzureStorageLoggerProvider : ILoggerProvider
    {
        private readonly Func<string, LogLevel, bool> _filter;
        private readonly ILogDataOperations _logOperations;

        public AzureStorageLoggerProvider(Func<string, LogLevel, bool> filter,
            ILogDataOperations logOperations)
        {
            this._logOperations = logOperations;
            this._filter = filter;
        }

        public ILogger CreateLogger(string categoryName)
            => new AzureStorageLogger(categoryName, this._filter, this._logOperations);

        public void Dispose() { }
    }

    public static class EmailLoggerExtensions
    {
        public static ILoggerFactory AddAzureTableStorageLog(this ILoggerFactory factory,
            ILogDataOperations logOperations, Func<string, LogLevel, bool> filter = null)
        {
            factory.AddProvider(new AzureStorageLoggerProvider(filter, logOperations));
            return factory;
        }
    }
}
