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
    /// para evitar acumulo na tabela. Multi-tenant: itera sobre todos os
    /// tenants ativos e roda a limpeza no banco de cada um.
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

        public async Task ExecuteAsync()
        {
            var tenants = await _tenantStore.ListActiveAsync();
            foreach (var tenant in tenants)
            {
                try
                {
                    using var ts = new TenantScope(_scopeFactory, tenant);
                    var ctx = ts.Services.GetRequiredService<ConnectVeiculosDbContext>();

                    var limite = DateTime.Now.AddHours(-24);
                    var antigos = await ctx.RecuperacoesSenha
                        .Where(r => r.RecDataCriacao < limite || r.RecUtilizado)
                        .ToListAsync();

                    if (antigos.Count == 0) continue;

                    ctx.RecuperacoesSenha.RemoveRange(antigos);
                    await ctx.SaveChangesAsync();
                    _logger.LogInformation("[{Tenant}] {Count} tokens de recuperacao removidos.",
                        tenant.TenSlug, antigos.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{Tenant}] erro limpando tokens de recuperacao", tenant.TenSlug);
                    // nao throw — nao interromper outros tenants
                }
            }
        }
    }
}
