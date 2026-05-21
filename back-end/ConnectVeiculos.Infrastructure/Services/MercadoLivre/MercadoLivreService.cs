using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.Infrastructure.Services.MercadoLivre
{
    public class MercadoLivreService : IMercadoLivreService
    {
        private readonly HttpClient _httpClient;
        private readonly MercadoLivreSettings _settings;
        private readonly ILogger<MercadoLivreService> _logger;
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly IVeiculoImagemRepository _imagemRepository;
        private readonly ILojaRepository _lojaRepository;
        private readonly IConfiguracaoSistemaRepository _configRepository;

        private const string BaseUrl = "https://api.mercadolibre.com";
        private const string AuthUrl = "https://auth.mercadolivre.com.br/authorization";

        public MercadoLivreService(
            HttpClient httpClient,
            IOptions<MercadoLivreSettings> settings,
            ILogger<MercadoLivreService> logger,
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
            _httpClient.BaseAddress = new Uri(BaseUrl);
        }

        public string GetAuthUrl()
        {
            if (string.IsNullOrWhiteSpace(_settings.AppId))
                throw new InvalidOperationException("Mercado Livre nao configurado: defina AppId em appsettings ou via env var ML_APP_ID.");
            if (string.IsNullOrWhiteSpace(_settings.RedirectUri))
                throw new InvalidOperationException("Mercado Livre nao configurado: defina RedirectUri em appsettings ou via env var ML_REDIRECT_URI.");

            return $"{AuthUrl}?response_type=code&client_id={_settings.AppId}&redirect_uri={Uri.EscapeDataString(_settings.RedirectUri)}";
        }

        public async Task HandleCallbackAsync(string code)
        {
            var request = new
            {
                grant_type = "authorization_code",
                client_id = _settings.AppId,
                client_secret = _settings.ClientSecret,
                code = code,
                redirect_uri = _settings.RedirectUri
            };

            var response = await _httpClient.PostAsJsonAsync("/oauth/token", request);

            // EnsureSuccessStatusCode lanca HttpRequestException com mensagem
            // generica. Lendo o body antes da pra retornar erro descritivo
            // (ex: invalid_grant, redirect_uri mismatch, etc) que aparece no
            // popup de callback.
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"Mercado Livre rejeitou a troca de code por token (HTTP {(int)response.StatusCode}): {errorBody}");
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();

            // access_token e refresh_token sao obrigatorios. Sem eles a integracao
            // nao funciona — falha imediato com mensagem clara em vez de KeyNotFound.
            if (!result.TryGetProperty("access_token", out var accessTokenEl))
                throw new InvalidOperationException("Resposta do Mercado Livre nao contem access_token.");
            if (!result.TryGetProperty("refresh_token", out var refreshTokenEl))
                throw new InvalidOperationException("Resposta do Mercado Livre nao contem refresh_token.");

            _settings.AccessToken = accessTokenEl.GetString();
            _settings.RefreshToken = refreshTokenEl.GetString();

            // user_id e opcional — algumas versoes da API ML nao retornam aqui
            // (precisa chamar /users/me separadamente). Antes, GetProperty
            // lancava KeyNotFoundException com "The given key was not present
            // in the dictionary." e o popup mostrava "Falha na conexao"
            // mesmo com o token ja salvo em memoria.
            if (result.TryGetProperty("user_id", out var userIdEl))
            {
                _settings.UserId = userIdEl.GetRawText();
            }

            // Persistir tokens no banco
            await _configRepository.SetValorAsync("ML_ACCESS_TOKEN", _settings.AccessToken);
            await _configRepository.SetValorAsync("ML_REFRESH_TOKEN", _settings.RefreshToken);
            if (!string.IsNullOrEmpty(_settings.UserId))
                await _configRepository.SetValorAsync("ML_USER_ID", _settings.UserId);

            _logger.LogInformation("Mercado Livre conectado. UserId: {UserId}", _settings.UserId ?? "(nao retornado no token endpoint)");
        }

        public async Task<bool> IsConnectedAsync()
        {
            await LoadTokensFromDbIfNeeded();

            if (string.IsNullOrEmpty(_settings.AccessToken)) return false;

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "/users/me");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.AccessToken);
                var response = await _httpClient.SendAsync(request);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    await RefreshTokenAsync();
                    return !string.IsNullOrEmpty(_settings.AccessToken);
                }

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<MercadoLivreContaInfo?> GetContaInfoAsync()
        {
            await LoadTokensFromDbIfNeeded();
            if (string.IsNullOrEmpty(_settings.AccessToken)) return null;

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "/users/me");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.AccessToken);
                var response = await _httpClient.SendAsync(request);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    await RefreshTokenAsync();
                    if (string.IsNullOrEmpty(_settings.AccessToken)) return null;
                    request = new HttpRequestMessage(HttpMethod.Get, "/users/me");
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.AccessToken);
                    response = await _httpClient.SendAsync(request);
                }

                if (!response.IsSuccessStatusCode) return null;

                var data = await response.Content.ReadFromJsonAsync<JsonElement>();
                return new MercadoLivreContaInfo
                {
                    Nickname = data.TryGetProperty("nickname", out var n) ? n.GetString() ?? "" : "",
                    Email = data.TryGetProperty("email", out var e) ? e.GetString() ?? "" : "",
                    UserId = data.TryGetProperty("id", out var i) ? i.ToString() : (_settings.UserId ?? ""),
                    Pais = data.TryGetProperty("country_id", out var c) ? c.GetString() : null,
                    UrlPerfil = data.TryGetProperty("permalink", out var p) ? p.GetString() : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar info da conta ML");
                return null;
            }
        }

        public async Task DesconectarAsync()
        {
            await _configRepository.SetValorAsync("ML_ACCESS_TOKEN", "");
            await _configRepository.SetValorAsync("ML_REFRESH_TOKEN", "");
            await _configRepository.SetValorAsync("ML_USER_ID", "");
            _settings.AccessToken = null;
            _settings.RefreshToken = null;
            _settings.UserId = null;
            _logger.LogInformation("Mercado Livre desconectado.");
        }

        private async Task LoadTokensFromDbIfNeeded()
        {
            if (!string.IsNullOrEmpty(_settings.AccessToken)) return;

            _settings.AccessToken = await _configRepository.GetValorAsync("ML_ACCESS_TOKEN");
            _settings.RefreshToken = await _configRepository.GetValorAsync("ML_REFRESH_TOKEN");
            _settings.UserId = await _configRepository.GetValorAsync("ML_USER_ID");
        }

        public async Task<(string ExternoId, string Url)> PublicarVeiculoAsync(int veiculoId)
        {
            await EnsureTokenAsync();

            var veiculo = await _veiculoRepository.GetByIdAsync(veiculoId);
            if (veiculo == null) throw new Exception("Veículo não encontrado.");

            var imagens = await _imagemRepository.GetByVeiculoIdAsync(veiculoId);
            var loja = await _lojaRepository.GetByIdAsync(veiculo.R_LojId);
            var urlBase = loja?.LojUrlCatalogo?.TrimEnd('/') ?? "";

            var pictures = imagens
                .Where(i => i.ImgSts)
                .OrderBy(i => i.ImgOrdem)
                .Select(i => new { source = $"{urlBase}/api/imagens/file?path={Uri.EscapeDataString(i.ImgCaminho)}" })
                .ToList();

            var item = new
            {
                title = $"{veiculo.VeiMarca} {veiculo.VeiModelo} {veiculo.VeiAno}",
                category_id = "MLB1744", // Carros, Vans e Utilitarios
                price = veiculo.VeiPreco,
                currency_id = "BRL",
                available_quantity = 1,
                buying_mode = "buy_it_now",
                condition = "used",
                listing_type_id = "gold_special", // Destaque
                description = new { plain_text = MontarDescricao(veiculo, loja) },
                pictures = pictures,
                attributes = new object[]
                {
                    new { id = "BRAND", value_name = veiculo.VeiMarca },
                    new { id = "MODEL", value_name = veiculo.VeiModelo },
                    new { id = "VEHICLE_YEAR", value_name = veiculo.VeiAno.ToString() },
                    new { id = "KILOMETERS", value_name = veiculo.VeiKm.ToString() },
                    new { id = "COLOR", value_name = veiculo.VeiCor ?? "" },
                    new { id = "FUEL_TYPE", value_name = "Flex" },
                    new { id = "ITEM_CONDITION", value_name = "Usado" }
                }
            };

            var json = JsonSerializer.Serialize(item);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "/items");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.AccessToken);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Erro ao publicar no ML: {Response}", responseBody);
                throw new Exception($"Erro ao publicar no Mercado Livre: {response.StatusCode}");
            }

            var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
            var externoId = result.GetProperty("id").GetString();
            var permalink = result.GetProperty("permalink").GetString();

            _logger.LogInformation("Veiculo {VeiculoId} publicado no ML: {ExternoId}", veiculoId, externoId);

            return (externoId, permalink);
        }

        public async Task RemoverAnuncioAsync(string externoId)
        {
            await EnsureTokenAsync();

            var json = JsonSerializer.Serialize(new { status = "closed" });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Put, $"/items/{externoId}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.AccessToken);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Erro ao remover anuncio ML {ExternoId}: {Response}", externoId, responseBody);
            }
            else
            {
                _logger.LogInformation("Anuncio ML {ExternoId} removido", externoId);
            }
        }

        public async Task AtualizarAnuncioAsync(string externoId, int veiculoId)
        {
            await EnsureTokenAsync();

            var veiculo = await _veiculoRepository.GetByIdAsync(veiculoId);
            if (veiculo == null) return;

            var json = JsonSerializer.Serialize(new
            {
                price = veiculo.VeiPreco,
                title = $"{veiculo.VeiMarca} {veiculo.VeiModelo} {veiculo.VeiAno}"
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Put, $"/items/{externoId}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.AccessToken);
            request.Content = content;

            await _httpClient.SendAsync(request);
        }

        private async Task RefreshTokenAsync()
        {
            if (string.IsNullOrEmpty(_settings.RefreshToken)) return;

            try
            {
                var request = new
                {
                    grant_type = "refresh_token",
                    client_id = _settings.AppId,
                    client_secret = _settings.ClientSecret,
                    refresh_token = _settings.RefreshToken
                };

                var response = await _httpClient.PostAsJsonAsync("/oauth/token", request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                _settings.AccessToken = result.GetProperty("access_token").GetString();
                _settings.RefreshToken = result.GetProperty("refresh_token").GetString();

                await _configRepository.SetValorAsync("ML_ACCESS_TOKEN", _settings.AccessToken);
                await _configRepository.SetValorAsync("ML_REFRESH_TOKEN", _settings.RefreshToken);

                _logger.LogInformation("Token do Mercado Livre renovado com sucesso");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao renovar token do Mercado Livre");
                _settings.AccessToken = null;
            }
        }

        private async Task EnsureTokenAsync()
        {
            if (string.IsNullOrEmpty(_settings.AccessToken))
                throw new Exception("Mercado Livre nao esta conectado. Configure a integracao primeiro.");

            if (!await IsConnectedAsync())
                throw new Exception("Token do Mercado Livre expirado e não foi possível renovar.");
        }

        private static string MontarDescricao(Veiculo veiculo, Loja loja)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{veiculo.VeiMarca} {veiculo.VeiModelo} {veiculo.VeiAno}");
            sb.AppendLine();
            if (veiculo.VeiKm > 0) sb.AppendLine($"Quilometragem: {veiculo.VeiKm:N0} km");
            if (!string.IsNullOrEmpty(veiculo.VeiCor)) sb.AppendLine($"Cor: {veiculo.VeiCor}");
            if (!string.IsNullOrEmpty(veiculo.VeiOpcionais))
            {
                sb.AppendLine();
                sb.AppendLine("Opcionais:");
                foreach (var opc in veiculo.VeiOpcionais.Split(','))
                    sb.AppendLine($"- {opc.Trim()}");
            }
            if (!string.IsNullOrEmpty(veiculo.VeiObservacao))
            {
                sb.AppendLine();
                sb.AppendLine(veiculo.VeiObservacao);
            }
            if (loja != null)
            {
                sb.AppendLine();
                sb.AppendLine($"{loja.LojNome}");
                if (!string.IsNullOrEmpty(loja.LojCidade)) sb.AppendLine($"{loja.LojCidade}/{loja.LojEstado}");
                if (!string.IsNullOrEmpty(loja.LojWhatsApp)) sb.AppendLine($"WhatsApp: {loja.LojWhatsApp}");
            }
            return sb.ToString();
        }
    }
}
