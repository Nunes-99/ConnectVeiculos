using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.Infrastructure.Services.ConsultaPlaca
{
    /// <summary>Consulta criada na ZapCar (ainda processando).</summary>
    public class ZapCarCriada
    {
        public string Id { get; set; } = "";
        public decimal ValorCobrado { get; set; }
        public bool Teste { get; set; }
    }

    /// <summary>Estado de uma consulta na ZapCar.</summary>
    public class ZapCarResultado
    {
        /// <summary>"processando", "concluido" ou "erro".</summary>
        public string Status { get; set; } = "processando";
        /// <summary>Bloco "veiculo" (contrato normalizado e estavel), JSON.</summary>
        public string? VeiculoJson { get; set; }
        public string? Erro { get; set; }
        public string? ErroCodigo { get; set; }
        public decimal? ValorCobrado { get; set; }
    }

    public class ZapCarException : Exception
    {
        public string Codigo { get; }
        public bool Retentavel { get; }
        public int Http { get; }

        public ZapCarException(string mensagem, string codigo, bool retentavel, int http) : base(mensagem)
        {
            Codigo = codigo;
            Retentavel = retentavel;
            Http = http;
        }
    }

    public interface IZapCarClient
    {
        Task<ZapCarCriada> CriarAsync(string servico, string placa, string chaveIdempotencia, CancellationToken ct = default);
        Task<ZapCarResultado> ObterAsync(string id, CancellationToken ct = default);
        /// <summary>PDF (ou imagem) do laudo; null quando nao ha documento.</summary>
        Task<(byte[] Conteudo, string Tipo)?> DocumentoAsync(string id, CancellationToken ct = default);
    }

    /// <summary>
    /// API da ZapCar (api.zapcarconsulta.com.br/v1): POST /consultas debita a
    /// carteira da plataforma e enfileira; GET /consultas/{id} ate "concluido"
    /// ou "erro" (erro e' estornado por eles). Programado contra o bloco
    /// "veiculo", que tem a mesma forma em todos os servicos.
    /// </summary>
    public class ZapCarClient : IZapCarClient
    {
        private readonly HttpClient _http;
        private readonly ZapCarSettings _settings;
        private readonly ILogger<ZapCarClient> _logger;

        public ZapCarClient(HttpClient http, IOptions<ZapCarSettings> settings, ILogger<ZapCarClient> logger)
        {
            _http = http;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<ZapCarCriada> CriarAsync(string servico, string placa, string chaveIdempotencia, CancellationToken ct = default)
        {
            using var req = Requisicao(HttpMethod.Post, "/consultas");
            req.Headers.Add("Idempotency-Key", chaveIdempotencia);
            req.Content = JsonContent.Create(new { servico, placa });
            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) throw Falha(resp, body, $"POST /consultas {servico} {placa}");

            using var doc = JsonDocument.Parse(body);
            var raiz = doc.RootElement;
            return new ZapCarCriada
            {
                Id = Texto(raiz, "id") ?? throw new ZapCarException("A base de consulta não devolveu o número da consulta.", "SEM_ID", true, (int)resp.StatusCode),
                ValorCobrado = Numero(raiz, "valor_cobrado") ?? 0,
                Teste = Texto(raiz, "ambiente") == "test"
            };
        }

        public async Task<ZapCarResultado> ObterAsync(string id, CancellationToken ct = default)
        {
            using var req = Requisicao(HttpMethod.Get, $"/consultas/{Uri.EscapeDataString(id)}");
            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) throw Falha(resp, body, $"GET /consultas/{id}");
            return LerResultado(body);
        }

        public static ZapCarResultado LerResultado(string body)
        {
            using var doc = JsonDocument.Parse(body);
            var raiz = doc.RootElement;
            return new ZapCarResultado
            {
                Status = Texto(raiz, "status") ?? "processando",
                VeiculoJson = raiz.TryGetProperty("veiculo", out var v) && v.ValueKind == JsonValueKind.Object ? v.GetRawText() : null,
                Erro = Texto(raiz, "erro"),
                ErroCodigo = Texto(raiz, "erro_codigo"),
                ValorCobrado = Numero(raiz, "valor_cobrado")
            };
        }

        public async Task<(byte[] Conteudo, string Tipo)?> DocumentoAsync(string id, CancellationToken ct = default)
        {
            using var req = Requisicao(HttpMethod.Get, $"/consultas/{Uri.EscapeDataString(id)}/pdf");
            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogInformation("ZapCar sem documento da consulta {Id}: HTTP {Status} {Body}",
                    id, (int)resp.StatusCode, await resp.Content.ReadAsStringAsync(ct));
                return null;
            }
            var tipo = resp.Content.Headers.ContentType?.MediaType ?? "application/pdf";
            return (await resp.Content.ReadAsByteArrayAsync(ct), tipo);
        }

        private ZapCarException Falha(HttpResponseMessage resp, string body, string chamada)
        {
            string codigo = "HTTP_" + ((int)resp.StatusCode).ToString(CultureInfo.InvariantCulture);
            string? erro = null;
            var retentavel = (int)resp.StatusCode >= 500 || (int)resp.StatusCode == 429;
            try
            {
                using var doc = JsonDocument.Parse(body);
                codigo = Texto(doc.RootElement, "codigo") ?? codigo;
                erro = Texto(doc.RootElement, "erro");
                if (doc.RootElement.TryGetProperty("retryable", out var r) && r.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    retentavel = r.GetBoolean();
            }
            catch (JsonException) { }

            _logger.LogWarning("ZapCar recusou {Chamada}: HTTP {Status} {Codigo} {Erro}", chamada, (int)resp.StatusCode, codigo, erro);
            return new ZapCarException(MensagemParaLoja(codigo, erro), codigo, retentavel, (int)resp.StatusCode);
        }

        /// <summary>
        /// O que a loja le. Erros da conta da plataforma (chave, saldo) nao sao
        /// problema dela: viram "indisponivel" (e ficam no log para nos).
        /// </summary>
        public static string MensagemParaLoja(string codigo, string? erro) => codigo switch
        {
            "PLACA_INVALIDA" => "Placa em formato inválido.",
            "SALDO_INSUFICIENTE" or "MISSING_API_KEY" or "INVALID_API_KEY" or "CUSTOMER_NOT_FOUND"
                => "A consulta por placa está indisponível no momento. Tente de novo mais tarde.",
            "RATE_LIMITED" => "Muitas consultas ao mesmo tempo. Aguarde um minuto e tente de novo.",
            "SERVICO_INDISPONIVEL" or "CIRCUIT_OPEN" or "PROVIDER_TIMEOUT" or "INTERNAL_ERROR"
                => "A base de consulta está fora do ar agora. Tente de novo em alguns minutos.",
            _ => string.IsNullOrWhiteSpace(erro) ? "Não foi possível fazer a consulta. Tente de novo." : erro
        };

        private HttpRequestMessage Requisicao(HttpMethod metodo, string caminho)
        {
            var req = new HttpRequestMessage(metodo, _settings.BaseUrl.TrimEnd('/') + caminho);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey.Trim());
            return req;
        }

        private static string? Texto(JsonElement e, string prop)
            => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static decimal? Numero(JsonElement e, string prop)
            => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;
    }
}
