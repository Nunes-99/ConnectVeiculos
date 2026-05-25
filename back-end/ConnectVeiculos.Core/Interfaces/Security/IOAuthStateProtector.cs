namespace ConnectVeiculos.Core.Interfaces.Security
{
    // Carga util do parametro `state` do OAuth — assinada e cifrada antes de ir pro
    // browser, validada no callback. Nonce + ExpiraEm protegem contra replay e
    // estados muito antigos. TenantSlug evita que callback de um tenant seja
    // aceito por outro (vetor cross-tenant em multi-tenant SaaS).
    public sealed record OAuthStatePayload(
        string TenantSlug,
        string Nonce,
        DateTime ExpiraEm);

    public class OAuthStateException : Exception
    {
        public OAuthStateException(string message) : base(message) { }
    }

    public interface IOAuthStateProtector
    {
        // Cria o state cifrado pra incluir no parametro `state` da URL de autorizacao.
        // TTL fixo de 10 minutos a partir de DateTime.UtcNow.
        string Proteger(string tenantSlug);

        // Decifra o state recebido no callback. Lanca OAuthStateException em caso
        // de state vazio, adulterado, expirado ou com tenantSlug diferente do
        // tenant atual da requisicao.
        OAuthStatePayload Validar(string? state, string tenantSlugAtual);
    }
}
