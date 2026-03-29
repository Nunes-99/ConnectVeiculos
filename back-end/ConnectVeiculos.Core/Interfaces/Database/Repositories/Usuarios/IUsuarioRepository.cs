using ConnectVeiculos.Core.Entities.Usuarios;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios
{
    public interface IUsuarioRepository
    {
        Task<Usuario> GetByIdAsync(int id);
        Task<Usuario> GetByEmailAsync(string email);
        Task<IEnumerable<Usuario>> GetAllAsync();
        Task<(IEnumerable<Usuario> Items, int Total)> GetPagedAsync(int page, int pageSize, string? search = null);
        Task<int> CreateAsync(Usuario usuario);
        Task UpdateAsync(Usuario usuario);
        Task DeleteAsync(int id);
    }
}
