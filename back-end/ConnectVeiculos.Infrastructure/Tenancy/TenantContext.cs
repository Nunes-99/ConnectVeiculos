using ConnectVeiculos.Core.Interfaces.Tenancy;

namespace ConnectVeiculos.Infrastructure.Tenancy
{
    /// <summary>Implementacao Scoped (1 por request) — populada pelo middleware.</summary>
    internal sealed class TenantContext : ITenantContext
    {
        public bool IsResolved { get; private set; }
        public int TenantId { get; private set; }
        public string TenantSlug { get; private set; } = string.Empty;
        public string DatabaseFile { get; private set; } = string.Empty;

        public void Resolve(int tenantId, string slug, string databaseFile)
        {
            if (IsResolved) return; // idempotente — primeira chamada vence
            TenantId = tenantId;
            TenantSlug = slug;
            DatabaseFile = databaseFile;
            IsResolved = true;
        }
    }
}
