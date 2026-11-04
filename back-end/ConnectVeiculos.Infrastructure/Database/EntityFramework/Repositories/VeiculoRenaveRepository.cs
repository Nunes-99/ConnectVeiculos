using ConnectVeiculos.Core.Entities.Renave;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Renave;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class VeiculoRenaveRepository : IVeiculoRenaveRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public VeiculoRenaveRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public Task<VeiculoRenave?> GetByVeiculoAsync(int veiculoId)
            => _context.VeiculosRenave.FirstOrDefaultAsync(r => r.R_VeiId == veiculoId)!;

        public async Task<IReadOnlyList<VeiculoRenave>> GetByVeiculosAsync(IEnumerable<int> veiculoIds)
        {
            var ids = veiculoIds.Distinct().ToList();
            if (ids.Count == 0) return Array.Empty<VeiculoRenave>();
            return await _context.VeiculosRenave.AsNoTracking().Where(r => ids.Contains(r.R_VeiId)).ToListAsync();
        }

        public async Task SalvarAsync(VeiculoRenave registro)
        {
            if (registro.RnvId == 0) _context.VeiculosRenave.Add(registro);
            else _context.VeiculosRenave.Update(registro);
            await _context.SaveChangesAsync();
        }
    }
}
