using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using ConnectVeiculos.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Jobs
{
    /// <summary>
    /// Remove tokens de recuperacao de senha expirados (mais antigos que 24h)
    /// para evitar acumulo na tabela. Roda em todos os tenants ativos.
    /// </summary>
    public class LimparTokensRecuperacaoJob : IBackgroundJob
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ITenantStore _tenantStore;
        private readonly ILogger<LimparTokensRecuperacaoJob> _logger;

        public string JobName => "LimparTokensRecuperacao";
        public string CronExpression => "0 4 * * *";

        public LimparTokensRecuperacaoJob(
            IServiceScopeFactory scopeFactory,
            ITenantStore tenantStore,
            ILogger<LimparTokensRecuperacaoJob> logger)
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
                    var ctx = ts.Services.GetRequiredService<ConnectVeiculosDbContext>();
                    var limite = DateTime.Now.AddHours(-24);
                    var antigos = await ctx.RecuperacoesSenha
                        .Where(r => r.RecDataCriacao < limite || r.RecUtilizado)
                        .ToListAsync();
                    if (antigos.Count == 0) return;

                    ctx.RecuperacoesSenha.RemoveRange(antigos);
                    await ctx.SaveChangesAsync();
                    _logger.LogInformation("[{Tenant}] {Count} tokens removidos.",
                        tenant.TenSlug, antigos.Count);
                });
        }
    }
}
