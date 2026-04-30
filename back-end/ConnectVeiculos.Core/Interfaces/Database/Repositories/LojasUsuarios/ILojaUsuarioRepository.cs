using ConnectVeiculos.Core.Entities.LojasUsuarios;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.LojasUsuarios
{
    public interface ILojaUsuarioRepository
    {
        Task<LojaUsuario> GetByUsuarioIdAsync(int usuarioId);
        Task<IEnumerable<LojaUsuario>> GetAllByUsuarioIdAsync(int usuarioId);
        Task<IEnumerable<LojaUsuario>> GetByUsuarioIdsAsync(IEnumerable<int> usuarioIds);
        Task<int> CreateAsync(LojaUsuario lojaUsuario);
        Task UpdateAsync(LojaUsuario lojaUsuario);
        Task DeleteByUsuarioIdAsync(int usuarioId);
        Task DeleteByLojaIdAsync(int lojaId);
    }
}
