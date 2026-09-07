using ConnectVeiculos.Core.Catalogo;
using ConnectVeiculos.Core.Interfaces.Email;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using ConnectVeiculos.Core.Entities.Lojas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Services.Notificacao
{
    public class FavoritoNotificacaoService : IFavoritoNotificacaoService
    {
        private readonly ConnectVeiculosDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ITenantContext _tenantContext;
        private readonly ILogger<FavoritoNotificacaoService> _logger;

        public FavoritoNotificacaoService(
            ConnectVeiculosDbContext context,
            IEmailService emailService,
            ITenantContext tenantContext,
            ILogger<FavoritoNotificacaoService> logger)
        {
            _context = context;
            _emailService = emailService;
            _tenantContext = tenantContext;
            _logger = logger;
        }

        /// <summary>
        /// Monta o link publico do veiculo. A rota Angular e'
        /// /catalogo/{tenantSlug}/veiculo/{id} — sem o slug do TENANT (nao o da
        /// loja) o router cai no wildcard e joga o visitante na landing page.
        /// </summary>
        private string MontarLinkVeiculo(Loja loja, int veiculoId)
        {
            var baseUrl = loja?.LojUrlCatalogo ?? "https://connectveiculos.dev.br";
            var tenantSlug = _tenantContext.IsResolved ? _tenantContext.TenantSlug : null;

            return CatalogoUrl.Veiculo(baseUrl, tenantSlug, veiculoId);
        }

        public async Task NotificarPrecoAlteradoAsync(int veiculoId, decimal precoAntigo, decimal precoNovo)
        {
            // So notifica se houve QUEDA real (>= 1% e >= R$ 100)
            if (precoNovo >= precoAntigo) return;
            var queda = precoAntigo - precoNovo;
            if (queda < 100m) return;
            if (precoAntigo > 0 && (queda / precoAntigo) < 0.01m) return;

            try
            {
                var veiculo = await _context.Veiculos.AsNoTracking()
                    .FirstOrDefaultAsync(v => v.VeiId == veiculoId);
                if (veiculo == null) return;

                var loja = await _context.Lojas.AsNoTracking()
                    .FirstOrDefaultAsync(l => l.LojId == veiculo.R_LojId);

                var favoritos = await _context.Favoritos.AsNoTracking()
                    .Where(f => f.R_VeiId == veiculoId)
                    .ToListAsync();

                var descricao = $"{veiculo.VeiMarca} {veiculo.VeiModelo} {veiculo.VeiAno}";
                var link = MontarLinkVeiculo(loja, veiculoId);

                foreach (var f in favoritos)
                {
                    if (string.IsNullOrWhiteSpace(f.FavEmail)) continue;
                    await _emailService.SendPrecoAlteradoAsync(f.FavEmail, f.FavNome ?? "", descricao, precoAntigo, precoNovo, link);
                }

                _logger.LogInformation("Notificacao de queda de preco enviada para {Count} favoritos do veiculo {VeiculoId}", favoritos.Count, veiculoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao notificar queda de preco do veiculo {VeiculoId}", veiculoId);
            }
        }

        public async Task NotificarVeiculoSimilarAsync(int veiculoId)
        {
            try
            {
                var veiculo = await _context.Veiculos.AsNoTracking()
                    .FirstOrDefaultAsync(v => v.VeiId == veiculoId);
                if (veiculo == null) return;

                // "Similar": mesma marca, mesma categoria, faixa de preco +-20%
                var precoMin = veiculo.VeiPreco * 0.8m;
                var precoMax = veiculo.VeiPreco * 1.2m;

                var idsVeiculosSimilares = await _context.Veiculos.AsNoTracking()
                    .Where(v => v.VeiId != veiculoId
                             && v.VeiMarca == veiculo.VeiMarca
                             && v.R_CatId == veiculo.R_CatId
                             && v.VeiPreco >= precoMin && v.VeiPreco <= precoMax)
                    .Select(v => v.VeiId)
                    .ToListAsync();

                if (!idsVeiculosSimilares.Any()) return;

                // Pega e-mails unicos que favoritaram alguns dos similares
                var emailsParaNotificar = await _context.Favoritos.AsNoTracking()
                    .Where(f => idsVeiculosSimilares.Contains(f.R_VeiId))
                    .GroupBy(f => f.FavEmail)
                    .Select(g => new { Email = g.Key, Nome = g.First().FavNome })
                    .ToListAsync();

                if (!emailsParaNotificar.Any()) return;

                var loja = await _context.Lojas.AsNoTracking()
                    .FirstOrDefaultAsync(l => l.LojId == veiculo.R_LojId);
                var descricao = $"{veiculo.VeiMarca} {veiculo.VeiModelo} {veiculo.VeiAno}";
                var link = MontarLinkVeiculo(loja, veiculoId);

                foreach (var dest in emailsParaNotificar)
                {
                    if (string.IsNullOrWhiteSpace(dest.Email)) continue;
                    await _emailService.SendVeiculoSimilarAsync(dest.Email, dest.Nome ?? "", descricao, veiculo.VeiPreco, link);
                }

                _logger.LogInformation("Notificacao de veiculo similar enviada para {Count} interessados (veiculo {VeiculoId})", emailsParaNotificar.Count, veiculoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao notificar veiculo similar {VeiculoId}", veiculoId);
            }
        }
    }
}
