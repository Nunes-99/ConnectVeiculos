using System.Text;
using System.Text.Json;
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
        private readonly HttpClient _httpClient;
        private readonly FacebookCatalogSettings _settings;
        private readonly ILogger<FacebookCatalogService> _logger;
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly IVeiculoImagemRepository _imagemRepository;
        private readonly ILojaRepository _lojaRepository;

        public FacebookCatalogService(
            HttpClient httpClient,
            IOptions<FacebookCatalogSettings> settings,
            ILogger<FacebookCatalogService> logger,
            IVeiculoRepository veiculoRepository,
            IVeiculoImagemRepository imagemRepository,
            ILojaRepository lojaRepository)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
            _veiculoRepository = veiculoRepository;
            _imagemRepository = imagemRepository;
            _lojaRepository = lojaRepository;
        }

        public bool IsConfigured()
        {
            return !string.IsNullOrEmpty(_settings.AccessToken) && !string.IsNullOrEmpty(_settings.CatalogId);
        }

        public async Task PublicarVeiculoAsync(int veiculoId)
        {
            if (!IsConfigured()) return;

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

            // Catalog Batch API - upsert do produto
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

            await EnviarBatchAsync(batch, veiculoId, "publicar");
        }

        public async Task RemoverVeiculoAsync(int veiculoId)
        {
            if (!IsConfigured()) return;

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

            await EnviarBatchAsync(batch, veiculoId, "remover");
        }

        private async Task EnviarBatchAsync(object batch, int veiculoId, string operacao)
        {
            try
            {
                var url = $"https://graph.facebook.com/{_settings.ApiVersion}/{_settings.CatalogId}/items_batch";
                var json = JsonSerializer.Serialize(batch);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.AccessToken);
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
