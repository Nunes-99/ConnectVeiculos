using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Jobs
{
    /// <summary>
    /// Loja com dominio proprio ativo entra sozinha no Search Console da
    /// plataforma, sem o passo manual da tag HTML. Em duas passadas:
    ///   1. pede ao Google o codigo de verificacao do dominio e guarda no tenant
    ///      (o SSR passa a servi-lo no &lt;head&gt; do dominio);
    ///   2. quando o codigo ja aparece no site (caches de ate ~10 min), verifica
    ///      a propriedade, adiciona ao Search Console e envia o sitemap.
    /// A propriedade fica na conta Google da plataforma; a loja pode ser
    /// adicionada como usuaria dela depois, se quiser ver os relatorios.
    /// </summary>
    public class SearchConsoleDominiosJob : IBackgroundJob
    {
        private readonly ITenantStore _store;
        private readonly ISearchConsoleService _searchConsole;
        private readonly ILogger<SearchConsoleDominiosJob> _logger;

        public string JobName => "SearchConsoleDominios";
        public string CronExpression => "*/10 * * * *";

        public SearchConsoleDominiosJob(ITenantStore store, ISearchConsoleService searchConsole, ILogger<SearchConsoleDominiosJob> logger)
        {
            _store = store;
            _searchConsole = searchConsole;
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            if (!_searchConsole.CadastroDeDominiosLigado) return;

            var pendentes = (await _store.ListActiveAsync())
                .Where(t => t.DominioProprioAtivo() && t.TenSearchConsoleEm == null)
                .ToList();

            foreach (var tenant in pendentes)
            {
                var siteUrl = $"https://{tenant.TenDominioProprio}/";

                if (string.IsNullOrEmpty(tenant.TenGoogleVerifAuto))
                {
                    var codigo = await _searchConsole.PedirCodigoVerificacaoAsync(siteUrl);
                    if (codigo == null) continue;
                    await _store.AtualizarAsync(tenant.TenId, t => t.RegistrarCodigoSearchConsole(codigo));
                    _logger.LogInformation("{Job}: codigo de verificacao de {Site} recebido; verifica na proxima passada", JobName, siteUrl);
                    continue;
                }

                if (!await _searchConsole.CodigoNoArAsync(siteUrl, tenant.TenGoogleVerifAuto)) continue;

                if (await _searchConsole.CadastrarSiteAsync(siteUrl))
                    await _store.AtualizarAsync(tenant.TenId, t => t.RegistrarNoSearchConsole());
            }
        }
    }
}
