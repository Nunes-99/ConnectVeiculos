namespace ConnectVeiculos.Core.Interfaces.Services
{
    public interface IQrCodeService
    {
        byte[] GerarQrCode(string conteudo, int tamanho = 250);
        string GerarQrCodeBase64(string conteudo, int tamanho = 250);
        /// <summary>
        /// QR code que aponta pra pagina publica do veiculo. O tenantSlug e'
        /// obrigatorio: a rota e' /catalogo/{tenantSlug}/veiculo/{id} e sem ele
        /// o QR impresso e colado no carro nao abre o veiculo.
        /// </summary>
        byte[] GerarQrCodeVeiculo(int veiculoId, string baseUrl, string tenantSlug);
    }
}
