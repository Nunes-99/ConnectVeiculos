using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

        public bool CadastroDeDominiosLigado =>
            !string.IsNullOrWhiteSpace(_settings.ClientId)
            && !string.IsNullOrWhiteSpace(_settings.ClientSecret)
            && !string.IsNullOrWhiteSpace(_settings.RefreshToken);

        public async Task<string?> PedirCodigoVerificacaoAsync(string siteUrl, CancellationToken ct = default)
        {
            var corpo = JsonSerializer.Serialize(new
            {
                site = new { type = "SITE", identifier = siteUrl },
                verificationMethod = "META"
            });
            var resposta = await ChamarGoogleAsync(HttpMethod.Post,
                "https://www.googleapis.com/siteVerification/v1/token", corpo, "pedir o codigo de verificacao de " + siteUrl, ct);
            if (resposta == null) return null;

            using var json = JsonDocument.Parse(resposta);
            var meta = json.RootElement.TryGetProperty("token", out var t) ? t.GetString() : null;
            return CodigoDaMetaTag(meta);
        }

        public async Task<bool> CodigoNoArAsync(string siteUrl, string codigo, CancellationToken ct = default)
        {
            try
            {
                var html = await _httpClient.GetStringAsync(siteUrl, ct);
                return html.Contains($"content=\"{codigo}\"", StringComparison.Ordinal);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                return false;
            }
        }

        public async Task<bool> CadastrarSiteAsync(string siteUrl, CancellationToken ct = default)
        {
            // 1) Verifica a propriedade pela meta tag (o SSR ja a serve no dominio).
            var corpo = JsonSerializer.Serialize(new { site = new { type = "SITE", identifier = siteUrl } });
            if (await ChamarGoogleAsync(HttpMethod.Post,
                    "https://www.googleapis.com/siteVerification/v1/webResource?verificationMethod=META",
                    corpo, "verificar " + siteUrl, ct) == null)
                return false;

            // 2) Adiciona ao Search Console (idempotente) e 3) envia o sitemap.
            var site = "https://www.googleapis.com/webmasters/v3/sites/" + Uri.EscapeDataString(siteUrl);
            if (await ChamarGoogleAsync(HttpMethod.Put, site, null, "adicionar " + siteUrl + " ao Search Console", ct) == null)
                return false;

            var sitemap = siteUrl.TrimEnd('/') + "/sitemap.xml";
            if (await ChamarGoogleAsync(HttpMethod.Put, site + "/sitemaps/" + Uri.EscapeDataString(sitemap),
                    null, "enviar " + sitemap, ct) == null)
                return false;

            _logger.LogInformation("Search Console: {Site} verificado, adicionado e com o sitemap enviado", siteUrl);
            return true;
        }

        /// <summary>
        /// Tira o "content" da meta tag que a API de verificacao devolve
        /// (&lt;meta name="google-site-verification" content="XYZ" /&gt;).
        /// </summary>
        public static string? CodigoDaMetaTag(string? meta)
        {
            if (string.IsNullOrWhiteSpace(meta)) return null;
            var m = Regex.Match(meta, "content=\"([A-Za-z0-9_-]{10,128})\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>
        /// Chamada autenticada; devolve o corpo (vazio em 204) ou null se falhou.
        /// Sem o escopo siteverification no refresh token o Google responde 403
        /// a cada execucao do job: o aviso sai no maximo uma vez por hora.
        /// </summary>
        private async Task<string?> ChamarGoogleAsync(HttpMethod metodo, string url, string? corpoJson, string acao, CancellationToken ct)
        {
            try
            {
                var token = await ObterAccessTokenAsync(ct);
                if (token == null) return null;

                using var request = new HttpRequestMessage(metodo, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                if (corpoJson != null)
                    request.Content = new StringContent(corpoJson, Encoding.UTF8, "application/json");
                var response = await _httpClient.SendAsync(request, ct);
                var body = await response.Content.ReadAsStringAsync(ct);
                if (response.IsSuccessStatusCode) return body;

                if (PodeAvisar())
                    _logger.LogWarning("Search Console: falha ao {Acao}: HTTP {Status} {Body}", acao, (int)response.StatusCode, body);
                return null;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                if (PodeAvisar())
                    _logger.LogWarning(ex, "Search Console: falha ao {Acao}", acao);
                return null;
            }
        }

        private static DateTime _ultimoAviso = DateTime.MinValue;

        private static bool PodeAvisar()
        {
            lock (Trava)
            {
                if (DateTime.UtcNow - _ultimoAviso < TimeSpan.FromHours(1)) return false;
                _ultimoAviso = DateTime.UtcNow;
                return true;
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
