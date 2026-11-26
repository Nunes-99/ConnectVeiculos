using ConnectVeiculos.Core.Entities.Tenants;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Services.ConsultaPlaca
{
    /// <summary>
    /// Creditos no master, em centavos. Cada escrita e' um comando so (o SQLite
    /// executa um de cada vez): duas consultas ao mesmo tempo nao passam do
    /// saldo, e a referencia unica (INSERT OR IGNORE) impede contar duas vezes.
    /// </summary>
    public class CreditosConsultaService : ICreditosConsultaService
    {
        private readonly MasterDbContext _master;

        public CreditosConsultaService(MasterDbContext master)
        {
            _master = master;
        }

        public async Task<decimal> SaldoAsync(int tenantId, CancellationToken ct = default)
        {
            var centavos = await _master.CreditosConsulta.Where(c => c.TenId == tenantId).SumAsync(c => c.CrcCentavos, ct);
            return centavos / 100m;
        }

        public async Task<bool> CreditarCompraAsync(int tenantId, decimal valor, string pagamentoId, CancellationToken ct = default)
        {
            if (valor <= 0) return false;
            return await InserirAsync(tenantId, Centavos(valor), CreditoConsulta.Compra, $"mp:{pagamentoId}",
                "Compra de créditos (Pix)", ct);
        }

        public async Task<bool> DebitarAsync(int tenantId, decimal valor, string referencia, string descricao, CancellationToken ct = default)
        {
            var centavos = Centavos(valor);
            if (centavos <= 0) return true;
            var linhas = await _master.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT OR IGNORE INTO CreditoConsulta (TenId, CrcCentavos, CrcTipo, CrcReferencia, CrcDescricao, CrcCriadoEm)
                SELECT {tenantId}, {-centavos}, {CreditoConsulta.Consumo}, {referencia}, {descricao}, {DateTime.UtcNow}
                WHERE COALESCE((SELECT SUM(CrcCentavos) FROM CreditoConsulta WHERE TenId = {tenantId}), 0) >= {centavos}", ct);
            return linhas > 0;
        }

        public Task<bool> EstornarAsync(int tenantId, decimal valor, string referencia, string descricao, CancellationToken ct = default)
            => InserirAsync(tenantId, Centavos(valor), CreditoConsulta.Estorno, $"estorno:{referencia}", descricao, ct);

        public async Task<IReadOnlyList<MovimentoCredito>> ExtratoAsync(int tenantId, int limite, CancellationToken ct = default)
        {
            var lista = await _master.CreditosConsulta.AsNoTracking()
                .Where(c => c.TenId == tenantId)
                .OrderByDescending(c => c.CrcId)
                .Take(limite)
                .ToListAsync(ct);
            return lista.Select(c => new MovimentoCredito
            {
                Tipo = c.CrcTipo,
                Valor = c.CrcCentavos / 100m,
                Descricao = c.CrcDescricao,
                Data = DateTime.SpecifyKind(c.CrcCriadoEm, DateTimeKind.Utc)
            }).ToList();
        }

        private async Task<bool> InserirAsync(int tenantId, long centavos, string tipo, string referencia, string descricao, CancellationToken ct)
        {
            if (centavos <= 0) return false;
            var linhas = await _master.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT OR IGNORE INTO CreditoConsulta (TenId, CrcCentavos, CrcTipo, CrcReferencia, CrcDescricao, CrcCriadoEm)
                VALUES ({tenantId}, {centavos}, {tipo}, {referencia}, {descricao}, {DateTime.UtcNow})", ct);
            return linhas > 0;
        }

        private static long Centavos(decimal valor) => (long)Math.Round(valor * 100m, MidpointRounding.AwayFromZero);
    }
}
