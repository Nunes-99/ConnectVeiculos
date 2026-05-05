using ConnectVeiculos.Core.Interfaces.Database.Repositories.RefreshTokens;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Jobs
{
    /// <summary>
    /// Limpa refresh tokens expirados de cada tenant.
    /// </summary>
    public class LimparRefreshTokensJob : IBackgroundJob
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ITenantStore _tenantStore;
        private readonly ILogger<LimparRefreshTokensJob> _logger;

        public string JobName => "LimparRefreshTokensExpirados";
        public string CronExpression => "0 3 * * *";

        public LimparRefreshTokensJob(
            IServiceScopeFactory scopeFactory,
            ITenantStore tenantStore,
            ILogger<LimparRefreshTokensJob> logger)
        {
            _scopeFactory = scopeFactory;
            _tenantStore = tenantStore;
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            var tenants = await _tenantStore.ListActiveAsync();
            foreach (var tenant in tenants)
            {
                try
                {
                    using var ts = new TenantScope(_scopeFactory, tenant);
                    var repo = ts.Services.GetRequiredService<IRefreshTokenRepository>();
                    await repo.DeleteExpiredTokensAsync();
                    _logger.LogInformation("[{Tenant}] refresh tokens expirados limpos.", tenant.TenSlug);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{Tenant}] erro limpando refresh tokens", tenant.TenSlug);
                }
            }
        }
    }
}
