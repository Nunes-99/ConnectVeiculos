using ConnectVeiculos.Core.Entities.Integracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Integracoes;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class VeiculoNapistaRepository : IVeiculoNapistaRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public VeiculoNapistaRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public async Task<VeiculoNapista?> GetByVeiculoIdAsync(int veiculoId) =>
            await _context.VeiculosNapista.FirstOrDefaultAsync(a => a.R_VeiId == veiculoId);

        public async Task<Dictionary<string, int>> VeiculosPorAnuncioAsync(IReadOnlyCollection<string> anuncioIds)
        {
            if (anuncioIds.Count == 0) return new();
            return (await _context.VeiculosNapista.AsNoTracking()
                    .Where(a => anuncioIds.Contains(a.VnpAnuncioId))
                    .Select(a => new { a.VnpAnuncioId, a.R_VeiId }).ToListAsync())
                .GroupBy(a => a.VnpAnuncioId).ToDictionary(g => g.Key, g => g.First().R_VeiId);
        }

        public async Task SalvarAsync(VeiculoNapista anuncio)
        {
            if (anuncio.VnpId == 0 && _context.Entry(anuncio).State == EntityState.Detached)
                _context.VeiculosNapista.Add(anuncio);
            await _context.SaveChangesAsync();
        }
    }
}
