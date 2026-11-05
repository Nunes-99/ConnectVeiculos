using ConnectVeiculos.Core.Entities.Integracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Integracoes;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class VeiculoWebmotorsRepository : IVeiculoWebmotorsRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public VeiculoWebmotorsRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public async Task<VeiculoWebmotors?> GetByVeiculoIdAsync(int veiculoId) =>
            await _context.VeiculosWebmotors.FirstOrDefaultAsync(a => a.R_VeiId == veiculoId);

        public async Task<IReadOnlyList<VeiculoWebmotors>> ListarPublicadosAsync() =>
            await _context.VeiculosWebmotors.Where(a => a.VwmStatus == VeiculoWebmotors.StatusAtivo).ToListAsync();

        public async Task SalvarAsync(VeiculoWebmotors anuncio)
        {
            if (anuncio.VwmId == 0 && _context.Entry(anuncio).State == EntityState.Detached)
                _context.VeiculosWebmotors.Add(anuncio);
            await _context.SaveChangesAsync();
        }
    }
}
