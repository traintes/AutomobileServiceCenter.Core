using ASC.DataAccess.Interfaces;
using ASC.Models.BaseTypes;
using ASC.Utilities;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ASC.DataAccess
{
    public class Repository<T> : IRepository<T> where T : TableEntity, new()
    {
        private readonly CloudStorageAccount storageAccount;
        private readonly CloudTableClient tableClient;
        private readonly CloudTable storageTable;

        public IUnitOfWork Scope { get; set; }

        public Repository(IUnitOfWork scope)
        {
            this.storageAccount = CloudStorageAccount.Parse(scope.ConnectionString);

            this.tableClient = storageAccount.CreateCloudTableClient();
            CloudTable table = tableClient.GetTableReference(typeof(T).Name);

            this.storageTable = table;
            this.Scope = scope;
        }

        public async Task<T> AddAsync(T entity)
        {
            BaseEntity entityToInsert = entity as BaseEntity;
            DateTime now = DateTime.UtcNow;
            entityToInsert.CreatedDate = now;
            entityToInsert.UpdatedDate = now;

            TableOperation insertOperation = TableOperation.Insert(entity);
            TableResult result = await ExecuteAsync(insertOperation);
            return result.Result as T;
        }

        public async Task<T> UpdateAsync(T entity)
        {
            BaseEntity entityToUpdate = entity as BaseEntity;
            entityToUpdate.UpdatedDate = DateTime.UtcNow;

            TableOperation updateOperation = TableOperation.Replace(entity);
            TableResult result = await ExecuteAsync(updateOperation);
            return result.Result as T;
        }

        public async Task DeleteAsync(T entity)
        {
            BaseEntity entityToDelete = entity as BaseEntity;
            entityToDelete.UpdatedDate = DateTime.UtcNow;
            entityToDelete.IsDeleted = true;

            TableOperation deleteOperation = TableOperation.Replace(entityToDelete);
            await ExecuteAsync(deleteOperation);
        }

        public async Task<T> FindAsync(string partitionKey, string rowKey)
        {
            TableOperation retrieveOperation = TableOperation.Retrieve<T>(partitionKey, rowKey);
            TableResult result = await this.storageTable.ExecuteAsync(retrieveOperation);
            return result.Result as T;
        }

        public async Task<IEnumerable<T>> FindAllByPartititionKeyAsync(string partitionKey)
        {
            TableQuery<T> query = new TableQuery<T>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey));
            TableContinuationToken tableContinuationToken = null;
            TableQuerySegment<T> result = await this.storageTable.ExecuteQuerySegmentedAsync(query, tableContinuationToken);
            return result.Results as IEnumerable<T>;
        }

        public async Task<IEnumerable<T>> FindAllAsync()
        {
            TableQuery<T> query = new TableQuery<T>();
            TableContinuationToken tableContinuationToken = null;
            TableQuerySegment<T> result = await this.storageTable.ExecuteQuerySegmentedAsync(query, tableContinuationToken);
            return result.Results as IEnumerable<T>;
        }

        public async Task CreateTableAsync()
        {
            CloudTable table = this.tableClient.GetTableReference(typeof(T).Name);
            await table.CreateIfNotExistsAsync();

            if (typeof(IAuditTracker).IsAssignableFrom(typeof(T)))
            {
                CloudTable auditTable = this.tableClient.GetTableReference($"{typeof(T).Name}Audit");
                await auditTable.CreateIfNotExistsAsync();
            }
        }

        private async Task<TableResult> ExecuteAsync(TableOperation operation)
        {
            var rollbackAction = CreateRollbackAction(operation);
            TableResult result = await this.storageTable.ExecuteAsync(operation);
            this.Scope.RollbackActions.Enqueue(rollbackAction);

            // Audit Implementation
            if(operation.Entity is IAuditTracker)
            {
                // Make sure do not use same RowKey and PartitionKey
                T auditEntity = ObjectExtensions.CopyObject<T>(operation.Entity);
                auditEntity.PartitionKey = $"{auditEntity.PartitionKey}-{auditEntity.RowKey}";
                auditEntity.RowKey = $"{DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff")}";

                TableOperation auditOperation = TableOperation.Insert(auditEntity);
                Task<Action> auditRollbackAction = CreateRollbackAction(auditOperation, true);

                CloudTable auditTable = this.tableClient.GetTableReference($"{typeof(T).Name}Audit");
                await auditTable.ExecuteAsync(auditOperation);

                this.Scope.RollbackActions.Enqueue(auditRollbackAction);
            }

            return result;
        }

        private async Task<Action> CreateRollbackAction(TableOperation operation, bool IsAuditOperation = false)
        {
            if (operation.OperationType == TableOperationType.Retrieve)
                return null;

            ITableEntity tableEntity = operation.Entity;
            CloudTable cloudTable = !IsAuditOperation ? this.storageTable
                : this.tableClient.GetTableReference($"{typeof(T).Name}Audit");

            switch (operation.OperationType)
            {
                case TableOperationType.Insert:
                    return async () => await UndoInsertOperationAsync(cloudTable, tableEntity);
                case TableOperationType.Delete:
                    return async () => await UndoDeleteOperation(cloudTable, tableEntity);
                case TableOperationType.Replace:
                    var retrieveResult = await cloudTable.ExecuteAsync(TableOperation.Retrieve(tableEntity.PartitionKey, tableEntity.RowKey));
                    return async () => await UndoReplaceOperation(cloudTable, retrieveResult.Result as DynamicTableEntity, tableEntity);
                default:
                    throw new InvalidOperationException("The storage operation cannot be identified.");
            }
        }

        private async Task UndoInsertOperationAsync(CloudTable table, ITableEntity entity)
        {
            TableOperation deleteOperation = TableOperation.Delete(entity);
            await table.ExecuteAsync(deleteOperation);
        }

        private async Task UndoDeleteOperation(CloudTable table, ITableEntity entity)
        {
            BaseEntity entityToRestore = entity as BaseEntity;
            entityToRestore.IsDeleted = false;

            TableOperation insertOperation = TableOperation.Replace(entity);
            await table.ExecuteAsync(insertOperation);
        }

        private async Task UndoReplaceOperation(CloudTable table, ITableEntity originalEntity, ITableEntity newEntity)
        {
            if (originalEntity != null)
            {
                if (!string.IsNullOrEmpty(newEntity.ETag))
                    originalEntity.ETag = newEntity.ETag;

                TableOperation replaceOperation = TableOperation.Replace(originalEntity);
                await table.ExecuteAsync(replaceOperation);
            }
        }
    }
}
