using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.Infrastructure.Services.Meta
{
    public class FacebookPagePostService : IFacebookPagePostService
    {
        public const string KEY_AUTO_POST = "META_FB_AUTO_POST";

        private readonly HttpClient _httpClient;
        private readonly MetaSettings _settings;
        private readonly IConfiguracaoSistemaRepository _configRepository;
        private readonly ITokenProtector _tokenProtector;
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly IVeiculoImagemRepository _imagemRepository;
        private readonly ILojaRepository _lojaRepository;
        private readonly ILogger<FacebookPagePostService> _logger;

        public FacebookPagePostService(
            HttpClient httpClient,
            IOptions<MetaSettings> settings,
            IConfiguracaoSistemaRepository configRepository,
            ITokenProtector tokenProtector,
            IVeiculoRepository veiculoRepository,
            IVeiculoImagemRepository imagemRepository,
            ILojaRepository lojaRepository,
            ILogger<FacebookPagePostService> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _configRepository = configRepository;
            _tokenProtector = tokenProtector;
            _veiculoRepository = veiculoRepository;
            _imagemRepository = imagemRepository;
            _lojaRepository = lojaRepository;
            _logger = logger;
        }

        public async Task<bool> IsConfiguredAsync()
        {
            var (token, pageId) = await ResolvePageCredentialsAsync();
            return !string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(pageId);
        }

        public async Task<FacebookPagePostConfigInfo> GetConfigAsync()
        {
            var (token, pageId) = await ResolvePageCredentialsAsync();
            var pageNome = await _configRepository.GetValorAsync(MetaOAuthService.KEY_PAGE_NOME);
            return new FacebookPagePostConfigInfo
            {
                PageConectada = !string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(pageId),
                PageId = string.IsNullOrEmpty(pageId) ? null : pageId,
                PageNome = string.IsNullOrEmpty(pageNome) ? null : pageNome,
                AutoPostHabilitado = await AutoPostHabilitadoAsync()
            };
        }

        public async Task SetAutoPostHabilitadoAsync(bool habilitado)
        {
            await _configRepository.SetValorAsync(KEY_AUTO_POST, habilitado ? "true" : "false");
            _logger.LogInformation("Facebook auto-post {Status} pelo operador.", habilitado ? "HABILITADO" : "DESABILITADO");
        }

        public async Task<TestIntegracaoResult> TestarAsync()
        {
            var (token, pageId) = await ResolvePageCredentialsAsync();
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(pageId))
                return new TestIntegracaoResult { Sucesso = false, Mensagem = "Page nao selecionada. Conecte sua conta Meta primeiro." };

            try
            {
                var url = $"https://graph.facebook.com/{_settings.ApiVersion}/{pageId}?fields=name,fan_count&access_token={Uri.EscapeDataString(token)}";
                var resp = await _httpClient.GetAsync(url);
                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    return new TestIntegracaoResult { Sucesso = false, Mensagem = $"Falha ({(int)resp.StatusCode}): {body}" };

                using var doc = JsonDocument.Parse(body);
                var nome = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() : "";
                var fans = doc.RootElement.TryGetProperty("fan_count", out var f) ? f.GetInt32() : 0;
                return new TestIntegracaoResult { Sucesso = true, Mensagem = $"Page '{nome}' OK. {fans} seguidor(es)." };
            }
            catch (Exception ex)
            {
                return new TestIntegracaoResult { Sucesso = false, Mensagem = ex.Message };
            }
        }

        public async Task PublicarVeiculoAsync(int veiculoId)
        {
            if (!await AutoPostHabilitadoAsync())
            {
                _logger.LogDebug("Facebook auto-post desabilitado, pulando veiculo {VeiculoId}", veiculoId);
                return;
            }

            var (token, pageId) = await ResolvePageCredentialsAsync();
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(pageId))
            {
                _logger.LogDebug("Facebook Page nao configurada, pulando veiculo {VeiculoId}", veiculoId);
                return;
            }

            var veiculo = await _veiculoRepository.GetByIdAsync(veiculoId);
            if (veiculo == null) return;

            var imagens = await _imagemRepository.GetByVeiculoIdAsync(veiculoId);
            var loja = await _lojaRepository.GetByIdAsync(veiculo.R_LojId);
            var baseUrl = NormalizeBaseUrl(loja?.LojUrlCatalogo) ?? NormalizeBaseUrl(_settings?.PublicSiteUrl);
            if (baseUrl == null)
            {
                _logger.LogWarning("Facebook Page post abortado: sem URL publica configurada (veiculo {VeiculoId})", veiculoId);
                return;
            }

            var slug = loja?.LojSlug ?? veiculo.R_LojId.ToString();
            var linkVeiculo = $"{baseUrl}/catalogo/{slug}/veiculo/{veiculoId}";
            var imagemPrincipal = imagens.Where(i => i.ImgSts).OrderBy(i => i.ImgOrdem).FirstOrDefault();
            var imageUrl = imagemPrincipal != null
                ? $"{baseUrl}/api/imagens/file?path={Uri.EscapeDataString(imagemPrincipal.ImgCaminho)}"
                : "";

            var legenda = MontarLegenda(veiculo, loja, linkVeiculo);

            try
            {
                // Post com foto principal: POST /{page-id}/photos com url=... e message=...
                // (Facebook publica como post de foto no feed da Page automaticamente.)
                var url = $"https://graph.facebook.com/{_settings.ApiVersion}/{pageId}/photos";
                var payload = new
                {
                    url = imageUrl,
                    message = legenda,
                    access_token = token
                };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var resp = await _httpClient.PostAsync(url, content);
                var body = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                    _logger.LogWarning("Facebook Page post veiculo {VeiculoId} falhou: {Body}", veiculoId, body);
                else
                    _logger.LogInformation("Facebook Page post veiculo {VeiculoId}: OK", veiculoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao postar veiculo {VeiculoId} na Facebook Page", veiculoId);
            }
        }

        // Legenda otimizada pra engajamento no Facebook: emoji + titulo + dados + link + hashtags.
        // Fica em torno de 400-500 chars (FB tolera ate 63K).
        private static string MontarLegenda(Veiculo veiculo, Loja? loja, string linkVeiculo)
        {
            var sb = new StringBuilder();
            sb.Append("🚗 ").Append(veiculo.VeiMarca).Append(' ').Append(veiculo.VeiModelo)
              .Append(' ').Append(veiculo.VeiAno).Append('\n');
            sb.Append("💰 R$ ").Append(veiculo.VeiPreco.ToString("N2", new CultureInfo("pt-BR"))).Append('\n');
            if (veiculo.VeiKm > 0)
                sb.Append("📊 ").Append(veiculo.VeiKm.ToString("N0", new CultureInfo("pt-BR"))).Append(" km\n");
            if (!string.IsNullOrWhiteSpace(veiculo.VeiCor))
                sb.Append("🎨 Cor: ").Append(veiculo.VeiCor).Append('\n');
            if (!string.IsNullOrWhiteSpace(veiculo.VeiOpcionais))
            {
                sb.Append("✨ ").Append(veiculo.VeiOpcionais.Replace(",", " · ").TrimEnd(' ', '·')).Append('\n');
            }
            sb.Append('\n');
            sb.Append("📞 Entre em contato pelo link: ").Append(linkVeiculo).Append('\n');
            if (loja != null && !string.IsNullOrWhiteSpace(loja.LojWhatsApp))
                sb.Append("📲 WhatsApp: ").Append(loja.LojWhatsApp).Append('\n');
            sb.Append('\n');
            sb.Append("#").Append(SanitizeTag(veiculo.VeiMarca)).Append(' ');
            sb.Append("#").Append(SanitizeTag(veiculo.VeiMarca + veiculo.VeiModelo)).Append(' ');
            sb.Append("#carros #usados #seminovos");
            if (loja != null && !string.IsNullOrWhiteSpace(loja.LojCidade))
                sb.Append(" #").Append(SanitizeTag(loja.LojCidade));
            return sb.ToString();
        }

        private static string SanitizeTag(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "carros";
            var sb = new StringBuilder();
            foreach (var c in raw)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            }
            return sb.Length == 0 ? "carros" : sb.ToString();
        }

        private async Task<bool> AutoPostHabilitadoAsync()
        {
            var v = await _configRepository.GetValorAsync(KEY_AUTO_POST);
            return string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<(string token, string pageId)> ResolvePageCredentialsAsync()
        {
            var pageId = await _configRepository.GetValorAsync(MetaOAuthService.KEY_PAGE_ID) ?? "";
            var cifrado = await _configRepository.GetValorAsync(MetaOAuthService.KEY_PAGE_TOKEN_CIFRADO);
            if (string.IsNullOrEmpty(cifrado)) return ("", pageId);
            try { return (_tokenProtector.Unprotect(cifrado), pageId); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Meta Page token nao pode ser decifrado (chave rotacionada?)");
                return ("", pageId);
            }
        }

        private static string? NormalizeBaseUrl(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var trimmed = raw.Trim().TrimEnd('/');
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                if (!Uri.TryCreate("https://" + trimmed, UriKind.Absolute, out uri))
                    return null;
            }
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
            if (string.IsNullOrWhiteSpace(uri.Host)) return null;
            var port = uri.IsDefaultPort ? "" : ":" + uri.Port;
            return $"{uri.Scheme}://{uri.Host}{port}";
        }
    }
}
