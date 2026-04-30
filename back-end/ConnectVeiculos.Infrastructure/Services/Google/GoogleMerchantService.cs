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
        private readonly HttpClient _httpClient;
        private readonly GoogleMerchantSettings _settings;
        private readonly ILogger<GoogleMerchantService> _logger;
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly IVeiculoImagemRepository _imagemRepository;
        private readonly ILojaRepository _lojaRepository;
        private readonly IConfiguracaoSistemaRepository _configRepository;

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

        public bool IsConfigured()
        {
            return !string.IsNullOrEmpty(_settings.MerchantId) &&
                   (!string.IsNullOrEmpty(_settings.AccessToken) || !string.IsNullOrEmpty(_settings.RefreshToken));
        }

        public async Task PublicarVeiculoAsync(int veiculoId)
        {
            if (!IsConfigured()) return;

            await EnsureTokenAsync();

            var veiculo = await _veiculoRepository.GetByIdAsync(veiculoId);
            if (veiculo == null) return;

            var imagens = await _imagemRepository.GetByVeiculoIdAsync(veiculoId);
            var loja = await _lojaRepository.GetByIdAsync(veiculo.R_LojId);
            var baseUrl = loja?.LojUrlCatalogo?.TrimEnd('/') ?? "";
            var slug = loja?.LojSlug ?? veiculo.R_LojId.ToString();

            var imagemPrincipal = imagens.Where(i => i.ImgSts).OrderBy(i => i.ImgOrdem).FirstOrDefault();
            var imageUrl = imagemPrincipal != null
                ? $"{baseUrl}/api/imagens/file?path={Uri.EscapeDataString(imagemPrincipal.ImgCaminho)}"
                : "";

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
                price = new { value = veiculo.VeiPreco.ToString("F2"), currency = "BRL" }
            };

            try
            {
                var url = $"https://shoppingcontent.googleapis.com/content/v2.1/{_settings.MerchantId}/products";
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.AccessToken);
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
            if (!IsConfigured()) return;

            await EnsureTokenAsync();

            try
            {
                var productId = $"online:pt:BR:{veiculoId}";
                var url = $"https://shoppingcontent.googleapis.com/content/v2.1/{_settings.MerchantId}/products/{productId}";
                var request = new HttpRequestMessage(HttpMethod.Delete, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.AccessToken);

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

        private async Task EnsureTokenAsync()
        {
            if (string.IsNullOrEmpty(_settings.AccessToken))
            {
                _settings.AccessToken = await _configRepository.GetValorAsync("GOOGLE_ACCESS_TOKEN");
                _settings.RefreshToken = await _configRepository.GetValorAsync("GOOGLE_REFRESH_TOKEN");
            }

            if (DateTime.UtcNow < _tokenExpiration && !string.IsNullOrEmpty(_settings.AccessToken))
                return;

            if (string.IsNullOrEmpty(_settings.RefreshToken)) return;

            try
            {
                var body = new
                {
                    client_id = _settings.ClientId,
                    client_secret = _settings.ClientSecret,
                    refresh_token = _settings.RefreshToken,
                    grant_type = "refresh_token"
                };
                var response = await _httpClient.PostAsJsonAsync("https://oauth2.googleapis.com/token", body);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                _settings.AccessToken = result.GetProperty("access_token").GetString();
                var expiresIn = result.GetProperty("expires_in").GetInt32();
                _tokenExpiration = DateTime.UtcNow.AddSeconds(expiresIn - 60);

                await _configRepository.SetValorAsync("GOOGLE_ACCESS_TOKEN", _settings.AccessToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao renovar token Google");
            }
        }
    }
}
