namespace ConnectVeiculos.Core.Interfaces.Database.Common
{
    public interface IUnitOfWorkFactory
    {
        IUnitOfWork Create();
    }
}
