using Microsoft.Data.Sqlite;
using Npgsql;
using System.Data;
using System.Data.Common;

namespace ConnectVeiculos.Infrastructure.Database.UnitOfWork
{
    public sealed class DbSession : IDisposable
    {
        public DbConnection Connection { get; }
        public IDbTransaction Transaction { get; set; }
        public bool IsPostgres { get; }

        public DbSession(string connectionString, bool usePostgres = false)
        {
            IsPostgres = usePostgres;
            Connection = usePostgres
                ? new NpgsqlConnection(connectionString)
                : new SqliteConnection(connectionString);
            Connection.Open();
        }

        public void BeginTransaction()
        {
            if (Transaction == null)
                Transaction = Connection.BeginTransaction();
        }

        public void CommitTransaction()
        {
            Transaction?.Commit();
            DisposeTransaction();
        }

        public void RollbackTransaction()
        {
            Transaction?.Rollback();
            DisposeTransaction();
        }

        private void DisposeTransaction()
        {
            Transaction?.Dispose();
            Transaction = null;
        }

        public void Dispose()
        {
            DisposeTransaction();
            Connection?.Dispose();
        }
    }
}
