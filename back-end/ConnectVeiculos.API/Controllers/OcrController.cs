using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace ConnectVeiculos.API.Controllers
{
    /// <summary>
    /// OCR de documentos veiculares (CRLV, laudo, etc.) usando Google Gemini.
    /// Modelo: gemini-2.0-flash. Tier gratuito: 1500 requests/dia + 15/min por
    /// conta Google. Sem cartao de credito pra criar a chave.
    ///
    /// Como obter a chave:
    ///   1. Acesse https://aistudio.google.com/apikey
    ///   2. Faca login com sua conta Google
    ///   3. Clique em "Create API Key"
    ///   4. Cole no .env como GEMINI_API_KEY=AIza...
    ///
    /// Sem a chave, endpoint retorna 503.
    /// </summary>
    [ApiController]
    [Route("api/ocr")]
    [Authorize]
    public class OcrController : ControllerBase
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<OcrController> _logger;
        private readonly Core.Interfaces.Database.Repositories.Configuracoes.IConfiguracaoSistemaRepository _configRepo;
        private readonly IConfiguration _configuration;

        private const string GeminiModel = "gemini-2.5-flash";
        private const string ConfigKeyGemini = "GEMINI_API_KEY";

        private static string GeminiApiUrl(string apiKey)
            => $"https://generativelanguage.googleapis.com/v1beta/models/{GeminiModel}:generateContent?key={apiKey}";

        public OcrController(
            IHttpClientFactory httpFactory,
            IConfiguration configuration,
            Core.Interfaces.Database.Repositories.Configuracoes.IConfiguracaoSistemaRepository configRepo,
            ILogger<OcrController> logger)
        {
            _httpFactory = httpFactory;
            _configuration = configuration;
            _configRepo = configRepo;
            _logger = logger;
        }

        // Resolucao em cascata: 1) Tabela ConfiguracaoSistema do tenant (definido via UI),
        // 2) env var GEMINI_API_KEY, 3) appsettings GeminiApi:Key. Permite que cada
        // tenant tenha sua propria chave OU caia no default do servidor.
        private async Task<string?> ResolverChaveAsync()
        {
            try
            {
                var dbValue = await _configRepo.GetValorAsync(ConfigKeyGemini);
                if (!string.IsNullOrWhiteSpace(dbValue)) return dbValue;
            }
            catch { /* tabela pode nao existir em tenants legados */ }

            return Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? _configuration["GeminiApi:Key"];
        }

        public sealed class CrlvScanRequest
        {
            /// <summary>Imagem do CRLV em base64 (data:image/jpeg;base64,... ou apenas o base64 puro).</summary>
            public string ImagemBase64 { get; set; } = string.Empty;
        }

        public sealed class CrlvScanResult
        {
            public string? Placa { get; set; }
            public string? Renavam { get; set; }
            public string? Chassi { get; set; }
            public string? Marca { get; set; }
            public string? Modelo { get; set; }
            public int? AnoFabricacao { get; set; }
            public int? AnoModelo { get; set; }
            public string? Cor { get; set; }
            public string? Combustivel { get; set; }
            public string? ProprietarioNome { get; set; }
            public string? ProprietarioDoc { get; set; }
            public string? Categoria { get; set; }
            public string? Especie { get; set; }
            public string? Confianca { get; set; }
            public string? Aviso { get; set; }
        }

        /// <summary>
        /// Extrai campos estruturados de uma foto de CRLV (formato digital ou em papel).
        /// </summary>
        [HttpPost("crlv")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> ExtrairCrlv([FromBody] CrlvScanRequest req, CancellationToken ct)
        {
            var geminiKey = await ResolverChaveAsync();
            if (string.IsNullOrEmpty(geminiKey))
                return StatusCode(503, new { message = "OCR não configurado. Cadastre a chave do Google Gemini em /integracoes (admin)." });

            if (req == null || string.IsNullOrWhiteSpace(req.ImagemBase64))
                return BadRequest(new { message = "Imagem em base64 é obrigatória." });

            var (mediaType, base64Data) = SepararDataUrl(req.ImagemBase64);

            var prompt = """
Voce e um extrator de dados de CRLV brasileiro (Certificado de Registro e Licenciamento de Veiculo). Analise a imagem e retorne SOMENTE um JSON com as seguintes chaves (use null se algum campo nao for legivel/encontrado):

{
  "placa": "AAA-1234 ou AAA1A23 (Mercosul)",
  "renavam": "11 digitos numericos",
  "chassi": "17 caracteres alfanumericos",
  "marca": "Apenas a marca (ex: TOYOTA, HONDA, FIAT)",
  "modelo": "Modelo completo (ex: COROLLA XEI 2.0)",
  "anoFabricacao": numero (ex: 2020),
  "anoModelo": numero (ex: 2021),
  "cor": "PRETO, BRANCO, etc",
  "combustivel": "GASOLINA / DIESEL / FLEX / ELETRICO / HIBRIDO",
  "proprietarioNome": "Nome completo",
  "proprietarioDoc": "CPF ou CNPJ formatado",
  "categoria": "PARTICULAR / ALUGUEL / etc",
  "especie": "PASSAGEIRO / CARGA / etc",
  "confianca": "ALTA / MEDIA / BAIXA",
  "aviso": "Texto curto se houver algum problema (foto borrada, doc nao parece CRLV, etc) ou null"
}

REGRAS:
- Se a imagem nao parece um CRLV, retorne todos os campos como null e preencha "aviso" explicando.
- Nao invente dados. Se nao tem certeza de um campo, retorne null.
- Retorne SOMENTE o JSON, sem texto antes ou depois, sem code fences.
""";

            var payload = new
            {
                contents = new object[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = prompt },
                            new { inline_data = new { mime_type = mediaType, data = base64Data } }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.1,
                    maxOutputTokens = 1024,
                    responseMimeType = "application/json"
                }
            };

            var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(60);
            using var httpReq = new HttpRequestMessage(HttpMethod.Post, GeminiApiUrl(geminiKey))
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            HttpResponseMessage resp;
            try
            {
                resp = await client.SendAsync(httpReq, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro chamando Gemini API");
                return StatusCode(502, new { message = "Falha na chamada ao serviço de OCR.", detail = ex.Message });
            }

            var respBody = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini API retornou {Status}: {Body}", (int)resp.StatusCode, respBody);

                if ((int)resp.StatusCode == 429)
                {
                    var (resetUtc, resetLocalBr) = CalcularProximoResetGemini();
                    return StatusCode(429, new
                    {
                        message = $"Limite diário de scans atingido. O sistema voltará a funcionar em {resetLocalBr:dd/MM/yyyy 'as' HH:mm} (horário de Brasília). Por favor, preencha os dados do veículo manualmente.",
                        codigo = "limite_diario",
                        proximoResetUtc = resetUtc.ToString("o"),
                        proximoResetBr = resetLocalBr.ToString("o")
                    });
                }

                var friendlyMsg = (int)resp.StatusCode switch
                {
                    400 => "Imagem invalida ou requisicao mal-formada.",
                    403 => "Chave do Gemini invalida ou sem permissao. Verifique no console do Google AI Studio.",
                    _ => "Erro do serviço de OCR."
                };
                return StatusCode((int)resp.StatusCode, new { message = friendlyMsg, detail = respBody });
            }

            string? extractedJson = null;
            try
            {
                using var doc = JsonDocument.Parse(respBody);
                var candidates = doc.RootElement.GetProperty("candidates");
                if (candidates.GetArrayLength() > 0)
                {
                    var first = candidates[0];
                    var contentParts = first.GetProperty("content").GetProperty("parts");
                    foreach (var part in contentParts.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var txt))
                        {
                            extractedJson = txt.GetString();
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao parsear resposta do Gemini: {Body}", respBody);
                return StatusCode(502, new { message = "Resposta inválida do OCR.", raw = respBody });
            }

            if (string.IsNullOrWhiteSpace(extractedJson))
                return StatusCode(502, new { message = "OCR retornou conteudo vazio." });

            extractedJson = LimparCodeFences(extractedJson).Trim();

            try
            {
                var result = JsonSerializer.Deserialize<CrlvScanResult>(extractedJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "JSON do OCR nao bate com schema: {Raw}", extractedJson);
                return Ok(new { aviso = "Nao foi possivel estruturar a resposta do OCR.", raw = extractedJson });
            }
        }

        public sealed class GeminiConfigStatus
        {
            public bool Configurado { get; set; }
            public string? Mascara { get; set; }
            public string? Fonte { get; set; }
        }

        /// <summary>
        /// Retorna se a chave do Gemini esta configurada (sem expor o valor).
        /// </summary>
        [HttpGet("config")]
        [Authorize(Roles = "Administrador,Gerente")]
        public async Task<IActionResult> GetConfig()
        {
            string? source = null;
            string? valor = null;
            try
            {
                valor = await _configRepo.GetValorAsync(ConfigKeyGemini);
                if (!string.IsNullOrWhiteSpace(valor)) source = "banco";
            }
            catch { }

            if (string.IsNullOrWhiteSpace(valor))
            {
                valor = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? _configuration["GeminiApi:Key"];
                if (!string.IsNullOrWhiteSpace(valor)) source = "ambiente";
            }

            if (string.IsNullOrWhiteSpace(valor))
                return Ok(new GeminiConfigStatus { Configurado = false });

            return Ok(new GeminiConfigStatus
            {
                Configurado = true,
                Mascara = valor.Length > 8 ? $"{valor[..4]}...{valor[^4..]}" : "****",
                Fonte = source
            });
        }

        public sealed class GeminiConfigRequest
        {
            public string Chave { get; set; } = string.Empty;
        }

        /// <summary>
        /// Salva ou atualiza a chave do Gemini para o tenant atual.
        /// </summary>
        [HttpPost("config")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> SetConfig([FromBody] GeminiConfigRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Chave))
                return BadRequest(new { message = "Chave é obrigatória." });
            if (!req.Chave.StartsWith("AIza"))
                return BadRequest(new { message = "Chave Gemini deve comecar com 'AIza'." });

            await _configRepo.SetValorAsync(ConfigKeyGemini, req.Chave.Trim());
            _logger.LogInformation("Chave Gemini configurada via UI");
            return Ok(new { mensagem = "Chave salva." });
        }

        /// <summary>
        /// Remove a chave salva no banco (volta a usar env var/config).
        /// </summary>
        [HttpDelete("config")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> RemoveConfig()
        {
            await _configRepo.SetValorAsync(ConfigKeyGemini, "");
            return Ok(new { mensagem = "Chave removida." });
        }

        // Quota gratuita do Gemini reseta a meia-noite no horario do Pacifico (PT).
        // PT = UTC-8 (PST) no inverno, UTC-7 (PDT) no verao (DST).
        private static (DateTime utc, DateTimeOffset brasilia) CalcularProximoResetGemini()
        {
            TimeZoneInfo pacific;
            try { pacific = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles"); }
            catch { pacific = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"); }

            var agoraPt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, pacific);
            var proximaMeiaNoitePt = new DateTime(agoraPt.Year, agoraPt.Month, agoraPt.Day, 0, 0, 0, DateTimeKind.Unspecified).AddDays(1);
            var proximoResetUtc = TimeZoneInfo.ConvertTimeToUtc(proximaMeiaNoitePt, pacific);

            TimeZoneInfo brasilia;
            try { brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
            catch { brasilia = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }

            var brasiliaDt = TimeZoneInfo.ConvertTimeFromUtc(proximoResetUtc, brasilia);
            var offset = brasilia.GetUtcOffset(proximoResetUtc);
            return (proximoResetUtc, new DateTimeOffset(brasiliaDt, offset));
        }

        private static (string mediaType, string base64) SepararDataUrl(string input)
        {
            if (input.StartsWith("data:") && input.Contains(";base64,"))
            {
                var headerEnd = input.IndexOf(";base64,");
                var media = input.Substring(5, headerEnd - 5);
                var data = input.Substring(headerEnd + ";base64,".Length);
                return (media, data);
            }
            return ("image/jpeg", input);
        }

        private static string LimparCodeFences(string s)
        {
            s = s.Trim();
            if (s.StartsWith("```"))
            {
                var firstNl = s.IndexOf('\n');
                if (firstNl > 0) s = s.Substring(firstNl + 1);
                if (s.EndsWith("```")) s = s.Substring(0, s.Length - 3);
            }
            return s.Trim();
        }
    }
}
