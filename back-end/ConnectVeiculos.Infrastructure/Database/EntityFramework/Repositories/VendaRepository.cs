using ConnectVeiculos.Core.Entities.Vendas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class VendaRepository : IVendaRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public VendaRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public async Task<Venda> GetByIdAsync(int id)
        {
            return await _context.Vendas
                .Include(v => v.Veiculo)
                .Include(v => v.Vendedor)
                .FirstOrDefaultAsync(v => v.VenId == id);
        }

        public async Task<IEnumerable<Venda>> GetAllAsync()
        {
            return await _context.Vendas
                .Include(v => v.Veiculo)
                .Include(v => v.Vendedor)
                .ToListAsync();
        }

        public async Task<IEnumerable<Venda>> GetByVendedorIdAsync(int vendedorId)
        {
            return await _context.Vendas
                .Include(v => v.Veiculo)
                .Where(v => v.R_UsuId == vendedorId)
                .ToListAsync();
        }

        public async Task<int> CreateAsync(Venda venda)
        {
            _context.Vendas.Add(venda);
            await _context.SaveChangesAsync();
            return venda.VenId;
        }

        public async Task UpdateAsync(Venda venda)
        {
            _context.Vendas.Update(venda);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var venda = await _context.Vendas.FirstOrDefaultAsync(v => v.VenId == id);
            if (venda != null)
            {
                _context.Vendas.Remove(venda);
                await _context.SaveChangesAsync();
            }
        }
    }
}
