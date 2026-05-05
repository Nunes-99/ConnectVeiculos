using ConnectVeiculos.Core.Entities.Tenants;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace ConnectVeiculos.Infrastructure.Tenancy
{
    /// <summary>
    /// Cria um IServiceScope com o ITenantContext ja resolvido para um tenant
    /// especifico. Util para Hangfire jobs e outras operacoes que rodam fora
    /// de request HTTP — sem isso, o scope teria ITenantContext.IsResolved=false
    /// e qualquer DbContext cairia no fallback (so tenant default).
    ///
    /// Uso:
    ///   foreach (var tenant in tenants)
    ///   {
    ///       using var ts = new TenantScope(_scopeFactory, tenant);
    ///       var ctx = ts.Services.GetRequiredService&lt;ConnectVeiculosDbContext&gt;();
    ///       // ... opera no banco do tenant
    ///   }
    /// </summary>
    public sealed class TenantScope : IDisposable
    {
        private readonly IServiceScope _scope;

        public IServiceProvider Services => _scope.ServiceProvider;

        public TenantScope(IServiceScopeFactory factory, Tenant tenant)
        {
            _scope = factory.CreateScope();
            var tenantContext = _scope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenantContext.Resolve(tenant.TenId, tenant.TenSlug, tenant.TenDatabaseFile);
        }

        public void Dispose() => _scope.Dispose();
    }
}
