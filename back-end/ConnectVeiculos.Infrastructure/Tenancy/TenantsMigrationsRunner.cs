using ConnectVeiculos.Core.Entities.Tenants;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Tenancy
{
    /// <summary>
    /// No startup, garante que (a) o banco master existe e (b) cada tenant ativo
    /// tem seu banco criado com schema atual. Idempotente — usa EnsureCreated.
    ///
    /// Importante: roda OFFLINE (sem request HTTP), entao nao tem ITenantContext
    /// scoped. Para cada tenant, criamos um DbContextOptions manual e construimos
    /// o ConnectVeiculosDbContext apontando para o banco daquele tenant.
    /// </summary>
    public sealed class TenantsMigrationsRunner
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<TenantsMigrationsRunner> _logger;

        public TenantsMigrationsRunner(IServiceProvider services, ILogger<TenantsMigrationsRunner> logger)
        {
            _services = services;
            _logger = logger;
        }

        public async Task RunAsync(CancellationToken ct = default)
        {
            using var scope = _services.CreateScope();
            var sp = scope.ServiceProvider;

            // 1) Master: cria se nao existe e popula tenant default na primeira vez.
            var master = sp.GetRequiredService<MasterDbContext>();
            await master.Database.EnsureCreatedAsync(ct);

            // 2) Auto-seed do tenant "default" se o master estiver vazio.
            //    Aponta para o arquivo configurado em DEFAULT_TENANT_DATABASE_FILE
            //    (default: "cliente.db", preservando o banco existente em prod).
            //    Isso elimina passo manual de migracao no primeiro deploy do
            //    multi-tenant — o sistema se auto-configura.
            if (!await master.Tenants.AnyAsync(ct))
            {
                var defaultDbFile = Environment.GetEnvironmentVariable("DEFAULT_TENANT_DATABASE_FILE") ?? "cliente.db";
                var defaultTenant = new Tenant("default", "Tenant Padrão", defaultDbFile);
                master.Tenants.Add(defaultTenant);
                await master.SaveChangesAsync(ct);
                _logger.LogInformation("Master vazio: tenant 'default' criado automaticamente, banco {File}", defaultDbFile);
            }

            // 3) Itera tenants ativos do master e roda EnsureCreated em cada banco.
            var store = sp.GetRequiredService<ITenantStore>();
            store.InvalidateCache(); // forca releitura caso tenha sido populado por outra instancia
            var tenants = await store.ListActiveAsync(ct);

            if (tenants.Count == 0)
            {
                _logger.LogWarning("Nenhum tenant ativo apos seed — algo errado");
                return;
            }

            var tenantFactory = sp.GetRequiredService<ITenantConnectionFactory>();
            var softDeleteInterceptor = sp.GetRequiredService<Database.Interceptors.SoftDeleteInterceptor>();

            foreach (var tenant in tenants)
            {
                var connStr = tenantFactory.GetConnectionStringForTenant(tenant.TenSlug, tenant.TenDatabaseFile);
                var optionsBuilder = new DbContextOptionsBuilder<ConnectVeiculosDbContext>()
                    .UseSqlite(connStr)
                    .AddInterceptors(softDeleteInterceptor);

                using var ctx = new ConnectVeiculosDbContext(optionsBuilder.Options);
                await ctx.Database.EnsureCreatedAsync(ct);

                _logger.LogInformation("Tenant '{Slug}' ({Nome}): banco {File} pronto",
                    tenant.TenSlug, tenant.TenNome, tenant.TenDatabaseFile);
            }
        }
    }
}
