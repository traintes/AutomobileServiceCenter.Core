using ASC.DataAccess.Interfaces;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ASC.DataAccess
{
    public class UnitOfWork : IUnitOfWork
    {
        private bool disposed;
        private bool complete;
        private Dictionary<string, object> _repositories;
        public Queue<Task<Action>> RollbackActions { get; set; }

        public string ConnectionString { get; set; }

        public UnitOfWork(string connectionString)
        {
            this.ConnectionString = connectionString;
            this.RollbackActions = new Queue<Task<Action>>();
        }

        public void CommitTransaction()
        {
            this.complete = true;
        }

        ~UnitOfWork()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    if (!this.complete)
                        RollbackTransaction();
                }
                finally
                {
                    this.RollbackActions.Clear();
                }
            }
            this.complete = false;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void RollbackTransaction()
        {
            while(this.RollbackActions.Count > 0)
            {
                Task<Action> undoAction = this.RollbackActions.Dequeue();
                undoAction.Result();
            }
        }

        public IRepository<T> Repository<T>() where T : TableEntity
        {
            if (this._repositories == null)
                this._repositories = new Dictionary<string, object>();

            string type = typeof(T).Name;

            if (this._repositories.ContainsKey(type))
                return (IRepository<T>)this._repositories[type];

            var repositoryType = typeof(Repository<>);
            var repositoryInstance = Activator
                .CreateInstance(repositoryType.MakeGenericType(typeof(T)), this);

            this._repositories.Add(type, repositoryInstance);
            return (IRepository<T>)this._repositories[type];
        }
    }
}
