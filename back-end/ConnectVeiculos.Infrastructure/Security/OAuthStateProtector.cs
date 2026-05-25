using System.Text.Json;
using ConnectVeiculos.Core.Interfaces.Security;

namespace ConnectVeiculos.Infrastructure.Security
{
    public sealed class OAuthStateProtector : IOAuthStateProtector
    {
        private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(10);

        private readonly ITokenProtector _tokenProtector;

        public OAuthStateProtector(ITokenProtector tokenProtector)
        {
            _tokenProtector = tokenProtector;
        }

        public string Proteger(string tenantSlug)
        {
            var payload = new OAuthStatePayload(
                tenantSlug ?? string.Empty,
                Guid.NewGuid().ToString("N"),
                DateTime.UtcNow.Add(StateTtl));
            var json = JsonSerializer.Serialize(payload);
            return _tokenProtector.Protect(json);
        }

        public OAuthStatePayload Validar(string? state, string tenantSlugAtual)
        {
            if (string.IsNullOrWhiteSpace(state))
                throw new OAuthStateException("Parametro 'state' ausente. Inicie a conexao OAuth a partir do painel.");

            string json;
            try
            {
                json = _tokenProtector.Unprotect(state);
            }
            catch
            {
                // CryptographicException — state adulterado, expirado da chave antiga ou de outra origem.
                throw new OAuthStateException("State invalido ou expirado. Tente conectar de novo.");
            }

            OAuthStatePayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<OAuthStatePayload>(json);
            }
            catch
            {
                throw new OAuthStateException("State invalido (payload corrompido).");
            }

            if (payload is null)
                throw new OAuthStateException("State invalido (payload vazio).");

            if (DateTime.UtcNow > payload.ExpiraEm)
                throw new OAuthStateException("State expirado (autorizacao demorou demais). Tente de novo.");

            if (!string.Equals(payload.TenantSlug, tenantSlugAtual, StringComparison.OrdinalIgnoreCase))
                throw new OAuthStateException(
                    $"State pertence a outro tenant ('{payload.TenantSlug}' != '{tenantSlugAtual}'). " +
                    "Confirme que esta conectando no subdomain certo.");

            return payload;
        }
    }
}
