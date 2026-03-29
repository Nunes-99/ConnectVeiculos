using ConnectVeiculos.Core.Entities.HistoricoPrecos;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.HistoricoPrecos
{
    public interface IHistoricoPrecoRepository
    {
        Task<IEnumerable<HistoricoPreco>> GetByVeiculoIdAsync(int veiculoId);
        Task<HistoricoPreco> AddAsync(HistoricoPreco historicoPreco);
    }
}
