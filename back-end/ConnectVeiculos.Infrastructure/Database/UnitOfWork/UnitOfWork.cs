using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ConnectVeiculos.Infrastructure.Database.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly DbSession _dbSession;
        private readonly ConnectVeiculosDbContext _dbContext;
        private IDbContextTransaction? _efTransaction;
        private bool _disposed = false;

        public UnitOfWork(DbSession dbSession, ConnectVeiculosDbContext dbContext)
        {
            _dbSession = dbSession;
            _dbContext = dbContext;
        }

        public void BeginTransaction()
        {
            if (_efTransaction == null)
            {
                _efTransaction = _dbContext.Database.BeginTransaction();
            }
        }

        public void ApplyChanges()
        {
            _dbContext.SaveChanges();
        }

        public void Commit()
        {
            try
            {
                _dbContext.SaveChanges();
                _efTransaction?.Commit();
            }
            catch (Exception)
            {
                Rollback();
                throw;
            }
            finally
            {
                _efTransaction?.Dispose();
                _efTransaction = null;
            }
        }

        public void Rollback()
        {
            _efTransaction?.Rollback();
            _efTransaction?.Dispose();
            _efTransaction = null;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _efTransaction?.Dispose();
                    _dbSession.Dispose();
                    _dbContext.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
