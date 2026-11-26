namespace ConnectVeiculos.Infrastructure.Services.Olx
{
    /// <summary>
    /// Chaves do ConnectVeiculos como integrador da OLX (as mesmas para todas
    /// as lojas), liberadas pelo suporteintegrador@olxbr.com. A OLX nao tem
    /// ambiente de testes: e' sempre producao.
    /// </summary>
    public class OlxSettings
    {
        public string AuthUrl { get; set; } = "https://auth.olx.com.br";
        public string ApiUrl { get; set; } = "https://apps.olx.com.br/autoupload";
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }

        /// <summary>Precisa ser uma das cadastradas na OLX.</summary>
        public string RedirectUri { get; set; } = "https://connectveiculos.dev.br/integracoes/olx";

        /// <summary>autoupload = importar anuncios; basic_user_info = dados da conta.</summary>
        public string Escopos { get; set; } = "autoupload basic_user_info";

        public bool Configurado => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
    }
}
