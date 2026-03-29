using ConnectVeiculos.Core.Entities.Lojas;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas
{
    public interface ILojaRepository
    {
        Task<Loja> GetByIdAsync(int id);
        Task<Loja> GetBySlugAsync(string slug);
        Task<IEnumerable<Loja>> GetAllAsync();
        Task<(IEnumerable<Loja> Items, int Total)> GetPagedAsync(int page, int pageSize, string? search = null);
        Task<int> CreateAsync(Loja loja);
        Task UpdateAsync(Loja loja);
        Task DeleteAsync(int id);
    }
}
