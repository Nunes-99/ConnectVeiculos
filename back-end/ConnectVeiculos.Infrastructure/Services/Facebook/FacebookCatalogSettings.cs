namespace ConnectVeiculos.Infrastructure.Services.Facebook
{
    public class FacebookCatalogSettings
    {
        public string AccessToken { get; set; }
        public string CatalogId { get; set; }
        public string ApiVersion { get; set; } = "v18.0";

        // Fallback usado para montar links de produto quando Loja.LojUrlCatalogo
        // estiver vazio ou invalido. Deve ser a URL publica do site (sem path),
        // ex: https://connectveiculos.dev.br. Definido via env var no compose.
        public string PublicSiteUrl { get; set; }
    }
}
