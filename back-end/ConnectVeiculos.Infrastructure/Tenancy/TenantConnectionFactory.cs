using ConnectVeiculos.Core.Interfaces.Tenancy;
using Microsoft.Extensions.Configuration;

namespace ConnectVeiculos.Infrastructure.Tenancy
{
    /// <summary>
    /// Decide a connection string a usar:
    ///   1. Se ITenantContext esta resolvido — usa data/{databaseFile} (SQLite).
    ///   2. Senao — fallback para "DefaultConnection" do appsettings ou DATABASE_URL.
    ///
    /// O fallback preserva compatibilidade com codigo que rode fora de request
    /// (startup, jobs Hangfire, testes) ate que tenham injecao de tenant explicita.
    /// </summary>
    internal sealed class TenantConnectionFactory : ITenantConnectionFactory
    {
        private readonly ITenantContext _tenant;
        private readonly string _fallbackConnectionString;
        private readonly string _dataDirectory;

        public TenantConnectionFactory(ITenantContext tenant, IConfiguration configuration)
        {
            _tenant = tenant;
            _fallbackConnectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
                ?? configuration.GetConnectionString("DefaultConnection")
                ?? "Data Source=ConnectVeiculos.db";
            _dataDirectory = Environment.GetEnvironmentVariable("TENANTS_DATA_DIR")
                ?? "/app/data";
        }

        public string GetConnectionString()
        {
            if (_tenant.IsResolved)
            {
                var path = Path.Combine(_dataDirectory, _tenant.DatabaseFile);
                return $"Data Source={path}";
            }
            return _fallbackConnectionString;
        }

        public string GetConnectionStringForTenant(string slug, string databaseFile)
        {
            var path = Path.Combine(_dataDirectory, databaseFile);
            return $"Data Source={path}";
        }
    }
}
