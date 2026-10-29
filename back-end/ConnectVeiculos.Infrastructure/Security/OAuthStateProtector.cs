using System.Text.Json;
using ConnectVeiculos.Core.Interfaces.Security;
using Microsoft.AspNetCore.Http;

namespace ConnectVeiculos.Infrastructure.Security
{
    public sealed class OAuthStateProtector : IOAuthStateProtector
    {
        private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(10);

        // O state sozinho so prova "foi gerado para esta loja". Um admin de outra
        // loja podia gerar um link de conexao e mandar para o dono de uma Page ou
        // conta do ML: quem aprovasse ligava a propria conta na loja do atacante.
        // O cookie amarra o state ao navegador que pediu a conexao.
        private const string PrefixoCookie = "cv_oauth_";

        private readonly ITokenProtector _tokenProtector;
        private readonly IHttpContextAccessor? _httpContextAccessor;

        public OAuthStateProtector(ITokenProtector tokenProtector, IHttpContextAccessor? httpContextAccessor = null)
        {
            _tokenProtector = tokenProtector;
            _httpContextAccessor = httpContextAccessor;
        }

        public string Proteger(string tenantSlug)
        {
            var payload = new OAuthStatePayload(
                tenantSlug ?? string.Empty,
                Guid.NewGuid().ToString("N"),
                DateTime.UtcNow.Add(StateTtl));
            var json = JsonSerializer.Serialize(payload);

            _httpContextAccessor?.HttpContext?.Response.Cookies.Append(PrefixoCookie + payload.Nonce, "1",
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    // Lax: o retorno do Facebook/ML e' navegacao de topo (GET), que leva o cookie.
                    SameSite = SameSiteMode.Lax,
                    Path = "/api/integracoes",
                    MaxAge = StateTtl
                });

            return _tokenProtector.Protect(json);
        }

        public OAuthStatePayload Validar(string? state, string tenantSlugAtual)
        {
            var payload = Decifrar(state);

            if (!string.Equals(payload.TenantSlug, tenantSlugAtual, StringComparison.OrdinalIgnoreCase))
                throw new OAuthStateException(
                    $"State pertence a outro tenant ('{payload.TenantSlug}' != '{tenantSlugAtual}'). " +
                    "Confirme que esta conectando no subdomain certo.");

            var http = _httpContextAccessor?.HttpContext;
            if (http != null)
            {
                var nome = PrefixoCookie + payload.Nonce;
                if (!http.Request.Cookies.ContainsKey(nome))
                    throw new OAuthStateException(
                        "Esta autorizacao nao foi iniciada neste navegador. Clique em Conectar no painel da loja e aprove na mesma janela.");
                // Uso unico.
                http.Response.Cookies.Delete(nome, new CookieOptions { Path = "/api/integracoes", Secure = true, HttpOnly = true, SameSite = SameSiteMode.Lax });
            }

            return payload;
        }

         public OAuthStatePayload Decifrar(string? state)
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

             return payload;
         }
    }
}
