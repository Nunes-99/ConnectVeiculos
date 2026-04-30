using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class LojaRepository : ILojaRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public LojaRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public async Task<Loja> GetByIdAsync(int id)
        {
            return await _context.Lojas.FirstOrDefaultAsync(l => l.LojId == id);
        }

        public async Task<Loja> GetBySlugAsync(string slug)
        {
            return await _context.Lojas.FirstOrDefaultAsync(l => l.LojSlug == slug);
        }

        public async Task<IEnumerable<Loja>> GetAllAsync()
        {
            return await _context.Lojas.ToListAsync();
        }

        public async Task<(IEnumerable<Loja> Items, int Total)> GetPagedAsync(int page, int pageSize, string? search = null)
        {
            var query = _context.Lojas.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(l => l.LojNome.ToLower().Contains(search) || l.LojCidade.ToLower().Contains(search));
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderBy(l => l.LojNome)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<int> CreateAsync(Loja loja)
        {
            _context.Lojas.Add(loja);
            await _context.SaveChangesAsync();
            return loja.LojId;
        }

        public async Task UpdateAsync(Loja loja)
        {
            _context.Lojas.Update(loja);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var loja = await _context.Lojas.FindAsync(id);
            if (loja != null)
            {
                _context.Lojas.Remove(loja);
                await _context.SaveChangesAsync();
            }
        }
    }
}
