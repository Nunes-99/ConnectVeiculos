using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.Infrastructure.Services.Financiamento
{
    public class PanFinanciamentoService : IBancoFinanciamentoService
    {
        private readonly HttpClient _httpClient;
        private readonly PanFinanciamentoSettings _settings;
        private readonly ILogger<PanFinanciamentoService> _logger;
        private string _accessToken;
        private DateTime _tokenExpiration;

        public string NomeBanco => "Banco Pan";
        public string CodigoBanco => "PAN";

        public PanFinanciamentoService(
            HttpClient httpClient,
            IOptions<PanFinanciamentoSettings> settings,
            ILogger<PanFinanciamentoService> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
        }

        public bool IsConfigured()
        {
            return !string.IsNullOrEmpty(_settings.ClientId) && !string.IsNullOrEmpty(_settings.ClientSecret);
        }

        public async Task<BancoSimulacaoResultado> SimularAsync(BancoSimulacaoRequest request)
        {
            await EnsureTokenAsync();

            var body = new
            {
                valorVeiculo = request.ValorVeiculo,
                valorEntrada = request.ValorEntrada,
                prazo = request.Parcelas,
                anoModeloVeiculo = request.AnoVeiculo,
                tipoVeiculo = request.TipoVeiculo,
                cpf = request.CpfCliente?.Replace(".", "").Replace("-", ""),
                renda = request.RendaMensal
            };

            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}/financiamento/v1/veiculos/simulacao");
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            httpRequest.Content = content;

            try
            {
                var response = await _httpClient.SendAsync(httpRequest);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Pan simulacao rejeitada: {Response}", responseBody);
                    return new BancoSimulacaoResultado
                    {
                        Banco = NomeBanco,
                        CodigoBanco = CodigoBanco,
                        Aprovado = false,
                        Mensagem = "Simulacao nao aprovada pelo Pan"
                    };
                }

                var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
                return new BancoSimulacaoResultado
                {
                    Banco = NomeBanco,
                    CodigoBanco = CodigoBanco,
                    Aprovado = true,
                    TaxaMensal = result.TryGetProperty("taxaMensal", out var tm) ? tm.GetDecimal() : 0,
                    TaxaAnual = result.TryGetProperty("taxaAnual", out var ta) ? ta.GetDecimal() : 0,
                    ValorParcela = result.TryGetProperty("valorParcela", out var vp) ? vp.GetDecimal() : 0,
                    ValorFinanciado = request.ValorVeiculo - request.ValorEntrada,
                    ValorTotal = result.TryGetProperty("valorTotalFinanciamento", out var vt) ? vt.GetDecimal() : 0,
                    CetAnual = result.TryGetProperty("cet", out var cet) ? cet.GetDecimal() : 0,
                    Parcelas = request.Parcelas,
                    SimulacaoId = result.TryGetProperty("idSimulacao", out var sid) ? sid.GetString() : Guid.NewGuid().ToString(),
                    Mensagem = "Simulacao aprovada"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro na simulacao Pan");
                return new BancoSimulacaoResultado
                {
                    Banco = NomeBanco,
                    CodigoBanco = CodigoBanco,
                    Aprovado = false,
                    Mensagem = $"Erro ao consultar Pan: {ex.Message}"
                };
            }
        }

        public async Task<BancoPropostaResultado> EnviarPropostaAsync(BancoPropostaRequest request)
        {
            await EnsureTokenAsync();

            var body = new
            {
                simulacaoId = request.SimulacaoId,
                veiculo = new { marca = request.Marca, modelo = request.Modelo, anoModelo = request.Ano, placa = request.Placa, chassi = request.Chassi, quilometragem = request.Km },
                cliente = new { nome = request.NomeCliente, cpf = request.CpfCliente?.Replace(".", "").Replace("-", ""), rg = request.RgCliente, dataNascimento = request.DataNascimento.ToString("yyyy-MM-dd"), celular = request.TelefoneCliente, email = request.EmailCliente, endereco = request.EnderecoCliente, renda = request.RendaMensal },
                valorVeiculo = request.ValorVeiculo,
                valorEntrada = request.ValorEntrada,
                prazo = request.Parcelas
            };

            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}/financiamento/v1/veiculos/proposta");
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            httpRequest.Content = content;

            try
            {
                var response = await _httpClient.SendAsync(httpRequest);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new BancoPropostaResultado { Banco = NomeBanco, Status = "RECUSADA", Mensagem = "Proposta nao aprovada pelo Pan" };
                }

                var result = JsonSerializer.Deserialize<JsonElement>(responseBody);
                return new BancoPropostaResultado
                {
                    Banco = NomeBanco,
                    PropostaExternaId = result.TryGetProperty("idProposta", out var pid) ? pid.GetString() : "",
                    Status = "EM_ANALISE",
                    Mensagem = "Proposta enviada para analise"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar proposta Pan");
                return new BancoPropostaResultado { Banco = NomeBanco, Status = "ERRO", Mensagem = ex.Message };
            }
        }

        public async Task<BancoPropostaStatus> ConsultarStatusAsync(string propostaExternaId)
        {
            await EnsureTokenAsync();

            var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{_settings.BaseUrl}/financiamento/v1/veiculos/proposta/{propostaExternaId}");
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

            try
            {
                var response = await _httpClient.SendAsync(httpRequest);
                var responseBody = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(responseBody);

                return new BancoPropostaStatus
                {
                    PropostaExternaId = propostaExternaId,
                    Banco = NomeBanco,
                    Status = result.TryGetProperty("situacao", out var s) ? s.GetString() : "DESCONHECIDO",
                    Mensagem = result.TryGetProperty("descricao", out var m) ? m.GetString() : "",
                    UltimaAtualizacao = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new BancoPropostaStatus { PropostaExternaId = propostaExternaId, Banco = NomeBanco, Status = "ERRO", Mensagem = ex.Message, UltimaAtualizacao = DateTime.UtcNow };
            }
        }

        private async Task EnsureTokenAsync()
        {
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiration) return;

            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ClientId}:{_settings.ClientSecret}"));
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}/oauth2/token");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
            request.Content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("grant_type", "client_credentials") });

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            _accessToken = result.GetProperty("access_token").GetString();
            var expiresIn = result.GetProperty("expires_in").GetInt32();
            _tokenExpiration = DateTime.UtcNow.AddSeconds(expiresIn - 60);
        }
    }
}
