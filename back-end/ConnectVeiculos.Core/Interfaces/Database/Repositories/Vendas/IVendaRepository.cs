using ConnectVeiculos.Core.Entities.Vendas;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas
{
    public interface IVendaRepository
    {
        Task<Venda> GetByIdAsync(int id);
        Task<IEnumerable<Venda>> GetAllAsync();
        Task<IEnumerable<Venda>> GetByVendedorIdAsync(int vendedorId);
        Task<IEnumerable<Venda>> GetByVeiculoIdAsync(int veiculoId);
        Task<int> CreateAsync(Venda venda);
        Task UpdateAsync(Venda venda);
        Task DeleteAsync(int id);
    }
}
