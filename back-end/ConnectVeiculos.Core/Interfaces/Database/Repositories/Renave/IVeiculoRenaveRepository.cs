using ConnectVeiculos.Core.Entities.Renave;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Renave
{
    public interface IVeiculoRenaveRepository
    {
        Task<VeiculoRenave?> GetByVeiculoAsync(int veiculoId);
        Task<IReadOnlyList<VeiculoRenave>> GetByVeiculosAsync(IEnumerable<int> veiculoIds);
        Task SalvarAsync(VeiculoRenave registro);

        /// <summary>Todos os registros da loja (poucos: um por veiculo que passou pelo Renave).</summary>
        Task<IReadOnlyList<VeiculoRenave>> GetTodosAsync();
    }
}
