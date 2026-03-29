using ConnectVeiculos.Core.Entities.Categorias;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Categorias
{
    public interface ICategoriaRepository
    {
        Task<Categoria> GetByIdAsync(int id);
        Task<IEnumerable<Categoria>> GetAllAsync();
        Task<(IEnumerable<Categoria> Items, int Total)> GetPagedAsync(int page, int pageSize, string? search = null);
        Task<int> CreateAsync(Categoria categoria);
        Task UpdateAsync(Categoria categoria);
        Task DeleteAsync(int id);
    }
}
