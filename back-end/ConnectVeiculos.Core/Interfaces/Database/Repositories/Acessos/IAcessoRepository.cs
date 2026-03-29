using ConnectVeiculos.Core.Entities.Acessos;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Acessos
{
    public interface IAcessoRepository
    {
        Task<Acesso> GetByIdAsync(int id);
        Task<IEnumerable<Acesso>> GetAllAsync();
        Task<(IEnumerable<Acesso> Items, int Total)> GetPagedAsync(int page, int pageSize, string? search = null);
        Task<int> CreateAsync(Acesso acesso);
        Task UpdateAsync(Acesso acesso);
        Task DeleteAsync(int id);
    }
}
