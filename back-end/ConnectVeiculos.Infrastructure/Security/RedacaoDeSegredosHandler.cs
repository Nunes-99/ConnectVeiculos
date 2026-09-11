using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace ConnectVeiculos.Infrastructure.Security
{
    /// <summary>
    /// Registra as chamadas HTTP de saida com a query string redigida.
    ///
    /// O log nativo do HttpClient escreve a URL inteira em nivel Information
    /// ("Start processing HTTP request GET {url}"). Como as integracoes passam
    /// credenciais na query — client_secret, access_token, code — o segredo do
    /// app da Meta e os tokens de todos os tenants ficavam em texto puro no log
    /// do container, visiveis pra qualquer um com acesso a ele.
    ///
    /// O log nativo e' silenciado no Program.cs; este handler o substitui
    /// mantendo o que serve pra depurar (metodo, host, caminho, status, tempo)
    /// sem o que nao pode vazar.
    /// </summary>
    public sealed class RedacaoDeSegredosHandler : DelegatingHandler
    {
        private static readonly string[] ParametrosSensiveis =
        {
            "client_secret", "access_token", "fb_exchange_token", "refresh_token",
            "code", "token", "api_key", "apikey", "key", "password", "secret",
            "signature", "sig"
        };

        private static readonly Regex Sensivel = new(
            @"(?<nome>" + string.Join("|", ParametrosSensiveis) + @")=(?<valor>[^&]*)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly ILogger<RedacaoDeSegredosHandler> _logger;

        public RedacaoDeSegredosHandler(ILogger<RedacaoDeSegredosHandler> logger)
        {
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = Redigir(request.RequestUri?.ToString());
            var inicio = DateTime.UtcNow;

            try
            {
                var resposta = await base.SendAsync(request, cancellationToken);
                _logger.LogInformation("HTTP {Metodo} {Url} -> {Status} em {Ms}ms",
                    request.Method, url, (int)resposta.StatusCode,
                    (int)(DateTime.UtcNow - inicio).TotalMilliseconds);
                return resposta;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HTTP {Metodo} {Url} falhou em {Ms}ms",
                    request.Method, url, (int)(DateTime.UtcNow - inicio).TotalMilliseconds);
                throw;
            }
        }

        /// <summary>
        /// Troca o valor de cada parametro sensivel por ***. Mantem o nome do
        /// parametro: saber que a chamada levava client_secret ajuda a depurar,
        /// o valor nao.
        /// </summary>
        public static string Redigir(string? url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            return Sensivel.Replace(url, m => $"{m.Groups["nome"].Value}=***");
        }
    }
}
