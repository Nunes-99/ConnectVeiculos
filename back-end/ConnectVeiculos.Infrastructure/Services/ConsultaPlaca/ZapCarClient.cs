using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.Infrastructure.Services.ConsultaPlaca
{
    /// <summary>Servico do catalogo da ZapCar (GET /v1/servicos).</summary>
    public class ZapCarServico
    {
        public string Id { get; set; } = "";
        public string Nome { get; set; } = "";
        public string Descricao { get; set; } = "";
        /// <summary>CONSULTAS, DOCUMENTOS ou ANALISE_PESSOA.</summary>
        public string Categoria { get; set; } = "";
        public string? Prazo { get; set; }
        public string? Nota { get; set; }
        public int Ordem { get; set; }
        /// <summary>Campos exigidos (placa, uf, renavam, documento, chassi...).</summary>
        public List<string> Requer { get; set; } = new();
        /// <summary>Custo para a plataforma. Null quando varia por UF (CRLV).</summary>
        public decimal? Custo { get; set; }
        /// <summary>So no CRLV: custo e campos exigidos por UF (apenas as disponiveis).</summary>
        public List<ZapCarPrecoUf> PorUf { get; set; } = new();
    }

    public class ZapCarPrecoUf
    {
        public string Uf { get; set; } = "";
        public decimal Custo { get; set; }
        public List<string> Requer { get; set; } = new();
    }

    /// <summary>Aviso que a loja precisa aceitar antes da 2a via do ATPV-e.</summary>
    public class AvisoAtpve
    {
        public string Versao { get; set; } = "";
        public string Texto { get; set; } = "";
        public string Declaracao { get; set; } = "";
    }

    /// <summary>Pedido de consulta: so os campos preenchidos vao para a ZapCar.</summary>
    public class ZapCarPedido
    {
        public string Servico { get; set; } = "";
        public string? Placa { get; set; }
        public string? Uf { get; set; }
        public string? Renavam { get; set; }
        public string? Cpf { get; set; }
        public string? Chassi { get; set; }
        public string? Motor { get; set; }
        public string? Documento { get; set; }
        public string? Cnh { get; set; }
        public string? Chave { get; set; }
        public string? DataNascimento { get; set; }
        public string? Finalidade { get; set; }
        public bool? DeclaracaoFinalidade { get; set; }
        public string? AceiteAtpveVersao { get; set; }

        public JsonObject ParaJson()
        {
            var o = new JsonObject { ["servico"] = Servico };
            void Por(string nome, string? valor) { if (!string.IsNullOrWhiteSpace(valor)) o[nome] = valor; }
            Por("placa", Placa);
            Por("uf", Uf);
            Por("renavam", Renavam);
            Por("cpf", Cpf);
            Por("chassi", Chassi);
            Por("motor", Motor);
            Por("documento", Documento);
            Por("cnh", Cnh);
            Por("chave", Chave);
            Por("data_nascimento", DataNascimento);
            Por("finalidade", Finalidade);
            if (DeclaracaoFinalidade.HasValue) o["declaracao_finalidade"] = DeclaracaoFinalidade.Value;
            if (!string.IsNullOrWhiteSpace(AceiteAtpveVersao))
                o["aceite_atpve"] = new JsonObject { ["aceito"] = true, ["versao"] = AceiteAtpveVersao };
            return o;
        }
    }

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
        /// <summary>Resposta inteira (veiculo, dados, proprietario...), JSON.</summary>
        public string? RespostaJson { get; set; }
        public bool TemDocumento { get; set; }
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
        /// <summary>Catalogo de servicos com custo (guardado por algumas horas).</summary>
        Task<IReadOnlyList<ZapCarServico>> ServicosAsync(CancellationToken ct = default);
        Task<AvisoAtpve?> AvisoAtpveAsync(CancellationToken ct = default);
        Task<ZapCarCriada> CriarAsync(ZapCarPedido pedido, string chaveIdempotencia, CancellationToken ct = default);
        Task<ZapCarResultado> ObterAsync(string id, CancellationToken ct = default);
        /// <summary>PDF (ou imagem) do laudo; null quando nao ha documento.</summary>
        Task<(byte[] Conteudo, string Tipo)?> DocumentoAsync(string id, CancellationToken ct = default);
    }

    /// <summary>
    /// API da ZapCar (api.zapcarconsulta.com.br/v1): POST /consultas debita a
    /// carteira da plataforma e enfileira; GET /consultas/{id} ate "concluido"
    /// ou "erro" (erro e' estornado por eles). O catalogo (servicos, campos
    /// exigidos e custos) vem de GET /servicos, entao servico novo deles
    /// aparece sozinho na tela.
    /// </summary>
    public class ZapCarClient : IZapCarClient
    {
        /// <summary>Servicos que o POST /consultas aceita (FIPE tem rota propria e o sistema ja tem FIPE).</summary>
        public static readonly HashSet<string> Suportados = new(StringComparer.Ordinal)
        {
            "consulta", "consulta-completa", "ecv-premium", "consulta-veicular", "historico-km", "roubo-furto",
            "leilao", "fotos-leilao", "gravame", "renainf", "renajud", "debitos", "debitos-estadual",
            "historico-proprietarios", "comunicacao-venda", "decodificacao-chassi", "decodificacao-motor",
            "score-credito", "situacao-cpf", "cadastro-simples-cpf", "cadastro-completo-cpf", "cadastro-simples-cnpj",
            "cadastro-completo-cnpj", "credito-unico-cpf", "credito-biro-cpf", "credito-unico-cnpj", "credito-biro-cnpj",
            "cnh-nacional", "debitos-trabalhistas", "divida-ativa", "codigo-seguranca", "atpve", "consulta-nfe", "crlv"
        };

        private static readonly TimeSpan ValidadeCatalogo = TimeSpan.FromHours(6);
        private static readonly SemaphoreSlim TravaCatalogo = new(1, 1);
        private static IReadOnlyList<ZapCarServico>? _catalogo;
        private static DateTime _catalogoEm;

        private readonly HttpClient _http;
        private readonly ZapCarSettings _settings;
        private readonly ILogger<ZapCarClient> _logger;

        public ZapCarClient(HttpClient http, IOptions<ZapCarSettings> settings, ILogger<ZapCarClient> logger)
        {
            _http = http;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<IReadOnlyList<ZapCarServico>> ServicosAsync(CancellationToken ct = default)
        {
            if (_catalogo != null && DateTime.UtcNow - _catalogoEm < ValidadeCatalogo) return _catalogo;
            await TravaCatalogo.WaitAsync(ct);
            try
            {
                if (_catalogo != null && DateTime.UtcNow - _catalogoEm < ValidadeCatalogo) return _catalogo;
                using var req = Requisicao(HttpMethod.Get, "/servicos");
                using var resp = await _http.SendAsync(req, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("ZapCar: GET /servicos -> HTTP {Status}", (int)resp.StatusCode);
                    return _catalogo ?? Array.Empty<ZapCarServico>();
                }
                _catalogo = LerCatalogo(body);
                _catalogoEm = DateTime.UtcNow;
                return _catalogo;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogWarning(ex, "ZapCar: catalogo indisponivel");
                return _catalogo ?? Array.Empty<ZapCarServico>();
            }
            finally
            {
                TravaCatalogo.Release();
            }
        }

        public static List<ZapCarServico> LerCatalogo(string body)
        {
            using var doc = JsonDocument.Parse(body);
            var lista = new List<ZapCarServico>();
            if (!doc.RootElement.TryGetProperty("servicos", out var servicos) || servicos.ValueKind != JsonValueKind.Array)
                return lista;

            foreach (var s in servicos.EnumerateArray())
            {
                var id = Texto(s, "servico");
                if (id == null || !Suportados.Contains(id)) continue;
                if (s.TryGetProperty("disponivel_api", out var api) && api.ValueKind == JsonValueKind.False) continue;

                var item = new ZapCarServico
                {
                    Id = id,
                    Nome = Texto(s, "nome") ?? id,
                    Descricao = Texto(s, "descricao") ?? "",
                    Categoria = Texto(s, "categoria") ?? "CONSULTAS",
                    Prazo = Texto(s, "prazo"),
                    Nota = Texto(s, "nota_precos"),
                    Ordem = s.TryGetProperty("ordem", out var o) && o.TryGetInt32(out var n) ? n : 999,
                    Requer = Lista(s, "requer"),
                    Custo = Numero(s, "preco")
                };
                if (s.TryGetProperty("preco_por_uf", out var porUf) && porUf.ValueKind == JsonValueKind.Array)
                {
                    foreach (var u in porUf.EnumerateArray())
                    {
                        if (u.TryGetProperty("disponivel", out var d) && d.ValueKind == JsonValueKind.False) continue;
                        var uf = Texto(u, "uf");
                        var custo = Numero(u, "preco");
                        if (uf == null || custo == null) continue;
                        item.PorUf.Add(new ZapCarPrecoUf { Uf = uf, Custo = custo.Value, Requer = Lista(u, "requer") });
                    }
                }
                // Sem custo conhecido nao da para cobrar a loja.
                if (item.Custo == null && item.PorUf.Count == 0) continue;
                lista.Add(item);
            }
            return lista.OrderBy(s => s.Ordem).ToList();
        }

        public async Task<AvisoAtpve?> AvisoAtpveAsync(CancellationToken ct = default)
        {
            try
            {
                var raiz = _settings.BaseUrl.TrimEnd('/');
                if (raiz.EndsWith("/v1", StringComparison.Ordinal)) raiz = raiz[..^3];
                using var resp = await _http.GetAsync(raiz + "/public/avisos/atpve", ct);
                if (!resp.IsSuccessStatusCode) return null;
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                var r = doc.RootElement;
                // O texto integral termina com a declaracao marcada ("[x] Confirmo..."):
                // a tela mostra a declaracao na caixa de selecao, entao sai do texto.
                var texto = string.Join("\n", (Texto(r, "texto_integral") ?? Texto(r, "mensagem") ?? "")
                    .Split('\n')
                    .Where(l => !l.TrimStart().StartsWith("[", StringComparison.Ordinal)));
                return new AvisoAtpve
                {
                    Versao = Texto(r, "versao") ?? "",
                    Texto = texto.Trim(),
                    Declaracao = Texto(r, "declaracao") ?? ""
                };
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogWarning(ex, "ZapCar: aviso do ATPV-e indisponivel");
                return null;
            }
        }

        public async Task<ZapCarCriada> CriarAsync(ZapCarPedido pedido, string chaveIdempotencia, CancellationToken ct = default)
        {
            using var req = Requisicao(HttpMethod.Post, "/consultas");
            req.Headers.Add("Idempotency-Key", chaveIdempotencia);
            req.Content = new StringContent(pedido.ParaJson().ToJsonString(), Encoding.UTF8, "application/json");
            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) throw Falha(resp, body, $"POST /consultas {pedido.Servico}");

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
            var status = Texto(raiz, "status") ?? "processando";
            return new ZapCarResultado
            {
                Status = status,
                RespostaJson = status == "concluido" ? body : null,
                TemDocumento = !string.IsNullOrEmpty(Texto(raiz, "pdf_url")),
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
                => "A consulta está indisponível no momento. Tente de novo mais tarde.",
            "RATE_LIMITED" => "Muitas consultas ao mesmo tempo. Aguarde um minuto e tente de novo.",
            "SERVICO_INDISPONIVEL" or "CIRCUIT_OPEN" or "PROVIDER_TIMEOUT" or "INTERNAL_ERROR"
                => "A base de consulta está fora do ar agora. Tente de novo em alguns minutos.",
            "UF_INDISPONIVEL" => "Este serviço não está disponível para o estado informado.",
            "AVISO_ATPVE_DESATUALIZADO" => "O aviso do ATPV-e mudou. Feche e abra de novo para ler e aceitar a versão nova.",
            _ => string.IsNullOrWhiteSpace(erro) ? "Não foi possível fazer a consulta. Tente de novo." : erro
        };

        private HttpRequestMessage Requisicao(HttpMethod metodo, string caminho)
        {
            var req = new HttpRequestMessage(metodo, _settings.BaseUrl.TrimEnd('/') + caminho);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey.Trim());
            return req;
        }

        private static List<string> Lista(JsonElement e, string prop)
            => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Array
                ? v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList()
                : new List<string>();

        private static string? Texto(JsonElement e, string prop)
            => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static decimal? Numero(JsonElement e, string prop)
            => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;
    }
}
