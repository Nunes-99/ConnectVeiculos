using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.Infrastructure.Services.Google
{
    public class GoogleMerchantService : IGoogleMerchantService
    {
        private const string KEY_CLIENT_ID = "GOOGLE_MERCHANT_CLIENT_ID";
        private const string KEY_CLIENT_SECRET = "GOOGLE_MERCHANT_CLIENT_SECRET";
        private const string KEY_REFRESH = "GOOGLE_MERCHANT_REFRESH_TOKEN";
        private const string KEY_MERCHANT = "GOOGLE_MERCHANT_ID";
        private const string KEY_ACCESS = "GOOGLE_MERCHANT_ACCESS_TOKEN";

        private readonly HttpClient _httpClient;
        private readonly GoogleMerchantSettings _settings;
        private readonly ILogger<GoogleMerchantService> _logger;
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly IVeiculoImagemRepository _imagemRepository;
        private readonly ILojaRepository _lojaRepository;
        private readonly IConfiguracaoSistemaRepository _configRepository;

        private string? _cachedAccessToken;
        private DateTime _tokenExpiration;

        public GoogleMerchantService(
            HttpClient httpClient,
            IOptions<GoogleMerchantSettings> settings,
            ILogger<GoogleMerchantService> logger,
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
        private async Task<(string clientId, string clientSecret, string refreshToken, string merchantId)> ResolveAsync()
        {
            var clientId = Environment.GetEnvironmentVariable("GOOGLE_MERCHANT_CLIENT_ID")
                ?? await _configRepository.GetValorAsync(KEY_CLIENT_ID)
                ?? _settings.ClientId ?? "";
            var clientSecret = Environment.GetEnvironmentVariable("GOOGLE_MERCHANT_CLIENT_SECRET")
                ?? await _configRepository.GetValorAsync(KEY_CLIENT_SECRET)
                ?? _settings.ClientSecret ?? "";
            var refreshToken = Environment.GetEnvironmentVariable("GOOGLE_MERCHANT_REFRESH_TOKEN")
                ?? await _configRepository.GetValorAsync(KEY_REFRESH)
                ?? _settings.RefreshToken ?? "";
            var merchantId = Environment.GetEnvironmentVariable("GOOGLE_MERCHANT_ID")
                ?? await _configRepository.GetValorAsync(KEY_MERCHANT)
                ?? _settings.MerchantId ?? "";

            return (clientId, clientSecret, refreshToken, merchantId);
        }

        public async Task<bool> IsConfiguredAsync()
        {
            var (clientId, clientSecret, refreshToken, merchantId) = await ResolveAsync();
            return !string.IsNullOrEmpty(clientId)
                && !string.IsNullOrEmpty(clientSecret)
                && !string.IsNullOrEmpty(refreshToken)
                && !string.IsNullOrEmpty(merchantId);
        }

        public async Task<GoogleMerchantConfigInfo> GetConfigAsync()
        {
            var (clientId, clientSecret, refreshToken, merchantId) = await ResolveAsync();
            return new GoogleMerchantConfigInfo
            {
                Configurado = !string.IsNullOrEmpty(clientId)
                    && !string.IsNullOrEmpty(clientSecret)
                    && !string.IsNullOrEmpty(refreshToken)
                    && !string.IsNullOrEmpty(merchantId),
                MerchantId = merchantId,
                ClientId = clientId,
                ClientSecretDefinido = !string.IsNullOrEmpty(clientSecret),
                RefreshTokenDefinido = !string.IsNullOrEmpty(refreshToken)
            };
        }

        public async Task SalvarConfigAsync(GoogleMerchantConfigInput input)
        {
            if (!string.IsNullOrWhiteSpace(input.ClientId))
                await _configRepository.SetValorAsync(KEY_CLIENT_ID, input.ClientId.Trim());
            if (!string.IsNullOrWhiteSpace(input.ClientSecret))
                await _configRepository.SetValorAsync(KEY_CLIENT_SECRET, input.ClientSecret.Trim());
            if (!string.IsNullOrWhiteSpace(input.RefreshToken))
                await _configRepository.SetValorAsync(KEY_REFRESH, input.RefreshToken.Trim());
            if (!string.IsNullOrWhiteSpace(input.MerchantId))
                await _configRepository.SetValorAsync(KEY_MERCHANT, input.MerchantId.Trim());

            // Forca renovacao do access token na proxima chamada
            _cachedAccessToken = null;
            _tokenExpiration = DateTime.MinValue;
            await _configRepository.SetValorAsync(KEY_ACCESS, "");
        }

        public async Task DesconectarAsync()
        {
            await _configRepository.SetValorAsync(KEY_CLIENT_ID, "");
            await _configRepository.SetValorAsync(KEY_CLIENT_SECRET, "");
            await _configRepository.SetValorAsync(KEY_REFRESH, "");
            await _configRepository.SetValorAsync(KEY_MERCHANT, "");
            await _configRepository.SetValorAsync(KEY_ACCESS, "");
            _cachedAccessToken = null;
            _tokenExpiration = DateTime.MinValue;
        }

        public async Task<TestIntegracaoResult> TestarAsync()
        {
            var (clientId, clientSecret, refreshToken, merchantId) = await ResolveAsync();
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(merchantId))
                return new TestIntegracaoResult { Sucesso = false, Mensagem = "Configuracao incompleta. Informe ClientId, ClientSecret, RefreshToken e MerchantId." };

            try
            {
                var token = await EnsureTokenAsync(clientId, clientSecret, refreshToken);
                if (string.IsNullOrEmpty(token))
                    return new TestIntegracaoResult { Sucesso = false, Mensagem = "Falha ao obter access_token via refresh_token." };

                var url = $"https://shoppingcontent.googleapis.com/content/v2.1/{merchantId}/accounts/{merchantId}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    return new TestIntegracaoResult { Sucesso = false, Mensagem = $"Falha ({(int)response.StatusCode}): {body}" };

                using var doc = JsonDocument.Parse(body);
                var nome = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() : merchantId;
                return new TestIntegracaoResult { Sucesso = true, Mensagem = $"Conta '{nome}' acessada com sucesso." };
            }
            catch (Exception ex)
            {
                return new TestIntegracaoResult { Sucesso = false, Mensagem = ex.Message };
            }
        }

        public async Task PublicarVeiculoAsync(int veiculoId)
        {
            var (clientId, clientSecret, refreshToken, merchantId) = await ResolveAsync();
            if (string.IsNullOrEmpty(merchantId) || string.IsNullOrEmpty(refreshToken)) return;

            var token = await EnsureTokenAsync(clientId, clientSecret, refreshToken);
            if (string.IsNullOrEmpty(token)) return;

            var veiculo = await _veiculoRepository.GetByIdAsync(veiculoId);
            if (veiculo == null) return;

            var imagens = await _imagemRepository.GetByVeiculoIdAsync(veiculoId);
            var loja = await _lojaRepository.GetByIdAsync(veiculo.R_LojId);
            var baseUrl = NormalizeBaseUrl(loja?.LojUrlCatalogo);
            if (baseUrl == null)
            {
                _logger.LogWarning(
                    "Google publicar veiculo {VeiculoId} abortado: LojUrlCatalogo da loja {LojaId} invalida ('{Url}'). Configure como https://dominio.com em Lojas > Editar.",
                    veiculoId, veiculo.R_LojId, loja?.LojUrlCatalogo);
                return;
            }
            var slug = loja?.LojSlug ?? veiculo.R_LojId.ToString();

            var imagemPrincipal = imagens.Where(i => i.ImgSts).OrderBy(i => i.ImgOrdem).FirstOrDefault();
            var imageUrl = imagemPrincipal != null
                ? $"{baseUrl}/api/imagens/file?path={Uri.EscapeDataString(imagemPrincipal.ImgCaminho)}"
                : "";

            // Vehicle Ads (Performance Max for vehicles), nao Shopping comum.
            // Shopping rejeita veiculos motorizados; precisamos excluir Shopping_ads/Free_listings
            // e enviar os atributos vehicle_* via customAttributes pra elegibilidade em Vehicle Ads.
            // Pre-requisitos no Merchant Center do cliente: programa Vehicle Ads habilitado + campanha
            // Performance Max for Vehicle Ads no Google Ads vinculado.
            var vehicleId = !string.IsNullOrWhiteSpace(veiculo.VeiChassi) ? veiculo.VeiChassi : veiculoId.ToString();

            var product = new
            {
                offerId = veiculoId.ToString(),
                title = $"{veiculo.VeiMarca} {veiculo.VeiModelo} {veiculo.VeiAno}",
                description = $"{veiculo.VeiMarca} {veiculo.VeiModelo} {veiculo.VeiAno}, {veiculo.VeiCor}, {veiculo.VeiKm:N0} km. {loja?.LojNome}",
                link = $"{baseUrl}/catalogo/{slug}/veiculo/{veiculoId}",
                imageLink = imageUrl,
                contentLanguage = "pt",
                targetCountry = "BR",
                channel = "online",
                availability = "in stock",
                condition = "used",
                brand = veiculo.VeiMarca,
                price = new { value = veiculo.VeiPreco.ToString("F2"), currency = "BRL" },
                productTypes = new[] { "Vehicles & Parts > Vehicles > Motor Vehicles > Cars & Trucks" },
                excludedDestinations = new[] { "Shopping_ads", "Free_listings", "Free_local_listings" },
                customAttributes = new object[]
                {
                    new { name = "vehicle_id", value = vehicleId },
                    new { name = "vehicle_make", value = veiculo.VeiMarca },
                    new { name = "vehicle_model", value = veiculo.VeiModelo },
                    new { name = "vehicle_model_year", value = veiculo.VeiAno.ToString() },
                    new { name = "vehicle_condition", value = "USED" },
                    new { name = "vehicle_color", value = veiculo.VeiCor ?? "" },
                    new { name = "mileage_value", value = veiculo.VeiKm.ToString() },
                    new { name = "mileage_unit", value = "KM" }
                }
            };

            try
            {
                var url = $"https://shoppingcontent.googleapis.com/content/v2.1/{merchantId}/products";
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                var json = JsonSerializer.Serialize(product);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Google publicar veiculo {VeiculoId} falhou: {Response}", veiculoId, body);
                }
                else
                {
                    _logger.LogInformation("Google publicar veiculo {VeiculoId}: OK", veiculoId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao publicar veiculo {VeiculoId} no Google", veiculoId);
            }
        }

        public async Task RemoverVeiculoAsync(int veiculoId)
        {
            var (clientId, clientSecret, refreshToken, merchantId) = await ResolveAsync();
            if (string.IsNullOrEmpty(merchantId) || string.IsNullOrEmpty(refreshToken)) return;

            var token = await EnsureTokenAsync(clientId, clientSecret, refreshToken);
            if (string.IsNullOrEmpty(token)) return;

            try
            {
                var productId = $"online:pt:BR:{veiculoId}";
                var url = $"https://shoppingcontent.googleapis.com/content/v2.1/{merchantId}/products/{productId}";
                var request = new HttpRequestMessage(HttpMethod.Delete, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Google remover veiculo {VeiculoId} falhou: {Response}", veiculoId, body);
                }
                else
                {
                    _logger.LogInformation("Google remover veiculo {VeiculoId}: OK", veiculoId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao remover veiculo {VeiculoId} do Google", veiculoId);
            }
        }

        // Sanitiza LojUrlCatalogo antes de enviar pro Google.
        // Aceita "https://site.com", "site.com" (https auto), "http://localhost:5219".
        // Rejeita vazio, "http:", "http:/" e qualquer URI sem host.
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

        private async Task<string?> EnsureTokenAsync(string clientId, string clientSecret, string refreshToken)
        {
            if (!string.IsNullOrEmpty(_cachedAccessToken) && DateTime.UtcNow < _tokenExpiration)
                return _cachedAccessToken;

            var stored = await _configRepository.GetValorAsync(KEY_ACCESS);
            if (!string.IsNullOrEmpty(stored) && DateTime.UtcNow < _tokenExpiration)
                return stored;

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(refreshToken))
                return null;

            try
            {
                var body = new
                {
                    client_id = clientId,
                    client_secret = clientSecret,
                    refresh_token = refreshToken,
                    grant_type = "refresh_token"
                };
                var response = await _httpClient.PostAsJsonAsync("https://oauth2.googleapis.com/token", body);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Google refresh_token falhou: {Body}", err);
                    return null;
                }

                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                _cachedAccessToken = result.GetProperty("access_token").GetString();
                var expiresIn = result.GetProperty("expires_in").GetInt32();
                _tokenExpiration = DateTime.UtcNow.AddSeconds(expiresIn - 60);

                if (!string.IsNullOrEmpty(_cachedAccessToken))
                    await _configRepository.SetValorAsync(KEY_ACCESS, _cachedAccessToken);

                return _cachedAccessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao renovar token Google Merchant");
                return null;
            }
        }
    }
}
