namespace ConnectVeiculos.Core.Interfaces.Database.Common
{
    public interface IUnitOfWork : IDisposable
    {
        void BeginTransaction();
        void ApplyChanges();
        void Commit();
        void Rollback();
    }
}
