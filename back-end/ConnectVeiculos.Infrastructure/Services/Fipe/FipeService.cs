using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Infrastructure.Cache;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace ConnectVeiculos.Infrastructure.Services.Fipe
{
    public class FipeService : IFipeService
    {
        private readonly HttpClient _httpClient;
        private readonly ICacheService _cacheService;
        private readonly ILogger<FipeService> _logger;
        private const string BaseUrl = "https://parallelum.com.br/fipe/api/v1";
        private const int CacheDurationMinutes = 1440; // 24 horas

        public FipeService(HttpClient httpClient, ICacheService cacheService, ILogger<FipeService> logger)
        {
            _httpClient = httpClient;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<IEnumerable<FipeMarca>> GetMarcasAsync(FipeTipoVeiculo tipo)
        {
            var cacheKey = $"fipe:marcas:{tipo}";

            var cached = _cacheService.Get<List<FipeMarca>>(cacheKey);
            if (cached != null)
                return cached;

            try
            {
                var tipoUrl = GetTipoUrl(tipo);
                var response = await _httpClient.GetAsync($"{BaseUrl}/{tipoUrl}/marcas");
                response.EnsureSuccessStatusCode();

                var marcas = await response.Content.ReadFromJsonAsync<List<FipeMarcaDto>>();
                var result = marcas?.Select(m => new FipeMarca { Codigo = int.Parse(m.Codigo), Nome = m.Nome }).ToList()
                    ?? new List<FipeMarca>();

                _cacheService.Set(cacheKey, result, TimeSpan.FromMinutes(CacheDurationMinutes));
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao consultar marcas FIPE");
                return Enumerable.Empty<FipeMarca>();
            }
        }

        public async Task<IEnumerable<FipeModelo>> GetModelosAsync(FipeTipoVeiculo tipo, int codigoMarca)
        {
            var cacheKey = $"fipe:modelos:{tipo}:{codigoMarca}";

            var cached = _cacheService.Get<List<FipeModelo>>(cacheKey);
            if (cached != null)
                return cached;

            try
            {
                var tipoUrl = GetTipoUrl(tipo);
                var response = await _httpClient.GetAsync($"{BaseUrl}/{tipoUrl}/marcas/{codigoMarca}/modelos");
                response.EnsureSuccessStatusCode();

                var data = await response.Content.ReadFromJsonAsync<FipeModelosResponseDto>();
                var result = data?.Modelos?.Select(m => new FipeModelo { Codigo = m.Codigo, Nome = m.Nome }).ToList()
                    ?? new List<FipeModelo>();

                _cacheService.Set(cacheKey, result, TimeSpan.FromMinutes(CacheDurationMinutes));
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao consultar modelos FIPE");
                return Enumerable.Empty<FipeModelo>();
            }
        }

        public async Task<IEnumerable<FipeAno>> GetAnosAsync(FipeTipoVeiculo tipo, int codigoMarca, int codigoModelo)
        {
            var cacheKey = $"fipe:anos:{tipo}:{codigoMarca}:{codigoModelo}";

            var cached = _cacheService.Get<List<FipeAno>>(cacheKey);
            if (cached != null)
                return cached;

            try
            {
                var tipoUrl = GetTipoUrl(tipo);
                var response = await _httpClient.GetAsync($"{BaseUrl}/{tipoUrl}/marcas/{codigoMarca}/modelos/{codigoModelo}/anos");
                response.EnsureSuccessStatusCode();

                var anos = await response.Content.ReadFromJsonAsync<List<FipeAnoDto>>();
                var result = anos?.Select(a => new FipeAno { Codigo = a.Codigo, Nome = a.Nome }).ToList()
                    ?? new List<FipeAno>();

                _cacheService.Set(cacheKey, result, TimeSpan.FromMinutes(CacheDurationMinutes));
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao consultar anos FIPE");
                return Enumerable.Empty<FipeAno>();
            }
        }

        public async Task<FipePreco> GetPrecoAsync(FipeTipoVeiculo tipo, int codigoMarca, int codigoModelo, string codigoAno)
        {
            var cacheKey = $"fipe:preco:{tipo}:{codigoMarca}:{codigoModelo}:{codigoAno}";

            var cached = _cacheService.Get<FipePreco>(cacheKey);
            if (cached != null)
                return cached;

            try
            {
                var tipoUrl = GetTipoUrl(tipo);
                var response = await _httpClient.GetAsync(
                    $"{BaseUrl}/{tipoUrl}/marcas/{codigoMarca}/modelos/{codigoModelo}/anos/{codigoAno}");
                response.EnsureSuccessStatusCode();

                var data = await response.Content.ReadFromJsonAsync<FipePrecoDto>();
                if (data == null)
                    return null;

                var result = new FipePreco
                {
                    TipoVeiculo = data.TipoVeiculo.ToString(),
                    Valor = data.Valor,
                    Marca = data.Marca,
                    Modelo = data.Modelo,
                    AnoModelo = data.AnoModelo,
                    Combustivel = data.Combustivel,
                    CodigoFipe = data.CodigoFipe,
                    MesReferencia = data.MesReferencia,
                    SiglaCombustivel = data.SiglaCombustivel
                };

                _cacheService.Set(cacheKey, result, TimeSpan.FromMinutes(CacheDurationMinutes));
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao consultar preco FIPE");
                return null;
            }
        }

        private static string GetTipoUrl(FipeTipoVeiculo tipo)
        {
            return tipo switch
            {
                FipeTipoVeiculo.Carros => "carros",
                FipeTipoVeiculo.Motos => "motos",
                FipeTipoVeiculo.Caminhoes => "caminhoes",
                _ => "carros"
            };
        }

        // DTOs internos para deserializacao
        private class FipeMarcaDto { public string Codigo { get; set; } public string Nome { get; set; } }
        private class FipeModeloDto { public int Codigo { get; set; } public string Nome { get; set; } }
        private class FipeModelosResponseDto { public List<FipeModeloDto> Modelos { get; set; } }
        private class FipeAnoDto { public string Codigo { get; set; } public string Nome { get; set; } }
        private class FipePrecoDto
        {
            public int TipoVeiculo { get; set; }
            public string Valor { get; set; }
            public string Marca { get; set; }
            public string Modelo { get; set; }
            public int AnoModelo { get; set; }
            public string Combustivel { get; set; }
            public string CodigoFipe { get; set; }
            public string MesReferencia { get; set; }
            public string SiglaCombustivel { get; set; }
        }
    }
}
