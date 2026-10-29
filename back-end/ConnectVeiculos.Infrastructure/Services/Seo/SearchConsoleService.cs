using System.Text.Json;
using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.Infrastructure.Services.Seo
{
    public class SearchConsoleService : ISearchConsoleService
    {
        // O Google nao rele o sitemap mais rapido que isso, e cada cadastro de
        // foto dispara uma atualizacao de veiculo: sem o intervalo, um carro
        // novo virava dezenas de reenvios seguidos.
        private static readonly TimeSpan IntervaloMinimo = TimeSpan.FromHours(1);
        private static readonly object Trava = new();
        private static DateTime _ultimoEnvio = DateTime.MinValue;

        private readonly HttpClient _httpClient;
        private readonly SearchConsoleSettings _settings;
        private readonly ILogger<SearchConsoleService> _logger;

        public SearchConsoleService(
            HttpClient httpClient,
            IOptions<SearchConsoleSettings> settings,
            ILogger<SearchConsoleService> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task ReenviarSitemapAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_settings.ClientId)
                || string.IsNullOrWhiteSpace(_settings.ClientSecret)
                || string.IsNullOrWhiteSpace(_settings.RefreshToken)
                || string.IsNullOrWhiteSpace(_settings.SiteUrl))
                return;

            var sitemap = $"{_settings.PublicSiteUrl.Trim().TrimEnd('/')}/sitemap.xml";
            if (!Uri.IsWellFormedUriString(sitemap, UriKind.Absolute))
                return;

            lock (Trava)
            {
                if (DateTime.UtcNow - _ultimoEnvio < IntervaloMinimo)
                    return;
                _ultimoEnvio = DateTime.UtcNow;
            }

            try
            {
                var token = await ObterAccessTokenAsync(ct);
                if (token == null) return;

                var url = "https://www.googleapis.com/webmasters/v3/sites/"
                    + Uri.EscapeDataString(_settings.SiteUrl.Trim())
                    + "/sitemaps/" + Uri.EscapeDataString(sitemap);
                using var request = new HttpRequestMessage(HttpMethod.Put, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                var response = await _httpClient.SendAsync(request, ct);

                // 403 = a conta do token nao e' dona da propriedade, ou o token
                // nao tem o escopo webmasters. Fire-and-forget: so registra.
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning("Search Console recusou o sitemap {Sitemap}: HTTP {Status} {Body}",
                        sitemap, (int)response.StatusCode, body);
                    return;
                }

                _logger.LogInformation("Sitemap {Sitemap} reenviado ao Search Console", sitemap);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogWarning(ex, "Falha ao reenviar o sitemap ao Search Console");
            }
        }

        private async Task<string?> ObterAccessTokenAsync(CancellationToken ct)
        {
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _settings.ClientId.Trim(),
                ["client_secret"] = _settings.ClientSecret.Trim(),
                ["refresh_token"] = _settings.RefreshToken.Trim(),
                ["grant_type"] = "refresh_token"
            });
            var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", form, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Search Console: falha ao renovar o token do Google: HTTP {Status} {Body}",
                    (int)response.StatusCode, body);
                return null;
            }

            using var json = JsonDocument.Parse(body);
            return json.RootElement.TryGetProperty("access_token", out var t) ? t.GetString() : null;
        }
    }
}
