using ConnectVeiculos.Core.Interfaces.Database.Repositories.Integracoes;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Services.MercadoLivre
{
    /// <inheritdoc cref="IMercadoLivreWebhookRouter"/>
    public sealed class MercadoLivreWebhookRouter : IMercadoLivreWebhookRouter
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ITenantStore _tenantStore;
        private readonly ILogger<MercadoLivreWebhookRouter> _logger;

        // seller id -> slug do tenant. Evita varrer todos os bancos a cada
        // notificacao: o ML dispara varias em rajada (uma por item alterado).
        // Só cresce, e uma entrada errada se corrige na proxima reconexao,
        // porque o slug é revalidado antes de processar.
        private static readonly Dictionary<string, string> _cacheSellerTenant = new();
        private static readonly SemaphoreSlim _lock = new(1, 1);

        public MercadoLivreWebhookRouter(
            IServiceScopeFactory scopeFactory,
            ITenantStore tenantStore,
            ILogger<MercadoLivreWebhookRouter> logger)
        {
            _scopeFactory = scopeFactory;
            _tenantStore = tenantStore;
            _logger = logger;
        }

        public async Task RotearAsync(string topic, string resource, string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                // Sem user_id nao da pra saber de quem e' o evento. Processar no
                // tenant padrao seria o bug antigo: mexer no banco de outra loja.
                _logger.LogWarning(
                    "Notificacao ML sem user_id (topic {Topic}, resource {Resource}) — ignorada por nao ser possivel identificar o tenant.",
                    topic, resource);
                return;
            }

            var tenants = await _tenantStore.ListActiveAsync();
            if (tenants.Count == 0) return;

            // Caminho rapido: seller ja visto antes.
            if (_cacheSellerTenant.TryGetValue(userId, out var slugCache))
            {
                var doCache = tenants.FirstOrDefault(t => t.TenSlug == slugCache);
                if (doCache != null &&
                    await TentarProcessarAsync(doCache.TenId, doCache.TenSlug, doCache.TenDatabaseFile, userId, topic, resource))
                {
                    return;
                }

                // Cache furou (loja trocou de conta ML, tenant desativado): refaz.
                await RemoverDoCacheAsync(userId);
            }

            foreach (var tenant in tenants)
            {
                if (await TentarProcessarAsync(tenant.TenId, tenant.TenSlug, tenant.TenDatabaseFile, userId, topic, resource))
                    return;
            }

            _logger.LogInformation(
                "Notificacao ML do seller {UserId} nao pertence a nenhum tenant ativo — ignorada.", userId);
        }

        /// <summary>
        /// Abre o escopo do tenant e, se a conta ML dele for a do evento,
        /// processa a notificacao ali dentro. Devolve true quando processou.
        /// </summary>
        private async Task<bool> TentarProcessarAsync(
            int tenantId, string slug, string databaseFile, string userId, string topic, string resource)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                scope.ServiceProvider
                    .GetRequiredService<ITenantContext>()
                    .Resolve(tenantId, slug, databaseFile);

                var repo = scope.ServiceProvider.GetRequiredService<IIntegracaoMercadoLivreRepository>();
                var integracao = await repo.GetSingletonAsync();

                if (integracao == null || !string.Equals(integracao.IntSellerId, userId, StringComparison.Ordinal))
                    return false;

                await scope.ServiceProvider
                    .GetRequiredService<IMercadoLivreService>()
                    .ProcessarNotificacaoAsync(topic, resource);

                await GuardarNoCacheAsync(userId, slug);

                _logger.LogInformation(
                    "Notificacao ML (topic {Topic}) do seller {UserId} processada no tenant {Slug}.",
                    topic, userId, slug);
                return true;
            }
            catch (Exception ex)
            {
                // Um tenant com banco corrompido nao pode impedir os outros de
                // receberem os proprios eventos.
                _logger.LogError(ex,
                    "Falha ao avaliar o tenant {Slug} para a notificacao ML do seller {UserId}.", slug, userId);
                return false;
            }
        }

        private static async Task GuardarNoCacheAsync(string userId, string slug)
        {
            await _lock.WaitAsync();
            try { _cacheSellerTenant[userId] = slug; }
            finally { _lock.Release(); }
        }

        private static async Task RemoverDoCacheAsync(string userId)
        {
            await _lock.WaitAsync();
            try { _cacheSellerTenant.Remove(userId); }
            finally { _lock.Release(); }
        }
    }
}
