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

        /// <summary>
        /// Tenant dono do dominio proprio (aceita o Host com ou sem "www.").
        /// Null quando o host nao e' dominio proprio de ninguem.
        /// </summary>
        Task<Tenant?> GetByDominioAsync(string host, CancellationToken ct = default);

        /// <summary>
        /// Grava (ou limpa, com null) o dominio proprio ja normalizado. Lanca
        /// InvalidOperationException se outro tenant ja usa o dominio.
        /// </summary>
        Task<Tenant> UpdateDominioProprioAsync(int tenantId, string? dominio, CancellationToken ct = default);

        /// <summary>Marca o dominio como ativo (HTTPS confirmado no servidor).</summary>
        Task<Tenant> AtivarDominioProprioAsync(int tenantId, CancellationToken ct = default);

        /// <summary>
        /// Aplica <paramref name="mudanca"/> no tenant do master, salva e atualiza
        /// o cache (sem isso o plano novo so valeria depois de reiniciar).
        /// </summary>
        Task<Tenant> AtualizarAsync(int tenantId, Action<Tenant> mudanca, CancellationToken ct = default);

        /// <summary>Invalida o cache em memoria. Chame apos criar/atualizar/suspender tenant.</summary>
        void InvalidateCache();
    }
}
