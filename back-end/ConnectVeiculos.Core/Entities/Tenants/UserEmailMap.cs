namespace ConnectVeiculos.Core.Entities.Tenants
{
    /// <summary>
    /// Registry global de e-mail → tenant no banco master. Garante que um mesmo
    /// e-mail nao exista em dois tenants ao mesmo tempo (evita ambiguidade no login
    /// cross-tenant). Populado pela camada de cadastro de usuario/tenant; consultado
    /// no Registrar e no Login.
    /// </summary>
    public class UserEmailMap
    {
        public int Id { get; private set; }
        public string Email { get; private set; } = string.Empty;
        public int TenantId { get; private set; }
        public string TenantSlug { get; private set; } = string.Empty;
        public DateTime CriadoEm { get; private set; } = DateTime.UtcNow;

        public UserEmailMap() { }

        public UserEmailMap(string email, int tenantId, string tenantSlug)
        {
            Email = email.Trim().ToLowerInvariant();
            TenantId = tenantId;
            TenantSlug = tenantSlug;
            CriadoEm = DateTime.UtcNow;
        }
    }
}
