namespace ConnectVeiculos.Infrastructure.Services.Renave
{
    public class RenaveSettings
    {
        // Enderecos do Renave-WS (Serpro). A homologacao e' gratuita, fica no ar
        // 24h e aceita chamadas sem certificado como o "Estabelecimento padrao
        // de teste" (CNPJ 85106037000585).
        public string UrlHomologacao { get; set; } = "https://hom.renave.estaleiro.serpro.gov.br/renave-ws";
        public string UrlProducao { get; set; } = "https://renave.estaleiro.serpro.gov.br/renave-ws";
    }
}
