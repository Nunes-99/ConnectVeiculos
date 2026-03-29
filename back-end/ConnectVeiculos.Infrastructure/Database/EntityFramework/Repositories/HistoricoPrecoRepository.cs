using ConnectVeiculos.Core.Entities.HistoricoPrecos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.HistoricoPrecos;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class HistoricoPrecoRepository : IHistoricoPrecoRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public HistoricoPrecoRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<HistoricoPreco>> GetByVeiculoIdAsync(int veiculoId)
        {
            return await _context.HistoricosPreco
                .Where(h => h.R_VeiId == veiculoId)
                .OrderByDescending(h => h.HisDataAlteracao)
                .Include(h => h.Usuario)
                .ToListAsync();
        }

        public async Task<HistoricoPreco> AddAsync(HistoricoPreco historicoPreco)
        {
            await _context.HistoricosPreco.AddAsync(historicoPreco);
            await _context.SaveChangesAsync();
            return historicoPreco;
        }
    }
}
