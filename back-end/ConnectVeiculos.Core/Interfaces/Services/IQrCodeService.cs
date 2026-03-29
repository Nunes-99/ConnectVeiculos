namespace ConnectVeiculos.Core.Interfaces.Services
{
    public interface IQrCodeService
    {
        byte[] GerarQrCode(string conteudo, int tamanho = 250);
        string GerarQrCodeBase64(string conteudo, int tamanho = 250);
        byte[] GerarQrCodeVeiculo(int veiculoId, string baseUrl);
    }
}
