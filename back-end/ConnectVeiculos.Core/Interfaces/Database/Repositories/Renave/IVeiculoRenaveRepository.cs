using ConnectVeiculos.Core.Entities.Renave;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Renave
{
    public interface IVeiculoRenaveRepository
    {
        Task<VeiculoRenave?> GetByVeiculoAsync(int veiculoId);
        Task<IReadOnlyList<VeiculoRenave>> GetByVeiculosAsync(IEnumerable<int> veiculoIds);
        Task SalvarAsync(VeiculoRenave registro);
    }
}
