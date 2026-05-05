using ConnectVeiculos.Core.Interfaces.Database.Repositories.Notificacoes;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Jobs
{
    public class LimparNotificacoesAntigasJob : IBackgroundJob
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ITenantStore _tenantStore;
        private readonly ILogger<LimparNotificacoesAntigasJob> _logger;
        private const int DiasRetencao = 30;

        public string JobName => "LimparNotificacoesAntigas";
        public string CronExpression => "0 4 * * 0";

        public LimparNotificacoesAntigasJob(
            IServiceScopeFactory scopeFactory,
            ITenantStore tenantStore,
            ILogger<LimparNotificacoesAntigasJob> logger)
        {
            _scopeFactory = scopeFactory;
            _tenantStore = tenantStore;
            _logger = logger;
        }

        public Task ExecuteAsync()
        {
            var dataLimite = DateTime.UtcNow.AddDays(-DiasRetencao);
            return MultiTenantJobExecutor.RunAsync(JobName, _tenantStore, _scopeFactory, _logger,
                async (ts, tenant) =>
                {
                    var repo = ts.Services.GetRequiredService<INotificacaoRepository>();
                    await repo.DeleteAntigasLidasAsync(dataLimite);
                });
        }
    }
}
