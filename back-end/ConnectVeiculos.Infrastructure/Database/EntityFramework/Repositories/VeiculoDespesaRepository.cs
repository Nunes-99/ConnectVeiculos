using ConnectVeiculos.Core.Entities.Despesas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Despesas;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class VeiculoDespesaRepository : IVeiculoDespesaRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public VeiculoDespesaRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<VeiculoDespesa>> GetAllAsync()
        {
            return await _context.VeiculosDespesas.AsNoTracking().ToListAsync();
        }

        public async Task<IEnumerable<VeiculoDespesa>> GetByVeiculoIdAsync(int veiculoId)
        {
            return await _context.VeiculosDespesas.AsNoTracking()
                .Where(d => d.R_VeiId == veiculoId)
                .ToListAsync();
        }
    }
}
