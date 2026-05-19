namespace ConnectVeiculos.Infrastructure.Services.Google
{
    public class GoogleMerchantSettings
    {
        public string AccessToken { get; set; }
        public string MerchantId { get; set; }
        public string RefreshToken { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }

        // Fallback usado para montar links de produto quando Loja.LojUrlCatalogo
        // estiver vazio ou invalido. Deve ser a URL publica do site (sem path),
        // ex: https://connectveiculos.dev.br
        public string PublicSiteUrl { get; set; }
    }
}
