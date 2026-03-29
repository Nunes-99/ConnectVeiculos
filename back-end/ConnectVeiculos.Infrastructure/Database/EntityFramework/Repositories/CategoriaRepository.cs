using ConnectVeiculos.Core.Entities.Categorias;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Categorias;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class CategoriaRepository : ICategoriaRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public CategoriaRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public async Task<Categoria> GetByIdAsync(int id)
        {
            return await _context.Categorias.FirstOrDefaultAsync(c => c.CatId == id);
        }

        public async Task<IEnumerable<Categoria>> GetAllAsync()
        {
            return await _context.Categorias.ToListAsync();
        }

        public async Task<(IEnumerable<Categoria> Items, int Total)> GetPagedAsync(int page, int pageSize, string? search = null)
        {
            var query = _context.Categorias.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(c => c.CatNome.ToLower().Contains(search));
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderBy(c => c.CatNome)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<int> CreateAsync(Categoria categoria)
        {
            _context.Categorias.Add(categoria);
            await _context.SaveChangesAsync();
            return categoria.CatId;
        }

        public async Task UpdateAsync(Categoria categoria)
        {
            _context.Categorias.Update(categoria);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var categoria = await GetByIdAsync(id);
            if (categoria != null)
            {
                _context.Categorias.Remove(categoria);
                await _context.SaveChangesAsync();
            }
        }
    }
}
