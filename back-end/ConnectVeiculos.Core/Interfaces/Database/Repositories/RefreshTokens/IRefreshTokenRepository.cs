using ConnectVeiculos.Core.Entities.RefreshTokens;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.RefreshTokens
{
    public interface IRefreshTokenRepository
    {
        Task<RefreshToken> GetByTokenAsync(string token);
        Task<IEnumerable<RefreshToken>> GetByUsuarioIdAsync(int usuarioId);
        Task<IEnumerable<RefreshToken>> GetExpiredTokensAsync();
        Task<RefreshToken> AddAsync(RefreshToken refreshToken);
        Task UpdateAsync(RefreshToken refreshToken);
        Task DeleteAsync(int id);
        Task RevokeAllByUsuarioIdAsync(int usuarioId);
        Task DeleteExpiredTokensAsync();
    }
}
