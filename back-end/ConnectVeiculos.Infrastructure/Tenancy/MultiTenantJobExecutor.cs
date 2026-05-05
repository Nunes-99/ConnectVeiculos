using ConnectVeiculos.Core.Entities.Tenants;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Tenancy
{
    /// <summary>
    /// Executor de logica multi-tenant para Hangfire jobs (e qualquer outra
    /// operacao que precisa rodar em todos os tenants ativos). Encapsula
    /// o loop de tenants, criacao de TenantScope e tratamento de erros:
    ///
    ///   - Erros isolados por tenant (1 falha nao para os outros)
    ///   - Re-throw apenas se TODOS os tenants falharem — assim Hangfire dashboard
    ///     mostra o job como Failed e tem retry automatico, mas falhas parciais
    ///     ficam apenas em log (e o job aparece como Succeeded)
    ///   - Logs prefixados com [tenant] para diagnostico facil
    /// </summary>
    public static class MultiTenantJobExecutor
    {
        public static async Task RunAsync(
            string jobName,
            ITenantStore tenantStore,
            IServiceScopeFactory scopeFactory,
            ILogger logger,
            Func<TenantScope, Tenant, Task> action,
            CancellationToken ct = default)
        {
            var tenants = await tenantStore.ListActiveAsync(ct);
            if (tenants.Count == 0)
            {
                logger.LogInformation("[{Job}] nenhum tenant ativo — nada a fazer", jobName);
                return;
            }

            int total = tenants.Count;
            int failed = 0;

            foreach (var tenant in tenants)
            {
                try
                {
                    using var ts = new TenantScope(scopeFactory, tenant);
                    await action(ts, tenant);
                }
                catch (Exception ex)
                {
                    failed++;
                    logger.LogError(ex, "[{Job}/{Tenant}] erro processando tenant", jobName, tenant.TenSlug);
                }
            }

            if (failed == total)
            {
                throw new InvalidOperationException(
                    $"Job '{jobName}' falhou em TODOS os {total} tenants. Ver logs anteriores para causas individuais.");
            }
            if (failed > 0)
            {
                logger.LogWarning("[{Job}] concluido com {Failed}/{Total} tenants em erro (job marcado Succeeded — Hangfire nao retry parcial)",
                    jobName, failed, total);
            }
            else
            {
                logger.LogInformation("[{Job}] concluido em {Total} tenants sem erros", jobName, total);
            }
        }
    }
}
