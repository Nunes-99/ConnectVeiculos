namespace ConnectVeiculos.Core.Interfaces.Tenancy
{
    /// <summary>
    /// Contexto do tenant resolvido para a request atual. Populado pelo
    /// TenantResolutionMiddleware e consumido pelas factories de DbContext/connection.
    /// Implementacao e Scoped por request.
    /// </summary>
    public interface ITenantContext
    {
        /// <summary>True se o middleware ja resolveu o tenant da request.</summary>
        bool IsResolved { get; }

        int TenantId { get; }
        string TenantSlug { get; }
        string DatabaseFile { get; }

        /// <summary>Setter usado SO pelo middleware. Idempotente.</summary>
        void Resolve(int tenantId, string slug, string databaseFile);
    }
}
