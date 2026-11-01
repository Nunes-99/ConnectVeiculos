using ConnectVeiculos.Core.Entities.Financiamentos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Financiamentos;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class FinanciamentoLojaRepository : IFinanciamentoLojaRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public FinanciamentoLojaRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<BancoFinanciamentoLoja>> ListarBancosAsync()
            => await _context.BancosFinanciamentoLoja.OrderBy(b => b.BflNome).ToListAsync();

        public Task<BancoFinanciamentoLoja?> ObterBancoAsync(int id)
            => _context.BancosFinanciamentoLoja.FirstOrDefaultAsync(b => b.BflId == id)!;

        public async Task SalvarBancoAsync(BancoFinanciamentoLoja banco)
        {
            if (banco.BflId == 0) _context.BancosFinanciamentoLoja.Add(banco);
            else _context.BancosFinanciamentoLoja.Update(banco);
            await _context.SaveChangesAsync();
        }

        public async Task RemoverBancoAsync(BancoFinanciamentoLoja banco)
        {
            _context.BancosFinanciamentoLoja.Remove(banco);
            await _context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<FichaFinanciamento>> ListarFichasAsync(int? vendedorId)
        {
            var q = _context.FichasFinanciamento.AsNoTracking();
            if (vendedorId.HasValue) q = q.Where(f => f.R_UsuId == vendedorId.Value);
            return await q.OrderByDescending(f => f.FifId).Take(500).ToListAsync();
        }

        public Task<FichaFinanciamento?> ObterFichaAsync(int id)
            => _context.FichasFinanciamento.FirstOrDefaultAsync(f => f.FifId == id)!;

        public async Task SalvarFichaAsync(FichaFinanciamento ficha)
        {
            if (ficha.FifId == 0) _context.FichasFinanciamento.Add(ficha);
            else _context.FichasFinanciamento.Update(ficha);
            await _context.SaveChangesAsync();
        }
    }
}
