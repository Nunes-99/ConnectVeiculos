using ConnectVeiculos.Core.Entities.Tenants;

namespace ConnectVeiculos.Core.Interfaces.Tenancy
{
    /// <summary>
    /// Acesso ao registry de tenants (banco master). Implementacao tipicamente
    /// cacheia em memoria — alteracoes no master sao raras (criar/suspender tenant).
    /// </summary>
    public interface ITenantStore
    {
        Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default);
        Task<Tenant?> GetByIdAsync(int tenantId, CancellationToken ct = default);
        Task<IReadOnlyList<Tenant>> ListActiveAsync(CancellationToken ct = default);

        /// <summary>
        /// Persiste o codigo de verificacao de dominio (Google e/ou Facebook) do
        /// tenant. Passar null em qualquer parametro mantem o valor anterior;
        /// passar string vazia LIMPA. Atualiza o cache em memoria apos commit.
        /// </summary>
        Task UpdateVerificationCodesAsync(int tenantId, string? googleCode, string? facebookCode, CancellationToken ct = default);

        /// <summary>Invalida o cache em memoria. Chame apos criar/atualizar/suspender tenant.</summary>
        void InvalidateCache();
    }
}
