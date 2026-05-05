using ConnectVeiculos.Core.Interfaces.Database.Repositories.RefreshTokens;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Jobs
{
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

        public Task ExecuteAsync()
        {
            return MultiTenantJobExecutor.RunAsync(JobName, _tenantStore, _scopeFactory, _logger,
                async (ts, tenant) =>
                {
                    var repo = ts.Services.GetRequiredService<IRefreshTokenRepository>();
                    await repo.DeleteExpiredTokensAsync();
                });
        }
    }
}
