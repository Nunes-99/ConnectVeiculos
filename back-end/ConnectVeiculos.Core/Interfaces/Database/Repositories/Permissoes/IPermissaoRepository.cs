using ConnectVeiculos.Core.Entities.Permissoes;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Permissoes
{
    public interface IPermissaoRepository
    {
        Task<Permissao> GetByIdAsync(int id);
        Task<IEnumerable<Permissao>> GetAllAsync();
        Task<IEnumerable<Permissao>> GetByUsuarioIdAsync(int usuarioId);
        Task<IEnumerable<Permissao>> GetByUsuarioIdsAsync(IEnumerable<int> usuarioIds);
        Task<int> CreateAsync(Permissao permissao);
        Task UpdateAsync(Permissao permissao);
        Task DeleteAsync(int id);
    }
}
