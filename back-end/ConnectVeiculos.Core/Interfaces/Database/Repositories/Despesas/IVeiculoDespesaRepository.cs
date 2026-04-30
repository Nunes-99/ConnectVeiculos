using ConnectVeiculos.Core.Entities.Despesas;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Despesas
{
    public interface IVeiculoDespesaRepository
    {
        Task<IEnumerable<VeiculoDespesa>> GetAllAsync();
        Task<IEnumerable<VeiculoDespesa>> GetByVeiculoIdAsync(int veiculoId);
    }
}
