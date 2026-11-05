namespace ConnectVeiculos.Infrastructure.Services.Webmotors
{
    public class WebmotorsSettings
    {
        /// <summary>
        /// Raiz dos WebServices. Padrao: homologacao. Producao (liberada pela
        /// Webmotors depois dos testes, chamado PTS-1524):
        /// https://integracao.webmotors.com.br
        /// </summary>
        public string BaseUrl { get; set; } = "https://hportal.webmotors.com.br/IntegracaoRevendedor";

        /// <summary>
        /// Token "Authorization: bearer" que a colecao do Postman da Webmotors
        /// preve. A homologacao nao exige; vazio, o cabecalho nao vai.
        /// </summary>
        public string Token { get; set; } = "";

        public bool Homologacao => BaseUrl.Contains("hportal", StringComparison.OrdinalIgnoreCase);
    }
}
