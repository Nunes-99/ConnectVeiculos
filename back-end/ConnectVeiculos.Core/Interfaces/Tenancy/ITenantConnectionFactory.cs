namespace ConnectVeiculos.Core.Interfaces.Tenancy
{
    /// <summary>
    /// Resolve a connection string a ser usada no request atual, baseado no
    /// ITenantContext resolvido pelo middleware. Se o tenant nao foi resolvido
    /// (ex: rotas que rodam fora do contexto de request — startup/jobs), cai
    /// para a connection string padrao ("DefaultConnection" / DATABASE_URL).
    /// </summary>
    public interface ITenantConnectionFactory
    {
        /// <summary>Connection string completa para o tenant atual (ou fallback).</summary>
        string GetConnectionString();

        /// <summary>Connection string para um tenant especifico (uso em jobs/admin).</summary>
        string GetConnectionStringForTenant(string slug, string databaseFile);
    }
}
