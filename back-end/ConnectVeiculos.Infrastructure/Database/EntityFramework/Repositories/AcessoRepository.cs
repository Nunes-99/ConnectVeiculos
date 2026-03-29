using ConnectVeiculos.Core.Entities.Acessos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Acessos;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class AcessoRepository : IAcessoRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public AcessoRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public async Task<Acesso> GetByIdAsync(int id)
        {
            return await _context.Acessos.FirstOrDefaultAsync(a => a.AcsId == id);
        }

        public async Task<IEnumerable<Acesso>> GetAllAsync()
        {
            return await _context.Acessos.ToListAsync();
        }

        public async Task<(IEnumerable<Acesso> Items, int Total)> GetPagedAsync(int page, int pageSize, string? search = null)
        {
            var query = _context.Acessos.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(a => a.AcsNome.ToLower().Contains(search));
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderBy(a => a.AcsNome)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<int> CreateAsync(Acesso acesso)
        {
            _context.Acessos.Add(acesso);
            await _context.SaveChangesAsync();
            return acesso.AcsId;
        }

        public async Task UpdateAsync(Acesso acesso)
        {
            _context.Acessos.Update(acesso);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var acesso = await GetByIdAsync(id);
            if (acesso != null)
            {
                _context.Acessos.Remove(acesso);
                await _context.SaveChangesAsync();
            }
        }
    }
}
