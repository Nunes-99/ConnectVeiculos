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
                 RefreshTokenDefinido = !string.IsNullOrEmpty(refreshToken),
                 VehicleAdsHabilitado = await VehicleAdsHabilitadoAsync()
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
             await _configRepository.SetValorAsync(KEY_VEHICLE_ADS_HABILITADO, "");
            _cachedAccessToken = null;
            _tokenExpiration = DateTime.MinValue;
        }

         public async Task SetVehicleAdsHabilitadoAsync(bool habilitado)
         {
             await _configRepository.SetValorAsync(KEY_VEHICLE_ADS_HABILITADO, habilitado ? "true" : "false");
             _logger.LogInformation("Vehicle Ads {Status} pelo operador.", habilitado ? "HABILITADO" : "DESABILITADO");
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

         // Flag explicita por tenant: 'GOOGLE_VEHICLE_ADS_HABILITADO'. Quando 'false'
         // (default), as chamadas Push API sao puladas pra evitar dezenas de produtos
         // reprovados poluindo o Merchant Center (Google rejeita TODO veiculo motorizado
         // sob o programa Shopping comum — exige inscricao manual no Vehicle Ads).
         // Usuario liga essa flag depois que Vehicle Ads for aprovado pelo Google.
         private const string KEY_VEHICLE_ADS_HABILITADO = "GOOGLE_VEHICLE_ADS_HABILITADO";

         private async Task<bool> VehicleAdsHabilitadoAsync()
         {
             var valor = await _configRepository.GetValorAsync(KEY_VEHICLE_ADS_HABILITADO);
             return string.Equals(valor, "true", StringComparison.OrdinalIgnoreCase);
         }

        public async Task PublicarVeiculoAsync(int veiculoId)
        {
            var (clientId, clientSecret, refreshToken, merchantId) = await ResolveAsync();
            if (string.IsNullOrEmpty(merchantId) || string.IsNullOrEmpty(refreshToken)) return;

             // Skip se Vehicle Ads nao habilitado. Sem isso, Google rejeita TODO veiculo
             // como "produto nao aceito no Shopping" — pollui o Merchant Center sem
             // beneficio. Quando o programa for aprovado pelo Google, usuario liga a
             // flag em Integracoes > Google e a publicacao volta a rodar.
             if (!await VehicleAdsHabilitadoAsync())
             {
                 _logger.LogInformation(
                     "Google publicar veiculo {VeiculoId} pulado: Vehicle Ads nao habilitado. " +
                     "Inscreva-se em Merchant Center > Crescimento > Anuncios de veiculos e " +
                     "ligue a flag em Integracoes apos aprovacao do Google.", veiculoId);
                 return;
             }

            var token = await EnsureTokenAsync(clientId, clientSecret, refreshToken);
            if (string.IsNullOrEmpty(token)) return;

            var veiculo = await _veiculoRepository.GetByIdAsync(veiculoId);
            if (veiculo == null) return;

            var imagens = await _imagemRepository.GetByVeiculoIdAsync(veiculoId);
            var loja = await _lojaRepository.GetByIdAsync(veiculo.R_LojId);
            // Tenta primeiro a URL especifica da loja; cai pro PublicSiteUrl global do appsettings/env
            // (GoogleMerchantSettings__PublicSiteUrl) se ausente. Frontend nao expoe LojUrlCatalogo
            // na UI atualmente, entao o fallback evita que o push aborte por configuracao de loja.
            var baseUrl = NormalizeBaseUrl(loja?.LojUrlCatalogo) ?? NormalizeBaseUrl(_settings?.PublicSiteUrl);
            if (baseUrl == null)
            {
                _logger.LogWarning(
                    "Google publicar veiculo {VeiculoId} abortado: nem LojUrlCatalogo (loja {LojaId}: '{Url}') nem GoogleMerchantSettings.PublicSiteUrl ('{Fallback}') sao URLs validas. Configure uma das duas.",
                    veiculoId, veiculo.R_LojId, loja?.LojUrlCatalogo, _settings?.PublicSiteUrl);
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
                 description = MontarDescricaoRica(veiculo, loja),
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

         // Descricao rica pro Vehicle Ads. Google reprova descricoes muito curtas
         // ou genericas ("Marca Modelo Ano, Cor, X km"). Esta versao inclui:
         // marca/modelo/ano destacado, km formatado, cor, opcionais detalhados,
         // observacao do operador e dados da loja — passa dos 200 chars facilmente,
         // bem dentro do limite de 5000.
         private static string MontarDescricaoRica(Core.Entities.Veiculos.Veiculo veiculo, Core.Entities.Lojas.Loja? loja)
         {
             var sb = new System.Text.StringBuilder();
             sb.Append(veiculo.VeiMarca).Append(' ').Append(veiculo.VeiModelo).Append(' ').Append(veiculo.VeiAno);
             sb.Append(" usado, em otimo estado de conservacao. ");
             if (!string.IsNullOrWhiteSpace(veiculo.VeiCor))
                 sb.Append("Cor: ").Append(veiculo.VeiCor).Append(". ");
             if (veiculo.VeiKm > 0)
                 sb.Append("Quilometragem: ").Append(veiculo.VeiKm.ToString("N0", new System.Globalization.CultureInfo("pt-BR"))).Append(" km. ");
             if (!string.IsNullOrWhiteSpace(veiculo.VeiOpcionais))
             {
                 sb.Append("Opcionais: ");
                 sb.Append(veiculo.VeiOpcionais.Replace(",", ", ").TrimEnd(' ', ','));
                 sb.Append(". ");
             }
             if (!string.IsNullOrWhiteSpace(veiculo.VeiObservacao))
                 sb.Append(veiculo.VeiObservacao).Append(' ');

             sb.Append("Veiculo disponivel para test drive e financiamento. ");
             if (loja != null)
             {
                 sb.Append("Anunciado por ").Append(loja.LojNome);
                 if (!string.IsNullOrWhiteSpace(loja.LojCidade))
                     sb.Append(" em ").Append(loja.LojCidade).Append('/').Append(loja.LojEstado);
                 if (!string.IsNullOrWhiteSpace(loja.LojWhatsApp))
                     sb.Append(". WhatsApp: ").Append(loja.LojWhatsApp);
                 sb.Append('.');
             }
             return sb.ToString().Trim();
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
