using ConnectVeiculos.Core.Entities.Integracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Integracoes;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class IntegracaoLogRepository : IIntegracaoLogRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public IntegracaoLogRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(IntegracaoLog log)
        {
            if (log.IlgCriadoEm == default) log.IlgCriadoEm = DateTime.UtcNow;
            _context.IntegracoesLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<IntegracaoLog>> GetUltimosAsync(int limit = 100)
        {
            return await _context.IntegracoesLogs
                .OrderByDescending(l => l.IlgCriadoEm)
                .Take(Math.Clamp(limit, 1, 1000))
                .ToListAsync();
        }
    }
}
