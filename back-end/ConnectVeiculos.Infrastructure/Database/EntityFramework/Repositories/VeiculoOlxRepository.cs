using ConnectVeiculos.Core.Entities.Integracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Integracoes;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class VeiculoOlxRepository : IVeiculoOlxRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public VeiculoOlxRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public async Task<VeiculoOlx?> GetByVeiculoIdAsync(int veiculoId) =>
            await _context.VeiculosOlx.FirstOrDefaultAsync(a => a.R_VeiId == veiculoId);

        public async Task<HashSet<int>> VeiculosNoArAsync() =>
            (await _context.VeiculosOlx.AsNoTracking()
                .Where(a => a.VolStatus == VeiculoOlx.StatusAtivo && a.VolSituacao != "refused" && a.VolSituacao != "error")
                .Select(a => a.R_VeiId).ToListAsync())
            .ToHashSet();

        public async Task SalvarAsync(VeiculoOlx anuncio)
        {
            if (anuncio.VolId == 0 && _context.Entry(anuncio).State == EntityState.Detached)
                _context.VeiculosOlx.Add(anuncio);
            await _context.SaveChangesAsync();
        }
    }
}
