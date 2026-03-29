using ConnectVeiculos.Core.Entities.RefreshTokens;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.RefreshTokens;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public RefreshTokenRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public async Task<RefreshToken> GetByTokenAsync(string token)
        {
            return await _context.RefreshTokens
                .Include(r => r.Usuario)
                .FirstOrDefaultAsync(r => r.RefToken == token);
        }

        public async Task<IEnumerable<RefreshToken>> GetByUsuarioIdAsync(int usuarioId)
        {
            return await _context.RefreshTokens
                .Where(r => r.R_UsuId == usuarioId)
                .OrderByDescending(r => r.RefCriadoEm)
                .ToListAsync();
        }

        public async Task<IEnumerable<RefreshToken>> GetExpiredTokensAsync()
        {
            return await _context.RefreshTokens
                .Where(r => r.RefExpiraEm < DateTime.UtcNow)
                .ToListAsync();
        }

        public async Task<RefreshToken> AddAsync(RefreshToken refreshToken)
        {
            await _context.RefreshTokens.AddAsync(refreshToken);
            await _context.SaveChangesAsync();
            return refreshToken;
        }

        public async Task UpdateAsync(RefreshToken refreshToken)
        {
            _context.RefreshTokens.Update(refreshToken);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var token = await _context.RefreshTokens.FindAsync(id);
            if (token != null)
            {
                _context.RefreshTokens.Remove(token);
                await _context.SaveChangesAsync();
            }
        }

        public async Task RevokeAllByUsuarioIdAsync(int usuarioId)
        {
            var tokens = await _context.RefreshTokens
                .Where(r => r.R_UsuId == usuarioId && !r.RefRevogado)
                .ToListAsync();

            foreach (var token in tokens)
            {
                token.Revogar();
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteExpiredTokensAsync()
        {
            var tokensExpirados = await _context.RefreshTokens
                .Where(r => r.RefExpiraEm < DateTime.UtcNow)
                .ToListAsync();

            _context.RefreshTokens.RemoveRange(tokensExpirados);
            await _context.SaveChangesAsync();
        }
    }
}
