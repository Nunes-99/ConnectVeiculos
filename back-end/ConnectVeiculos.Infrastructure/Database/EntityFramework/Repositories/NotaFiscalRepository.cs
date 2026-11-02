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

        // Numero inutilizado na Sefaz tambem conta como usado.
        public async Task<bool> NumeroEmUsoAsync(string ambiente, int serie, int numero)
            => await _context.NotasFiscais.AnyAsync(n => n.NfAmbiente == ambiente && n.NfSerie == serie && n.NfNumero == numero)
               || await _context.NotasFiscaisInutilizacoes.AnyAsync(i => i.NfiAmbiente == ambiente && i.NfiSerie == serie
                   && numero >= i.NfiNumeroInicial && numero <= i.NfiNumeroFinal);

        public async Task<IReadOnlyList<NotaFiscal>> GetNoIntervaloAsync(string ambiente, int serie, int inicial, int final)
            => await _context.NotasFiscais
                .Where(n => n.NfAmbiente == ambiente && n.NfSerie == serie && n.NfNumero >= inicial && n.NfNumero <= final)
                .ToListAsync();

        public async Task<IReadOnlyList<NotaFiscalInutilizacao>> ListarInutilizacoesAsync()
            => await _context.NotasFiscaisInutilizacoes.AsNoTracking().OrderByDescending(i => i.NfiId).Take(200).ToListAsync();

        public Task<NotaFiscalInutilizacao?> ObterInutilizacaoAsync(int id)
            => _context.NotasFiscaisInutilizacoes.FirstOrDefaultAsync(i => i.NfiId == id)!;

        public async Task SalvarInutilizacaoAsync(NotaFiscalInutilizacao inutilizacao)
        {
            if (inutilizacao.NfiId == 0) _context.NotasFiscaisInutilizacoes.Add(inutilizacao);
            await _context.SaveChangesAsync();
        }

        public async Task SalvarAsync(NotaFiscal nota)
        {
            if (nota.NfId == 0) _context.NotasFiscais.Add(nota);
            else if (_context.Entry(nota).State == EntityState.Detached) _context.NotasFiscais.Update(nota);
            await _context.SaveChangesAsync();
        }
    }
}
