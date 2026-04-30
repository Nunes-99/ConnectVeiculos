namespace ConnectVeiculos.Core.Interfaces.Services
{
    /// <summary>
    /// Push instantaneo para Facebook Catalog (alem do feed XML automatico).
    /// Usa a Catalog Batch API para atualizar produtos em segundos.
    /// </summary>
    public interface IFacebookCatalogService
    {
        Task<bool> IsConfiguredAsync();
        Task<FacebookConfigInfo> GetConfigAsync();
        Task SalvarConfigAsync(FacebookConfigInput input);
        Task DesconectarAsync();
        Task<TestIntegracaoResult> TestarAsync();
        Task PublicarVeiculoAsync(int veiculoId);
        Task RemoverVeiculoAsync(int veiculoId);
    }

    public class FacebookConfigInfo
    {
        public bool Configurado { get; set; }
        public string? CatalogId { get; set; }
        public string? ApiVersion { get; set; }
        public bool TokenDefinido { get; set; }
    }

    public class FacebookConfigInput
    {
        public string AccessToken { get; set; } = "";
        public string CatalogId { get; set; } = "";
        public string ApiVersion { get; set; } = "v18.0";
    }

    /// <summary>
    /// Push instantaneo para Google Merchant Center (alem do feed XML automatico).
    /// Usa Content API for Shopping para atualizar produtos em segundos.
    /// </summary>
    public interface IGoogleMerchantService
    {
        Task<bool> IsConfiguredAsync();
        Task<GoogleMerchantConfigInfo> GetConfigAsync();
        Task SalvarConfigAsync(GoogleMerchantConfigInput input);
        Task DesconectarAsync();
        Task<TestIntegracaoResult> TestarAsync();
        Task PublicarVeiculoAsync(int veiculoId);
        Task RemoverVeiculoAsync(int veiculoId);
    }

    public class GoogleMerchantConfigInfo
    {
        public bool Configurado { get; set; }
        public string? MerchantId { get; set; }
        public string? ClientId { get; set; }
        public bool ClientSecretDefinido { get; set; }
        public bool RefreshTokenDefinido { get; set; }
    }

    public class GoogleMerchantConfigInput
    {
        public string ClientId { get; set; } = "";
        public string ClientSecret { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public string MerchantId { get; set; } = "";
    }

    public class TestIntegracaoResult
    {
        public bool Sucesso { get; set; }
        public string? Mensagem { get; set; }
    }
}
