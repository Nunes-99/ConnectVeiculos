using ConnectVeiculos.Core.Entities.NotasFiscais;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.NotasFiscais;
using ConnectVeiculos.Core.NotasFiscais;
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

        public async Task<IReadOnlyList<NotaFiscalComVeiculo>> ListarAsync(NfeFiltro filtro, int limite)
        {
            var notas = _context.NotasFiscais.AsNoTracking();
            if (filtro.De.HasValue)
            {
                var inicio = filtro.De.Value.Date;
                notas = notas.Where(n => n.NfDtEmissao >= inicio);
            }
            if (filtro.Ate.HasValue)
            {
                var fim = filtro.Ate.Value.Date.AddDays(1);
                notas = notas.Where(n => n.NfDtEmissao < fim);
            }
            if (!string.IsNullOrEmpty(filtro.Tipo)) notas = notas.Where(n => n.NfTipo == filtro.Tipo);
            if (!string.IsNullOrEmpty(filtro.Status)) notas = notas.Where(n => n.NfStatus == filtro.Status);

            // Left join: a nota continua na lista mesmo se o veiculo foi excluido.
            var consulta = from n in notas
                           join v in _context.Veiculos.AsNoTracking() on n.R_VeiId equals v.VeiId into vs
                           from v in vs.DefaultIfEmpty()
                           select new { Nota = n, Marca = v.VeiMarca, Modelo = v.VeiModelo, Ano = (short?)v.VeiAno, Placa = v.VeiPlaca };

            var busca = filtro.Busca?.Trim();
            if (!string.IsNullOrEmpty(busca))
            {
                var padrao = $"%{busca}%";
                var padraoPlaca = $"%{busca.Replace("-", "")}%";
                var numero = int.TryParse(busca, out var nr) ? nr : -1;
                consulta = consulta.Where(x => x.Nota.NfNumero == numero
                    || EF.Functions.Like(x.Nota.NfDestNome ?? "", padrao)
                    || EF.Functions.Like(x.Nota.NfChave ?? "", padrao)
                    || EF.Functions.Like(x.Placa ?? "", padraoPlaca)
                    || EF.Functions.Like((x.Marca ?? "") + " " + (x.Modelo ?? ""), padrao));
            }

            var itens = await consulta
                .OrderByDescending(x => x.Nota.NfDtEmissao).ThenByDescending(x => x.Nota.NfId)
                .Take(limite)
                .ToListAsync();
            return itens.Select(x => new NotaFiscalComVeiculo(x.Nota, x.Marca, x.Modelo, x.Ano, x.Placa)).ToList();
        }

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
