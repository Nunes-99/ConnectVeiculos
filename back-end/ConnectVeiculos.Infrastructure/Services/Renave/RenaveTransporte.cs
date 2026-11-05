using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace ConnectVeiculos.Infrastructure.Services.Renave
{
    /// <summary>
    /// Envia a requisicao ao Renave-WS. Separado do servico porque a
    /// autenticacao e' TLS mutuo com o certificado A1 da loja: cada loja tem o
    /// seu, entao o handler HTTP nasce por chamada com o certificado certo.
    /// </summary>
    public interface IRenaveTransporte
    {
        Task<HttpResponseMessage> EnviarAsync(HttpRequestMessage request, X509Certificate2? certificado, CancellationToken ct);
    }

    public class RenaveTransporte : IRenaveTransporte
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

        public async Task<HttpResponseMessage> EnviarAsync(HttpRequestMessage request, X509Certificate2? certificado, CancellationToken ct)
        {
            var handler = new HttpClientHandler { SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13 };
            if (certificado != null)
            {
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                handler.ClientCertificates.Add(certificado);
            }

            using var client = new HttpClient(handler, disposeHandler: true) { Timeout = Timeout };
            var response = await client.SendAsync(request, ct);
            // O corpo e' lido antes de o client ser descartado.
            await response.Content.LoadIntoBufferAsync();
            return response;
        }
    }
}
