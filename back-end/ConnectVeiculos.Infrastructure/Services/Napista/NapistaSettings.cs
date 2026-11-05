namespace ConnectVeiculos.Infrastructure.Services.Napista
{
    /// <summary>
    /// Chaves do ConnectVeiculos como integrador de estoque do NaPista (as
    /// mesmas para todas as lojas). Cliente publico: so client_id, sem segredo.
    /// Padrao = ambiente de desenvolvimento do NaPista; producao pelo .env.
    /// </summary>
    public class NapistaSettings
    {
        /// <summary>Base do OpenID Connect (Keycloak): .../auth, .../token, .../logout.</summary>
        public string AuthUrl { get; set; } = "https://auth.development.napista.com.br/realms/marketplace-dealer/protocol/openid-connect";

        public string ApiUrl { get; set; } = "https://api.development.napista.com.br/seller-inventory-api";

        public string? ClientId { get; set; }

        /// <summary>Precisa estar cadastrada no NaPista (eles cadastram junto com o client_id).</summary>
        public string RedirectUri { get; set; } = "https://connectveiculos.dev.br/integracoes/napista";

        /// <summary>
        /// "openid" basta para entrar. Se o NaPista liberar "offline_access", a
        /// conexao dura sem a loja entrar de novo (refresh token sem validade curta).
        /// </summary>
        public string Escopos { get; set; } = "openid";

        public bool Configurado => !string.IsNullOrWhiteSpace(ClientId);
        public bool Desenvolvimento => AuthUrl.Contains(".development.", StringComparison.OrdinalIgnoreCase);
    }
}
