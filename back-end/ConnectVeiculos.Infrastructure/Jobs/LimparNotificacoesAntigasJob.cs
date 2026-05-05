using ConnectVeiculos.Core.Interfaces.Database.Repositories.Notificacoes;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Jobs
{
    /// <summary>
    /// Limpa notificacoes lidas com mais de 30 dias em cada tenant.
    /// </summary>
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

        public async Task ExecuteAsync()
        {
            var tenants = await _tenantStore.ListActiveAsync();
            var dataLimite = DateTime.UtcNow.AddDays(-DiasRetencao);

            foreach (var tenant in tenants)
            {
                try
                {
                    using var ts = new TenantScope(_scopeFactory, tenant);
                    var repo = ts.Services.GetRequiredService<INotificacaoRepository>();
                    await repo.DeleteAntigasLidasAsync(dataLimite);
                    _logger.LogInformation("[{Tenant}] notificacoes antigas limpas.", tenant.TenSlug);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{Tenant}] erro limpando notificacoes", tenant.TenSlug);
                }
            }
        }
    }
}
