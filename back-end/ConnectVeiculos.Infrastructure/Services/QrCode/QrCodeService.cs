using ConnectVeiculos.Core.Interfaces.Services;
using QRCoder;

namespace ConnectVeiculos.Infrastructure.Services.QrCode
{
    public class QrCodeService : IQrCodeService
    {
        public byte[] GerarQrCode(string conteudo, int tamanho = 250)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(conteudo, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);

            return qrCode.GetGraphic(tamanho / 25); // pixelsPerModule
        }

        public string GerarQrCodeBase64(string conteudo, int tamanho = 250)
        {
            var bytes = GerarQrCode(conteudo, tamanho);
            return Convert.ToBase64String(bytes);
        }

        public byte[] GerarQrCodeVeiculo(int veiculoId, string baseUrl)
        {
            var url = $"{baseUrl.TrimEnd('/')}/catalogo/veiculo/{veiculoId}";
            return GerarQrCode(url);
        }
    }
}
