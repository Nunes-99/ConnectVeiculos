namespace ConnectVeiculos.Infrastructure.Services.Credere
{
    /// <summary>
    /// Chaves do ConnectVeiculos como plataforma parceira da Credere (as
    /// mesmas para todas as lojas). Cada loja conecta a propria conta por OAuth.
    /// </summary>
    public class CredereSettings
    {
        public string BaseUrl { get; set; } = "https://app.meucredere.com.br/api/v1";
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }

        /// <summary>Precisa estar cadastrada na Credere (o suporte deles cadastra).</summary>
        public string RedirectUri { get; set; } = "https://connectveiculos.dev.br/integracoes/credere";

        /// <summary>simulator+proposals ja cobre a fase das propostas, sem pedir nova autorizacao.</summary>
        public string Escopos { get; set; } = "simulator+proposals";

        public bool Configurado => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
    }
}
