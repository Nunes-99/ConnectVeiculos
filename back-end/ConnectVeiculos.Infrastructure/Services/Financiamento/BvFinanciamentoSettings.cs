namespace ConnectVeiculos.Infrastructure.Services.Financiamento
{
    public class BvFinanciamentoSettings
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string BaseUrl { get; set; } = "https://api-sandbox.bvopen.com.br";
        public string PartnerId { get; set; }
    }

    public class PanFinanciamentoSettings
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string BaseUrl { get; set; } = "https://api-sandbox.bancopan.com.br";
    }
}
