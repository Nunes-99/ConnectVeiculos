using ConnectVeiculos.Core.Entities.LojasUsuarios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.LojasUsuarios;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class LojaUsuarioRepository : ILojaUsuarioRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public LojaUsuarioRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public async Task<LojaUsuario> GetByUsuarioIdAsync(int usuarioId)
        {
            return await _context.LojasUsuarios
                .Include(lu => lu.Loja)
                .FirstOrDefaultAsync(lu => lu.R_UsuId == usuarioId);
        }

        public async Task<IEnumerable<LojaUsuario>> GetByUsuarioIdsAsync(IEnumerable<int> usuarioIds)
        {
            return await _context.LojasUsuarios
                .Include(lu => lu.Loja)
                .Where(lu => usuarioIds.Contains(lu.R_UsuId))
                .ToListAsync();
        }

        public async Task<int> CreateAsync(LojaUsuario lojaUsuario)
        {
            _context.LojasUsuarios.Add(lojaUsuario);
            await _context.SaveChangesAsync();
            return lojaUsuario.LojUsuId;
        }

        public async Task UpdateAsync(LojaUsuario lojaUsuario)
        {
            _context.LojasUsuarios.Update(lojaUsuario);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<LojaUsuario>> GetAllByUsuarioIdAsync(int usuarioId)
        {
            return await _context.LojasUsuarios
                .Include(lu => lu.Loja)
                .Where(lu => lu.R_UsuId == usuarioId)
                .ToListAsync();
        }

        public async Task DeleteByUsuarioIdAsync(int usuarioId)
        {
            var registros = await _context.LojasUsuarios
                .Where(lu => lu.R_UsuId == usuarioId)
                .ToListAsync();
            _context.LojasUsuarios.RemoveRange(registros);
            await _context.SaveChangesAsync();
        }
    }
}
