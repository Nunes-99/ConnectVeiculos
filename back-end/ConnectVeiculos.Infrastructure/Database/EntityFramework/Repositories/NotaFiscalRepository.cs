using ConnectVeiculos.Core.Entities.NotasFiscais;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.NotasFiscais;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Database.EntityFramework.Repositories
{
    public class NotaFiscalRepository : INotaFiscalRepository
    {
        private readonly ConnectVeiculosDbContext _context;

        public NotaFiscalRepository(ConnectVeiculosDbContext context)
        {
            _context = context;
        }

        public Task<NotaFiscal?> GetByIdAsync(int id)
            => _context.NotasFiscais.FirstOrDefaultAsync(n => n.NfId == id)!;

        public async Task<IReadOnlyList<NotaFiscal>> GetByVeiculoAsync(int veiculoId)
            => await _context.NotasFiscais.Where(n => n.R_VeiId == veiculoId).ToListAsync();

        public Task<bool> NumeroEmUsoAsync(string ambiente, int serie, int numero)
            => _context.NotasFiscais.AnyAsync(n => n.NfAmbiente == ambiente && n.NfSerie == serie && n.NfNumero == numero);

        public async Task SalvarAsync(NotaFiscal nota)
        {
            if (nota.NfId == 0) _context.NotasFiscais.Add(nota);
            else if (_context.Entry(nota).State == EntityState.Detached) _context.NotasFiscais.Update(nota);
            await _context.SaveChangesAsync();
        }
    }
}
