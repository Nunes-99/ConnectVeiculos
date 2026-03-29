using ConnectVeiculos.Core.Entities.Permissoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Permissoes;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class PermissaoRepository : IPermissaoRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public PermissaoRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public async Task<Permissao> GetByIdAsync(int id)
        {
            return await _context.Permissoes
                .Include(p => p.Usuario)
                .Include(p => p.Acesso)
                .FirstOrDefaultAsync(p => p.UsuAcsId == id);
        }

        public async Task<IEnumerable<Permissao>> GetAllAsync()
        {
            return await _context.Permissoes
                .Include(p => p.Usuario)
                .Include(p => p.Acesso)
                .ToListAsync();
        }

        public async Task<IEnumerable<Permissao>> GetByUsuarioIdAsync(int usuarioId)
        {
            return await _context.Permissoes
                .Include(p => p.Acesso)
                .Where(p => p.R_UsuId == usuarioId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Permissao>> GetByUsuarioIdsAsync(IEnumerable<int> usuarioIds)
        {
            return await _context.Permissoes
                .Include(p => p.Acesso)
                .Where(p => usuarioIds.Contains(p.R_UsuId))
                .ToListAsync();
        }

        public async Task<int> CreateAsync(Permissao permissao)
        {
            _context.Permissoes.Add(permissao);
            await _context.SaveChangesAsync();
            return permissao.UsuAcsId;
        }

        public async Task UpdateAsync(Permissao permissao)
        {
            _context.Permissoes.Update(permissao);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var permissao = await _context.Permissoes.FirstOrDefaultAsync(p => p.UsuAcsId == id);
            if (permissao != null)
            {
                _context.Permissoes.Remove(permissao);
                await _context.SaveChangesAsync();
            }
        }
    }
}
