using System.Text;
using System.Text.Json;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.Infrastructure.Services.Facebook
{
    public class FacebookCatalogService : IFacebookCatalogService
    {
        private const string KEY_TOKEN = "FB_ACCESS_TOKEN";
        private const string KEY_CATALOG = "FB_CATALOG_ID";
        private const string KEY_VERSION = "FB_API_VERSION";
        // Flag de auto-publicacao no Catalog. Quando 'false', PublicarVeiculoAsync
        // pula a Push API (mesmo se Catalog estiver configurado) — util quando o
        // tenant nao quer pagar Vehicle Ads ainda. Default false ate o tenant ligar.
        private const string KEY_AUTO_POST = "FB_AUTO_POST";

        private readonly HttpClient _httpClient;
        private readonly FacebookCatalogSettings _settings;
        private readonly ILogger<FacebookCatalogService> _logger;
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly IVeiculoImagemRepository _imagemRepository;
        private readonly ILojaRepository _lojaRepository;
        private readonly IConfiguracaoSistemaRepository _configRepository;

        public FacebookCatalogService(
            HttpClient httpClient,
            IOptions<FacebookCatalogSettings> settings,
            ILogger<FacebookCatalogService> logger,
            IVeiculoRepository veiculoRepository,
            IVeiculoImagemRepository imagemRepository,
            ILojaRepository lojaRepository,
            IConfiguracaoSistemaRepository configRepository)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
            _veiculoRepository = veiculoRepository;
            _imagemRepository = imagemRepository;
            _lojaRepository = lojaRepository;
            _configRepository = configRepository;
        }

        // Precedencia: env var > database (ConfiguracaoSistema) > appsettings.json
        private async Task<(string token, string catalogId, string apiVersion)> ResolveAsync()
        {
            var envToken = Environment.GetEnvironmentVariable("FB_ACCESS_TOKEN");
            var envCatalog = Environment.GetEnvironmentVariable("FB_CATALOG_ID");
            var envVersion = Environment.GetEnvironmentVariable("FB_API_VERSION");

            var token = !string.IsNullOrEmpty(envToken)
                ? envToken
                : (await _configRepository.GetValorAsync(KEY_TOKEN)) ?? _settings.AccessToken ?? "";

            var catalogId = !string.IsNullOrEmpty(envCatalog)
                ? envCatalog
                : (await _configRepository.GetValorAsync(KEY_CATALOG)) ?? _settings.CatalogId ?? "";

            var apiVersion = !string.IsNullOrEmpty(envVersion)
                ? envVersion
                : (await _configRepository.GetValorAsync(KEY_VERSION))
                  ?? (string.IsNullOrEmpty(_settings.ApiVersion) ? "v18.0" : _settings.ApiVersion);

            return (token, catalogId, apiVersion);
        }

        public async Task<bool> IsConfiguredAsync()
        {
            var (token, catalogId, _) = await ResolveAsync();
            return !string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(catalogId);
        }

        public async Task<FacebookConfigInfo> GetConfigAsync()
        {
            var (token, catalogId, apiVersion) = await ResolveAsync();
            return new FacebookConfigInfo
            {
                Configurado = !string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(catalogId),
                CatalogId = catalogId,
                ApiVersion = apiVersion,
                TokenDefinido = !string.IsNullOrEmpty(token),
                AutoPostHabilitado = await AutoPostHabilitadoAsync()
            };
        }

        public async Task SetAutoPostHabilitadoAsync(bool habilitado)
        {
            await _configRepository.SetValorAsync(KEY_AUTO_POST, habilitado ? "true" : "false");
            _logger.LogInformation("Facebook Catalog auto-post {Status}", habilitado ? "HABILITADO" : "DESABILITADO");
        }

        private async Task<bool> AutoPostHabilitadoAsync()
        {
            var v = await _configRepository.GetValorAsync(KEY_AUTO_POST);
            return string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
        }

        public async Task SalvarConfigAsync(FacebookConfigInput input)
        {
            if (!string.IsNullOrWhiteSpace(input.AccessToken))
                await _configRepository.SetValorAsync(KEY_TOKEN, input.AccessToken.Trim());
            if (!string.IsNullOrWhiteSpace(input.CatalogId))
                await _configRepository.SetValorAsync(KEY_CATALOG, input.CatalogId.Trim());
            if (!string.IsNullOrWhiteSpace(input.ApiVersion))
                await _configRepository.SetValorAsync(KEY_VERSION, input.ApiVersion.Trim());
        }

        public async Task DesconectarAsync()
        {
            await _configRepository.SetValorAsync(KEY_TOKEN, "");
            await _configRepository.SetValorAsync(KEY_CATALOG, "");
            await _configRepository.SetValorAsync(KEY_VERSION, "");
            await _configRepository.SetValorAsync(KEY_AUTO_POST, "");
        }

        public async Task<TestIntegracaoResult> TestarAsync()
        {
            var (token, catalogId, apiVersion) = await ResolveAsync();
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(catalogId))
                return new TestIntegracaoResult { Sucesso = false, Mensagem = "Configuracao incompleta. Informe AccessToken e CatalogId." };

            try
            {
                var url = $"https://graph.facebook.com/{apiVersion}/{catalogId}?fields=name,vertical,product_count";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new TestIntegracaoResult { Sucesso = false, Mensagem = $"Falha ({(int)response.StatusCode}): {body}" };
                }

                using var doc = JsonDocument.Parse(body);
                var nome = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() : "";
                var prod = doc.RootElement.TryGetProperty("product_count", out var p) ? p.GetInt32().ToString() : "?";
                return new TestIntegracaoResult { Sucesso = true, Mensagem = $"Catalogo '{nome}' OK. {prod} produto(s)." };
            }
            catch (Exception ex)
            {
                return new TestIntegracaoResult { Sucesso = false, Mensagem = ex.Message };
            }
        }

        public async Task PublicarVeiculoAsync(int veiculoId)
        {
            var (token, catalogId, apiVersion) = await ResolveAsync();
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(catalogId)) return;

            // Skip se auto-post desligado. Diferente do Google, o Facebook Catalog
            // aceita veiculos no programa Vehicle Ads do BR — mas o tenant decide
            // ligar quando estiver pronto pra pagar Dynamic Ads for Auto.
            if (!await AutoPostHabilitadoAsync())
            {
                _logger.LogDebug("Facebook Catalog auto-post desligado, pulando veiculo {VeiculoId}", veiculoId);
                return;
            }

            var veiculo = await _veiculoRepository.GetByIdAsync(veiculoId);
            if (veiculo == null) return;

            var imagens = await _imagemRepository.GetByVeiculoIdAsync(veiculoId);
            var loja = await _lojaRepository.GetByIdAsync(veiculo.R_LojId);
            // LojUrlCatalogo nao e expoe na UI atualmente (form control existe sem input
            // correspondente), entao cai pra PublicSiteUrl do env var
            // FacebookCatalogSettings__PublicSiteUrl. Sem fallback valido, aborta o push.
            var baseUrl = NormalizeBaseUrl(loja?.LojUrlCatalogo) ?? NormalizeBaseUrl(_settings?.PublicSiteUrl);
            if (baseUrl == null)
            {
                _logger.LogWarning(
                    "Facebook publicar veiculo {VeiculoId} abortado: nem LojUrlCatalogo (loja {LojaId}: '{Url}') nem FacebookCatalogSettings.PublicSiteUrl ('{Fallback}') sao URLs validas.",
                    veiculoId, veiculo.R_LojId, loja?.LojUrlCatalogo, _settings?.PublicSiteUrl);
                return;
            }
            var slug = loja?.LojSlug ?? veiculo.R_LojId.ToString();

            var imagemPrincipal = imagens.Where(i => i.ImgSts).OrderBy(i => i.ImgOrdem).FirstOrDefault();
            var imageUrl = imagemPrincipal != null
                ? $"{baseUrl}/api/imagens/file?path={Uri.EscapeDataString(imagemPrincipal.ImgCaminho)}"
                : "";

            var batch = new
            {
                requests = new[]
                {
                    new
                    {
                        method = "UPDATE",
                        retailer_id = veiculoId.ToString(),
                        data = new
                        {
                            availability = "in stock",
                            condition = "used",
                            price = $"{veiculo.VeiPreco:F2} BRL",
                            title = $"{veiculo.VeiMarca} {veiculo.VeiModelo} {veiculo.VeiAno}",
                            description = $"{veiculo.VeiMarca} {veiculo.VeiModelo} {veiculo.VeiAno}, {veiculo.VeiCor}, {veiculo.VeiKm:N0} km",
                            image_link = imageUrl,
                            link = $"{baseUrl}/catalogo/{slug}/veiculo/{veiculoId}",
                            brand = veiculo.VeiMarca,
                            vehicle_type = "car",
                            year = veiculo.VeiAno,
                            mileage = new { value = veiculo.VeiKm, unit = "KM" },
                            color = veiculo.VeiCor ?? "",
                            address = new
                            {
                                city = loja?.LojCidade ?? "",
                                region = loja?.LojEstado ?? ""
                            }
                        }
                    }
                }
            };

            await EnviarBatchAsync(batch, veiculoId, "publicar", token, catalogId, apiVersion);
        }

        public async Task RemoverVeiculoAsync(int veiculoId)
        {
            var (token, catalogId, apiVersion) = await ResolveAsync();
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(catalogId)) return;

            var batch = new
            {
                requests = new[]
                {
                    new
                    {
                        method = "DELETE",
                        retailer_id = veiculoId.ToString()
                    }
                }
            };

            await EnviarBatchAsync(batch, veiculoId, "remover", token, catalogId, apiVersion);
        }

        // Sanitiza LojUrlCatalogo / PublicSiteUrl antes de enviar pro Facebook.
        // Aceita "https://site.com", "site.com" (https auto), "http://localhost:PORT".
        // Rejeita vazio, "http:", "http:/" e qualquer URI sem host. Identica ao
        // helper do GoogleMerchantService (manter em sync se mudar).
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

        private async Task EnviarBatchAsync(object batch, int veiculoId, string operacao, string token, string catalogId, string apiVersion)
        {
            try
            {
                var url = $"https://graph.facebook.com/{apiVersion}/{catalogId}/items_batch";
                var json = JsonSerializer.Serialize(batch);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Facebook {Operacao} veiculo {VeiculoId} falhou: {Response}", operacao, veiculoId, responseBody);
                }
                else
                {
                    _logger.LogInformation("Facebook {Operacao} veiculo {VeiculoId}: OK", operacao, veiculoId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao {Operacao} veiculo {VeiculoId} no Facebook", operacao, veiculoId);
            }
        }
    }
}
