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
    public class InstagramPostService : IInstagramPostService
    {
        public const string KEY_AUTO_POST = "META_IG_AUTO_POST";

        // Instagram Carrossel: minimo 2, maximo 10 itens. Single Photo se for 1.
        private const int MaxImagensCarrossel = 10;

        private readonly HttpClient _httpClient;
        private readonly MetaSettings _settings;
        private readonly IConfiguracaoSistemaRepository _configRepository;
        private readonly ITokenProtector _tokenProtector;
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly IVeiculoImagemRepository _imagemRepository;
        private readonly ILojaRepository _lojaRepository;
        private readonly ILogger<InstagramPostService> _logger;

        public InstagramPostService(
            HttpClient httpClient,
            IOptions<MetaSettings> settings,
            IConfiguracaoSistemaRepository configRepository,
            ITokenProtector tokenProtector,
            IVeiculoRepository veiculoRepository,
            IVeiculoImagemRepository imagemRepository,
            ILojaRepository lojaRepository,
            ILogger<InstagramPostService> logger)
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
            var (token, igId) = await ResolveAsync();
            return !string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(igId);
        }

        public async Task<InstagramPostConfigInfo> GetConfigAsync()
        {
            var (token, igId) = await ResolveAsync();
            var username = await _configRepository.GetValorAsync(MetaOAuthService.KEY_IG_USERNAME);
            return new InstagramPostConfigInfo
            {
                InstagramConectado = !string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(igId),
                BusinessAccountId = string.IsNullOrEmpty(igId) ? null : igId,
                Username = string.IsNullOrEmpty(username) ? null : username,
                AutoPostHabilitado = await AutoPostHabilitadoAsync()
            };
        }

        public async Task SetAutoPostHabilitadoAsync(bool habilitado)
        {
            await _configRepository.SetValorAsync(KEY_AUTO_POST, habilitado ? "true" : "false");
            _logger.LogInformation("Instagram auto-post {Status} pelo operador.", habilitado ? "HABILITADO" : "DESABILITADO");
        }

        public async Task<TestIntegracaoResult> TestarAsync()
        {
            var (token, igId) = await ResolveAsync();
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(igId))
                return new TestIntegracaoResult { Sucesso = false, Mensagem = "Instagram Business nao configurado. Selecione uma Page Facebook com IG Business vinculado." };

            try
            {
                var url = $"https://graph.facebook.com/{_settings.ApiVersion}/{igId}?fields=username,followers_count&access_token={Uri.EscapeDataString(token)}";
                var resp = await _httpClient.GetAsync(url);
                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    return new TestIntegracaoResult { Sucesso = false, Mensagem = $"Falha ({(int)resp.StatusCode}): {body}" };

                using var doc = JsonDocument.Parse(body);
                var username = doc.RootElement.TryGetProperty("username", out var u) ? u.GetString() : "";
                var followers = doc.RootElement.TryGetProperty("followers_count", out var f) ? f.GetInt32() : 0;
                return new TestIntegracaoResult { Sucesso = true, Mensagem = $"Conta @{username} OK. {followers} seguidor(es)." };
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
                _logger.LogDebug("Instagram auto-post desabilitado, pulando veiculo {VeiculoId}", veiculoId);
                return;
            }

            var (token, igId) = await ResolveAsync();
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(igId))
            {
                _logger.LogDebug("Instagram Business nao configurado, pulando veiculo {VeiculoId}", veiculoId);
                return;
            }

            var veiculo = await _veiculoRepository.GetByIdAsync(veiculoId);
            if (veiculo == null) return;

            var imagens = (await _imagemRepository.GetByVeiculoIdAsync(veiculoId))
                .Where(i => i.ImgSts)
                .OrderBy(i => i.ImgOrdem)
                .ToList();
            if (imagens.Count == 0)
            {
                _logger.LogInformation("Instagram post veiculo {VeiculoId} pulado: sem imagens.", veiculoId);
                return;
            }

            var loja = await _lojaRepository.GetByIdAsync(veiculo.R_LojId);
            var baseUrl = NormalizeBaseUrl(loja?.LojUrlCatalogo) ?? NormalizeBaseUrl(_settings?.PublicSiteUrl);
            if (baseUrl == null)
            {
                _logger.LogWarning("Instagram post abortado: sem URL publica configurada (veiculo {VeiculoId})", veiculoId);
                return;
            }

            // IG so aceita HTTPS. Em dev (http://localhost) o post sera rejeitado.
            if (!baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Instagram post abortado: URL publica precisa ser HTTPS (recebido '{Url}')", baseUrl);
                return;
            }

            var slug = loja?.LojSlug ?? veiculo.R_LojId.ToString();
            var imageUrls = imagens
                .Take(MaxImagensCarrossel)
                .Select(i => $"{baseUrl}/api/imagens/file?path={Uri.EscapeDataString(i.ImgCaminho)}")
                .ToList();

            var legenda = MontarLegenda(veiculo, loja, baseUrl, slug);

            try
            {
                if (imageUrls.Count == 1)
                    await PublicarFotoUnicaAsync(igId, token, imageUrls[0], legenda, veiculoId);
                else
                    await PublicarCarrosselAsync(igId, token, imageUrls, legenda, veiculoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao postar veiculo {VeiculoId} no Instagram", veiculoId);
            }
        }

        private async Task PublicarFotoUnicaAsync(string igId, string token, string imageUrl, string caption, int veiculoId)
        {
            var creationId = await CreateImageContainerAsync(igId, token, imageUrl, caption, isCarouselItem: false);
            if (string.IsNullOrEmpty(creationId))
            {
                _logger.LogWarning("Instagram foto unica veiculo {VeiculoId}: falha ao criar container", veiculoId);
                return;
            }
            await PublishContainerAsync(igId, token, creationId, veiculoId);
        }

        private async Task PublicarCarrosselAsync(string igId, string token, List<string> imageUrls, string caption, int veiculoId)
        {
            // 1. Cria 1 container por imagem (is_carousel_item=true).
            var childIds = new List<string>();
            foreach (var imgUrl in imageUrls)
            {
                var id = await CreateImageContainerAsync(igId, token, imgUrl, caption: null, isCarouselItem: true);
                if (!string.IsNullOrEmpty(id)) childIds.Add(id);
            }
            if (childIds.Count < 2)
            {
                _logger.LogWarning("Instagram carrossel veiculo {VeiculoId}: menos de 2 itens validos, abortando", veiculoId);
                return;
            }

            // 2. Cria container do carrossel referenciando os children.
            var url = $"https://graph.facebook.com/{_settings.ApiVersion}/{igId}/media";
            var payload = new Dictionary<string, string>
            {
                ["media_type"] = "CAROUSEL",
                ["children"] = string.Join(",", childIds),
                ["caption"] = caption,
                ["access_token"] = token
            };
            var resp = await _httpClient.PostAsync(url, new FormUrlEncodedContent(payload));
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Instagram carrossel container veiculo {VeiculoId} falhou: {Body}", veiculoId, body);
                return;
            }
            using var doc = JsonDocument.Parse(body);
            var carrosselId = doc.RootElement.GetProperty("id").GetString() ?? "";

            // 3. Publica o carrossel.
            await PublishContainerAsync(igId, token, carrosselId, veiculoId);
        }

        private async Task<string?> CreateImageContainerAsync(string igId, string token, string imageUrl, string? caption, bool isCarouselItem)
        {
            var url = $"https://graph.facebook.com/{_settings.ApiVersion}/{igId}/media";
            var payload = new Dictionary<string, string>
            {
                ["image_url"] = imageUrl,
                ["access_token"] = token
            };
            if (!string.IsNullOrEmpty(caption)) payload["caption"] = caption;
            if (isCarouselItem) payload["is_carousel_item"] = "true";

            var resp = await _httpClient.PostAsync(url, new FormUrlEncodedContent(payload));
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Instagram criar container falhou: {Body}", body);
                return null;
            }
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
        }

        private async Task PublishContainerAsync(string igId, string token, string creationId, int veiculoId)
        {
            var url = $"https://graph.facebook.com/{_settings.ApiVersion}/{igId}/media_publish";
            var payload = new Dictionary<string, string>
            {
                ["creation_id"] = creationId,
                ["access_token"] = token
            };
            var resp = await _httpClient.PostAsync(url, new FormUrlEncodedContent(payload));
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                _logger.LogWarning("Instagram publish veiculo {VeiculoId} falhou: {Body}", veiculoId, body);
            else
                _logger.LogInformation("Instagram publish veiculo {VeiculoId}: OK", veiculoId);
        }

        // Legenda IG: similar ao FB mas sem link clicavel (IG nao permite links em
        // captions do feed). Substitui por "link na bio" + nome do veiculo.
        private static string MontarLegenda(Veiculo veiculo, Loja? loja, string baseUrl, string slug)
        {
            var sb = new StringBuilder();
            sb.Append("🚗 ").Append(veiculo.VeiMarca).Append(' ').Append(veiculo.VeiModelo)
              .Append(' ').Append(veiculo.VeiAno).Append('\n');
            sb.Append("💰 R$ ").Append(veiculo.VeiPreco.ToString("N2", new CultureInfo("pt-BR"))).Append('\n');
            if (veiculo.VeiKm > 0)
                sb.Append("📊 ").Append(veiculo.VeiKm.ToString("N0", new CultureInfo("pt-BR"))).Append(" km\n");
            if (!string.IsNullOrWhiteSpace(veiculo.VeiCor))
                sb.Append("🎨 ").Append(veiculo.VeiCor).Append('\n');
            if (!string.IsNullOrWhiteSpace(veiculo.VeiOpcionais))
            {
                sb.Append("✨ ").Append(veiculo.VeiOpcionais.Replace(",", " · ").TrimEnd(' ', '·')).Append('\n');
            }
            sb.Append('\n');
            sb.Append("👉 Link na bio para mais detalhes!").Append('\n');
            if (loja != null && !string.IsNullOrWhiteSpace(loja.LojWhatsApp))
                sb.Append("📲 WhatsApp: ").Append(loja.LojWhatsApp).Append('\n');
            sb.Append('\n');
            sb.Append("#").Append(SanitizeTag(veiculo.VeiMarca)).Append(' ');
            sb.Append("#").Append(SanitizeTag(veiculo.VeiMarca + veiculo.VeiModelo)).Append(' ');
            sb.Append("#carros #usados #seminovos #carrosanovenda");
            if (loja != null && !string.IsNullOrWhiteSpace(loja.LojCidade))
                sb.Append(" #").Append(SanitizeTag(loja.LojCidade)).Append(" #").Append(SanitizeTag("carros" + loja.LojCidade));
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

        private async Task<(string token, string igId)> ResolveAsync()
        {
            var igId = await _configRepository.GetValorAsync(MetaOAuthService.KEY_IG_BUSINESS_ID) ?? "";
            var cifrado = await _configRepository.GetValorAsync(MetaOAuthService.KEY_PAGE_TOKEN_CIFRADO);
            if (string.IsNullOrEmpty(cifrado)) return ("", igId);
            try { return (_tokenProtector.Unprotect(cifrado), igId); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Meta Page token (usado pelo IG) nao pode ser decifrado");
                return ("", igId);
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
