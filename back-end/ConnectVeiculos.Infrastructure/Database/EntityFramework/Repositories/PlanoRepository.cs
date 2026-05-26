using ConnectVeiculos.Core.Entities.Tenants;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Tenants;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class PlanoRepository : IPlanoRepository
    {
        private readonly MasterDbContext _master;

        public PlanoRepository(MasterDbContext master)
        {
            _master = master;
        }

        public Task<Plano?> GetByIdAsync(int planoId, CancellationToken ct = default)
            => _master.Planos.AsNoTracking().FirstOrDefaultAsync(p => p.PlaId == planoId, ct);

        public async Task<IReadOnlyList<Plano>> ListarAtivosAsync(CancellationToken ct = default)
        {
            return await _master.Planos.AsNoTracking()
                .Where(p => p.PlaAtivo)
                .OrderBy(p => p.PlaOrdem)
                .ToListAsync(ct);
        }
    }
}
