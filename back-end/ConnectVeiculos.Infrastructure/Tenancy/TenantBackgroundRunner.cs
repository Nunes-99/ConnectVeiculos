using ConnectVeiculos.Core.Interfaces.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Tenancy
{
    /// <inheritdoc cref="ITenantBackgroundRunner"/>
    public sealed class TenantBackgroundRunner : ITenantBackgroundRunner
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ITenantContext _tenantContext;
        private readonly ILogger<TenantBackgroundRunner> _logger;

        public TenantBackgroundRunner(
            IServiceScopeFactory scopeFactory,
            ITenantContext tenantContext,
            ILogger<TenantBackgroundRunner> logger)
        {
            _scopeFactory = scopeFactory;
            _tenantContext = tenantContext;
            _logger = logger;
        }

        public void Enqueue<TService>(Func<TService, Task> trabalho, string descricao) where TService : notnull
        {
            if (!_tenantContext.IsResolved)
            {
                _logger.LogWarning(
                    "Ignorando trabalho em background '{Descricao}': tenant nao resolvido, o escopo novo cairia no banco errado.",
                    descricao);
                return;
            }

            // Copia os valores AGORA, com o escopo da request ainda vivo. Guardar
            // o _tenantContext e ler depois seria ler de um objeto ja descartado.
            var tenantId = _tenantContext.TenantId;
            var tenantSlug = _tenantContext.TenantSlug;
            var databaseFile = _tenantContext.DatabaseFile;

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    scope.ServiceProvider
                        .GetRequiredService<ITenantContext>()
                        .Resolve(tenantId, tenantSlug, databaseFile);

                    await trabalho(scope.ServiceProvider.GetRequiredService<TService>());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Trabalho em background '{Descricao}' falhou no tenant {Tenant}",
                        descricao, tenantSlug);
                }
            });
        }
    }
}
