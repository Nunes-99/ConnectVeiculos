using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ConnectVeiculos.Core.Entities.Integracoes;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Integracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Core.Olx;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.Infrastructure.Services.Olx
{
    /// <inheritdoc cref="IOlxService"/>
    /// <remarks>
    /// Como a API da OLX funciona (developers.olx.com.br/anuncio/api):
    /// - OAuth com client_secret; o token nao tem refresh nem validade
    ///   documentada. 401 = a loja precisa conectar de novo.
    /// - PUT /autoupload/import recebe a lista de anuncios com o token no
    ///   corpo. "insert" com o mesmo id altera; "delete" tira do ar. A
    ///   resposta so diz se entrou na fila: o resultado (aceito/recusado,
    ///   link) e' consultado depois pelo token do envio.
    /// - Marca/modelo/versao sao ids do catalogo car_info da OLX.
    /// </remarks>
    public class OlxService : IOlxService
    {
        public const string ChaveToken = "OLX_TOKEN";
        public const string ChaveConta = "OLX_CONTA";
        public const int MaximoFotos = 20; // limite da OLX
        private static readonly TimeSpan DuracaoCatalogo = TimeSpan.FromHours(12);

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> TravasVeiculo = new();
        private static readonly ConcurrentDictionary<string, (IReadOnlyList<OlxItem> Valor, DateTime Expira)> Catalogo = new();
        private static readonly ConcurrentDictionary<string, long> PedidosFotos = new();

        /// <summary>Para os testes: cada um simula o seu catalogo.</summary>
        public static void LimparCatalogoEmCache() => Catalogo.Clear();

        /// <summary>Espera antes de mandar fotos novas: a tela sobe uma foto por vez.</summary>
        public static TimeSpan EsperaFotos { get; set; } = TimeSpan.FromSeconds(15);

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _http;
        private readonly OlxSettings _settings;
        private readonly IConfiguracaoSistemaRepository _config;
        private readonly ITokenProtector _protector;
        private readonly IOAuthStateProtector _state;
        private readonly ITenantContext _tenant;
        private readonly IVeiculoRepository _veiculos;
        private readonly IVeiculoImagemRepository _imagens;
        private readonly ILojaRepository _lojas;
        private readonly IVeiculoOlxRepository _anuncios;
        private readonly ILogger<OlxService> _logger;

        public OlxService(HttpClient http, IOptions<OlxSettings> settings, IConfiguracaoSistemaRepository config,
            ITokenProtector protector, IOAuthStateProtector state, ITenantContext tenant, IVeiculoRepository veiculos,
            IVeiculoImagemRepository imagens, ILojaRepository lojas, IVeiculoOlxRepository anuncios, ILogger<OlxService> logger)
        {
            _http = http;
            _settings = settings.Value;
            _config = config;
            _protector = protector;
            _state = state;
            _tenant = tenant;
            _veiculos = veiculos;
            _imagens = imagens;
            _lojas = lojas;
            _anuncios = anuncios;
            _logger = logger;
        }

        private sealed class TokenSalvo
        {
            public string AccessToken { get; set; } = "";
            public DateTime ConectadoEm { get; set; }
        }

        // ================================================================
        // Conexao
        // ================================================================

        public async Task<OlxStatus> StatusAsync(CancellationToken ct = default)
        {
            var status = new OlxStatus { Configurado = _settings.Configurado, SiteConexao = Origem(_settings.RedirectUri) };
            var token = await TokenAsync();
            if (!_settings.Configurado || token == null) return status;

            status.Conectado = true;
            status.ConectadoEm = token.ConectadoEm;
            try
            {
                using var doc = await BearerAsync("/balance", ct, aceitar410: true);
                if (doc == null)
                {
                    status.Erro = "A conta conectada não tem plano profissional da OLX. A integração exige Essencial, Plus ou Premium Empresa.";
                    return status;
                }
                var r = doc.RootElement;
                status.Conta = Texto(r, "name");
                if (r.TryGetProperty("ads", out var ads) && ads.ValueKind == JsonValueKind.Object)
                {
                    status.AnunciosUsados = Inteiro(ads, "performed");
                    status.AnunciosDisponiveis = Inteiro(ads, "available");
                    status.AnunciosTotal = Inteiro(ads, "total");
                }
                if (Texto(r, "next_renew_date") is { } renova &&
                    DateTime.TryParse(renova, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
                    status.ProximaRenovacao = dt;
            }
            catch (OlxException ex)
            {
                status.Erro = ex.Message;
                status.Conectado = await TokenAsync() != null; // 401 desconecta
            }
            return status;
        }

        public string UrlAutorizacao()
        {
            ExigirConfigurado();
            var state = _state.Proteger(_tenant.TenantSlug ?? "");
            return $"{_settings.AuthUrl.TrimEnd('/')}/oauth?response_type=code" +
                   $"&client_id={Uri.EscapeDataString(_settings.ClientId!)}" +
                   $"&redirect_uri={Uri.EscapeDataString(_settings.RedirectUri)}" +
                   $"&scope={Uri.EscapeDataString(_settings.Escopos)}" +
                   $"&state={Uri.EscapeDataString(state)}";
        }

        public async Task<OlxStatus> ConectarAsync(string code, string? state, CancellationToken ct = default)
        {
            ExigirConfigurado();
            if (string.IsNullOrWhiteSpace(code)) throw new OlxException("A OLX não devolveu o código de autorização.");
            try
            {
                _state.Validar(state, _tenant.TenantSlug ?? "");
            }
            catch (OAuthStateException ex)
            {
                throw new OlxException(ex.Message);
            }

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_settings.AuthUrl.TrimEnd('/')}/oauth/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["client_id"] = _settings.ClientId!,
                    ["client_secret"] = _settings.ClientSecret!,
                    ["redirect_uri"] = _settings.RedirectUri,
                    ["grant_type"] = "authorization_code"
                })
            };
            HttpResponseMessage resp;
            try { resp = await _http.SendAsync(req, ct); }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new OlxException("Não foi possível falar com a OLX agora. Tente de novo em alguns minutos.");
            }
            using (resp)
            {
                var texto = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("OLX: token recusado ({Status}) {Corpo}", (int)resp.StatusCode, Cortar(texto, 300));
                    throw new OlxException("A OLX recusou a autorização. Clique em Conectar de novo.");
                }
                string? access;
                try
                {
                    using var doc = JsonDocument.Parse(texto);
                    access = Texto(doc.RootElement, "access_token");
                }
                catch (JsonException) { access = null; }
                if (string.IsNullOrEmpty(access)) throw new OlxException("A OLX não devolveu a chave de acesso.");
                await SalvarTokenAsync(new TokenSalvo { AccessToken = access, ConectadoEm = DateTime.UtcNow });
            }
            _logger.LogInformation("OLX conectada no tenant {Tenant}", _tenant.TenantSlug);
            return await StatusAsync(ct);
        }

        public async Task DesconectarAsync(CancellationToken ct = default)
        {
            // A OLX nao documenta revogacao: so esquece o token.
            await _config.SetValorAsync(ChaveToken, "");
            await _config.SetValorAsync(ChaveConta, "");
        }

        // ================================================================
        // Catalogo
        // ================================================================

        public Task<IReadOnlyList<OlxItem>> MarcasAsync(CancellationToken ct = default) =>
            CatalogoAsync("/car_info", ct);

        public Task<IReadOnlyList<OlxItem>> ModelosAsync(string marcaId, CancellationToken ct = default)
        {
            if (!SoDigitos(marcaId).Equals(marcaId) || marcaId == "") throw new OlxException("Escolha a marca.");
            return CatalogoAsync($"/car_info/{marcaId}", ct);
        }

        public Task<IReadOnlyList<OlxItem>> VersoesAsync(string marcaId, string modeloId, CancellationToken ct = default)
        {
            if (!SoDigitos(marcaId).Equals(marcaId) || marcaId == "") throw new OlxException("Escolha a marca.");
            if (!SoDigitos(modeloId).Equals(modeloId) || modeloId == "") throw new OlxException("Escolha o modelo.");
            return CatalogoAsync($"/car_info/{marcaId}/{modeloId}", ct);
        }

        public OlxAtributos Atributos() => OlxCodigos.Atributos();

        private async Task<IReadOnlyList<OlxItem>> CatalogoAsync(string caminho, CancellationToken ct)
        {
            var chave = _settings.ApiUrl + caminho;
            if (Catalogo.TryGetValue(chave, out var c) && c.Expira > DateTime.UtcNow) return c.Valor;
            var token = await AccessTokenAsync();
            using var doc = await PostJsonAsync(caminho, new { access_token = token }, ct);
            var itens = LerCatalogo(doc.RootElement);
            Catalogo[chave] = (itens, DateTime.UtcNow.Add(DuracaoCatalogo));
            return itens;
        }

        /// <summary>{"status":"ok","data":{"AUDI":6,...}}: nome -> id.</summary>
        public static List<OlxItem> LerCatalogo(JsonElement r)
        {
            var dados = r.ValueKind == JsonValueKind.Object && r.TryGetProperty("data", out var d) ? d : r;
            var itens = new List<OlxItem>();
            if (dados.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in dados.EnumerateObject())
                {
                    var id = p.Value.ValueKind switch
                    {
                        JsonValueKind.Number => p.Value.GetRawText(),
                        JsonValueKind.String => p.Value.GetString(),
                        _ => null
                    };
                    if (!string.IsNullOrEmpty(id)) itens.Add(new OlxItem { Id = id, Nome = p.Name.Trim() });
                }
            }
            else if (dados.ValueKind == JsonValueKind.Array)
            {
                itens.AddRange(dados.EnumerateArray()
                    .Select(x => new OlxItem { Id = Texto(x, "id") ?? "", Nome = (Texto(x, "name") ?? "").Trim() })
                    .Where(x => x.Id != ""));
            }
            return itens.OrderBy(x => x.Nome, StringComparer.Create(new CultureInfo("pt-BR"), true)).ToList();
        }

        // ================================================================
        // Anuncio do veiculo
        // ================================================================

        /// <summary>Id do anuncio na OLX (o integrador escolhe; regex [A-Za-z0-9_{}-]{1,19}).</summary>
        public static string IdAnuncio(int veiculoId) => "cv" + veiculoId.ToString(CultureInfo.InvariantCulture);

        public async Task<OlxVeiculoInfo> ObterVeiculoAsync(int veiculoId, CancellationToken ct = default)
        {
            var veiculo = await _veiculos.GetByIdAsync(veiculoId) ?? throw new OlxException("Veículo não encontrado.");
            var anuncio = await _anuncios.GetByVeiculoIdAsync(veiculoId);
            if (anuncio != null && !string.IsNullOrEmpty(anuncio.VolToken))
            {
                try { await UmPorVeiculoAsync(veiculoId, async () => { await ConferirAsync(anuncio, ct); return true; }, ct); }
                catch (OlxException ex) { _logger.LogInformation("OLX: resultado do veiculo {VeiculoId} ainda sem resposta: {Erro}", veiculoId, ex.Message); }
            }
            var pendencias = await PendenciasAsync(veiculo);
            if (anuncio?.VolDadosJson is { Length: > 0 })
                return Info(anuncio, Dados(anuncio), false, pendencias);

            await AccessTokenAsync();
            return Info(anuncio, await SugerirAsync(veiculo, ct), true, pendencias);
        }

        public Task<OlxVeiculoInfo> PublicarAsync(int veiculoId, OlxAnuncioDados dados, CancellationToken ct = default)
            => UmPorVeiculoAsync(veiculoId, () => PublicarSemTravaAsync(veiculoId, dados, ct), ct);

        public Task<OlxVeiculoInfo> PausarAsync(int veiculoId, CancellationToken ct = default)
            => UmPorVeiculoAsync(veiculoId, () => PausarSemTravaAsync(veiculoId, ct), ct);

        public Task SincronizarVeiculoAsync(int veiculoId)
            => UmPorVeiculoAsync(veiculoId, async () => { await SincronizarSemTravaAsync(veiculoId); return true; }, CancellationToken.None);

        public async Task FotosMudaramAsync(int veiculoId)
        {
            var chave = $"{_tenant.TenantSlug}:{veiculoId}";
            var meu = PedidosFotos.AddOrUpdate(chave, 1, (_, n) => n + 1);
            if (EsperaFotos > TimeSpan.Zero) await Task.Delay(EsperaFotos);
            if (PedidosFotos.TryGetValue(chave, out var ultimo) && ultimo != meu) return;
            await SincronizarVeiculoAsync(veiculoId);
        }

        private async Task<T> UmPorVeiculoAsync<T>(int veiculoId, Func<Task<T>> acao, CancellationToken ct)
        {
            var trava = TravasVeiculo.GetOrAdd($"{_tenant.TenantSlug}:{veiculoId}", _ => new SemaphoreSlim(1, 1));
            await trava.WaitAsync(ct);
            try { return await acao(); }
            finally { trava.Release(); }
        }

        private async Task<OlxVeiculoInfo> PublicarSemTravaAsync(int veiculoId, OlxAnuncioDados dados, CancellationToken ct)
        {
            await AccessTokenAsync();
            var veiculo = await _veiculos.GetByIdAsync(veiculoId) ?? throw new OlxException("Veículo não encontrado.");
            if (veiculo.VeiSts != "D") throw new OlxException("Só veículo disponível pode ser anunciado.");
            Validar(dados);

            var anuncio = await _anuncios.GetByVeiculoIdAsync(veiculoId) ?? new VeiculoOlx(veiculoId, "");
            anuncio.SalvarDados(JsonSerializer.Serialize(dados, Json));
            await EnviarAsync(veiculo, anuncio, dados, forcar: true, ct);
            return Info(anuncio, dados, false, await PendenciasAsync(veiculo));
        }

        private async Task<OlxVeiculoInfo> PausarSemTravaAsync(int veiculoId, CancellationToken ct)
        {
            await AccessTokenAsync();
            var anuncio = await _anuncios.GetByVeiculoIdAsync(veiculoId);
            if (anuncio == null || !anuncio.EstaPublicado) throw new OlxException("O veículo não está anunciado na OLX.");
            var token = await RemoverNaOlxAsync(veiculoId, ct);
            anuncio.Removido(token, peloUsuario: true);
            await _anuncios.SalvarAsync(anuncio);
            var veiculo = await _veiculos.GetByIdAsync(veiculoId);
            return Info(anuncio, Dados(anuncio), false, veiculo == null ? new() : await PendenciasAsync(veiculo));
        }

        private async Task SincronizarSemTravaAsync(int veiculoId)
        {
            var anuncio = await _anuncios.GetByVeiculoIdAsync(veiculoId);
            if (anuncio == null || string.IsNullOrEmpty(anuncio.VolDadosJson) || anuncio.Pausado) return;
            if (!_settings.Configurado || await TokenAsync() == null) return;

            var veiculo = await _veiculos.GetByIdAsync(veiculoId);
            try
            {
                if (veiculo == null || veiculo.VeiSts != "D")
                {
                    if (anuncio.EstaPublicado)
                    {
                        var token = await RemoverNaOlxAsync(veiculoId, CancellationToken.None);
                        anuncio.Removido(token);
                        await _anuncios.SalvarAsync(anuncio);
                    }
                    return;
                }
                // Sem mudanca (mesma assinatura) nao reenvia: cada envio entra na fila da OLX.
                await EnviarAsync(veiculo, anuncio, Dados(anuncio), forcar: !anuncio.EstaPublicado, CancellationToken.None);
            }
            catch (OlxException ex)
            {
                // Em segundo plano: o erro fica no anuncio para a tela mostrar.
                _logger.LogWarning("OLX: veiculo {VeiculoId} nao sincronizou: {Erro}", veiculoId, ex.Message);
            }
        }

        /// <summary>Envia o "insert" (inclui ou altera). forcar = manda mesmo sem mudanca.</summary>
        private async Task EnviarAsync(Veiculo veiculo, VeiculoOlx anuncio, OlxAnuncioDados dados, bool forcar, CancellationToken ct)
        {
            try
            {
                var (ad, fotos) = await MontarAnuncioAsync(veiculo, dados);
                var assinatura = Assinatura(ad);
                if (!forcar && anuncio.EstaPublicado && assinatura == anuncio.VolAssinatura) return;

                var token = await ImportarAsync(ad, ct);
                anuncio.Enviado(token, assinatura, fotos);
                await _anuncios.SalvarAsync(anuncio);
            }
            catch (OlxException ex)
            {
                anuncio.Falhou(ex.Message);
                await _anuncios.SalvarAsync(anuncio);
                throw;
            }
        }

        private async Task<string> RemoverNaOlxAsync(int veiculoId, CancellationToken ct) =>
            await ImportarAsync(new Dictionary<string, object> { ["id"] = IdAnuncio(veiculoId), ["operation"] = "delete" }, ct);

        /// <summary>O anuncio no formato da OLX e quantas fotos foram.</summary>
        private async Task<(Dictionary<string, object> Ad, int Fotos)> MontarAnuncioAsync(Veiculo v, OlxAnuncioDados d)
        {
            var loja = await _lojas.GetByIdAsync(v.R_LojId);
            var faltando = Pendencias(v, loja, (await FotosAsync(v.VeiId)).Count);
            if (faltando.Count > 0) throw new OlxException("Antes de anunciar: " + string.Join("; ", faltando) + ".");

            var parametros = new Dictionary<string, object>
            {
                ["vehicle_brand"] = d.MarcaId!,
                ["vehicle_model"] = d.ModeloId!,
                ["vehicle_version"] = d.VersaoId!,
                ["regdate"] = OlxCodigos.AnoOlx(d.Ano ?? v.VeiAno),
                ["mileage"] = Math.Max(v.VeiKm, 0),
                ["fuel"] = d.Combustivel!,
                ["gearbox"] = d.Cambio!,
                ["carcolor"] = d.Cor!
            };
            var placa = Placa(v.VeiPlaca);
            if (placa.Length > 0) parametros["vehicle_tag"] = placa;
            // Opcional na OLX: so vai preenchido (vazio ou 0 faz a OLX recusar).
            if (!string.IsNullOrWhiteSpace(d.Portas)) parametros["doors"] = d.Portas;
            if (!string.IsNullOrWhiteSpace(d.Tipo)) parametros["cartype"] = d.Tipo;
            if (!string.IsNullOrWhiteSpace(d.Direcao)) parametros["car_steering"] = d.Direcao;
            if (!string.IsNullOrWhiteSpace(d.Potencia)) parametros["motorpower"] = d.Potencia;
            var opcionais = d.Opcionais.Where(o => !string.IsNullOrWhiteSpace(o) && o != "2").Distinct().ToList();
            if (opcionais.Count > 0) parametros["car_features"] = opcionais;
            if (d.AceitaTroca is { } troca) parametros["exchange"] = troca ? "1" : "2";
            if (d.UnicoDono is { } dono) parametros["owner"] = dono ? "1" : "2";

            var fotos = await FotosAsync(v.VeiId);
            var urlBase = await UrlPublicaAsync(loja);
            var imagens = fotos
                // O endpoint publico ja converte (HEIC/WebP/PNG) para JPEG.
                .Select(f => $"{urlBase}/api/imagens/file?path={Uri.EscapeDataString(f)}&max=1600&format=jpeg")
                .Distinct().ToList();

            var ad = new Dictionary<string, object>
            {
                ["id"] = IdAnuncio(v.VeiId),
                ["operation"] = "insert",
                ["category"] = OlxCodigos.CategoriaCarros,
                ["subject"] = Titulo(v, d),
                ["body"] = Descricao(v),
                ["phone"] = long.Parse(Telefone(loja)!, CultureInfo.InvariantCulture),
                ["type"] = "s",
                ["price"] = (long)Math.Round(v.VeiPreco, MidpointRounding.AwayFromZero),
                ["zipcode"] = SoDigitos(loja!.LojCEP),
                ["images"] = imagens,
                ["params"] = parametros
            };
            return (ad, imagens.Count);
        }

        /// <summary>PUT /import com um anuncio. Devolve o token para conferir o resultado.</summary>
        private async Task<string> ImportarAsync(Dictionary<string, object> ad, CancellationToken ct)
        {
            var token = await AccessTokenAsync();
            using var doc = await EnviarAsync(HttpMethod.Put, "/import", new { access_token = token, ad_list = new[] { ad } }, ct);
            var r = doc.RootElement;
            var codigo = Inteiro(r, "statusCode") ?? -1;
            if (codigo == 0 && Texto(r, "token") is { Length: > 0 } t) return t;

            var mensagem = OlxCodigos.StatusImportacao(codigo);
            var detalhes = ErrosDeValidacao(r);
            if (detalhes.Count > 0) mensagem = "A OLX recusou: " + string.Join("; ", detalhes) + ".";
            _logger.LogWarning("OLX: importacao recusada (statusCode {Codigo}) {Corpo}", codigo, Cortar(r.GetRawText(), 500));
            throw new OlxException(mensagem);
        }

        /// <summary>errors: [{ id, status, messages: [{ category }] }].</summary>
        private static List<string> ErrosDeValidacao(JsonElement r)
        {
            var lista = new List<string>();
            if (r.ValueKind != JsonValueKind.Object || !r.TryGetProperty("errors", out var erros) || erros.ValueKind != JsonValueKind.Array)
                return lista;
            foreach (var e in erros.EnumerateArray())
                lista.AddRange(Mensagens(e, "messages"));
            return lista.Distinct().ToList();
        }

        /// <summary>Lista de mensagens da OLX ({category}, {error} ou texto) traduzidas.</summary>
        private static IEnumerable<string> Mensagens(JsonElement e, string campo)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(campo, out var msgs)) yield break;
            var itens = msgs.ValueKind == JsonValueKind.Array ? msgs.EnumerateArray().ToList() : new List<JsonElement> { msgs };
            foreach (var m in itens)
            {
                var texto = m.ValueKind == JsonValueKind.String ? m.GetString()
                    : Texto(m, "category") ?? Texto(m, "error") ?? Texto(m, "message");
                if (!string.IsNullOrWhiteSpace(texto)) yield return OlxCodigos.Traduzir(texto);
            }
        }

        /// <summary>
        /// POST /import/{token}: resultado do ultimo envio. Enquanto a OLX
        /// processa (pending/queued) o token fica guardado para conferir de novo.
        /// </summary>
        private async Task ConferirAsync(VeiculoOlx anuncio, CancellationToken ct)
        {
            var token = await AccessTokenAsync();
            using var doc = await PostJsonAsync($"/import/{Uri.EscapeDataString(anuncio.VolToken)}", new { access_token = token }, ct);
            var r = doc.RootElement;
            if (!r.TryGetProperty("ads", out var ads)) return;

            var id = IdAnuncio(anuncio.R_VeiId);
            JsonElement? meu = null;
            if (ads.ValueKind == JsonValueKind.Object && ads.TryGetProperty(id, out var porId)) meu = porId;
            else if (ads.ValueKind == JsonValueKind.Array)
                meu = ads.EnumerateArray().Cast<JsonElement?>().FirstOrDefault(a => Texto(a!.Value, "id") == id);
            if (meu is not { } ad) return;

            var situacao = Texto(ad, "status") ?? "";
            if (situacao is "pending" or "queued" or "")
            {
                if (situacao != "" && situacao != anuncio.VolSituacao)
                {
                    anuncio.Processando(situacao);
                    await _anuncios.SalvarAsync(anuncio);
                }
                return;
            }
            var erros = Mensagens(ad, "message").Concat(Mensagens(ad, "image_errors")).Concat(Mensagens(ad, "imageErrors")).Distinct().ToList();
            var erro = situacao is "refused" or "error"
                ? "A OLX recusou o anúncio" + (erros.Count > 0 ? ": " + string.Join("; ", erros) : "") + "."
                : null;
            anuncio.Conferido(situacao, Texto(ad, "list_id")!, Texto(ad, "url")!, erro!);
            await _anuncios.SalvarAsync(anuncio);
        }

        public static void Validar(OlxAnuncioDados d)
        {
            var faltando = new List<string>();
            if (string.IsNullOrWhiteSpace(d.MarcaId)) faltando.Add("marca");
            if (string.IsNullOrWhiteSpace(d.ModeloId)) faltando.Add("modelo");
            if (string.IsNullOrWhiteSpace(d.VersaoId)) faltando.Add("versão");
            if (d.Ano is not > 1900) faltando.Add("ano");
            if (string.IsNullOrWhiteSpace(d.Combustivel)) faltando.Add("combustível");
            if (string.IsNullOrWhiteSpace(d.Cambio)) faltando.Add("câmbio");
            if (string.IsNullOrWhiteSpace(d.Cor)) faltando.Add("cor");
            if (faltando.Count > 0) throw new OlxException("Preencha: " + string.Join(", ", faltando) + ".");
        }

        private async Task<List<string>> PendenciasAsync(Veiculo v) =>
            Pendencias(v, await _lojas.GetByIdAsync(v.R_LojId), (await FotosAsync(v.VeiId)).Count);

        /// <summary>O que falta no cadastro para a OLX aceitar.</summary>
        public static List<string> Pendencias(Veiculo v, Loja? loja, int fotos)
        {
            var p = new List<string>();
            if (v.VeiPreco <= 0) p.Add("cadastre o preço do veículo");
            if (fotos == 0) p.Add("suba pelo menos uma foto");
            if (Placa(v.VeiPlaca).Length != 7) p.Add("cadastre a placa (a OLX confere a placa de carro usado)");
            if (Telefone(loja) == null) p.Add("cadastre o telefone ou WhatsApp da loja com DDD (Lojas)");
            if (SoDigitos(loja?.LojCEP).Length != 8) p.Add("cadastre o CEP da loja (Lojas)");
            return p;
        }

        /// <summary>DDD + numero (10 ou 11 digitos), sem o 55.</summary>
        public static string? Telefone(Loja? loja)
        {
            foreach (var t in new[] { loja?.LojWhatsApp, loja?.LojTel1, loja?.LojTel2 })
            {
                var d = SoDigitos(t);
                if (d.StartsWith("55") && d.Length is 12 or 13) d = d[2..];
                if (d.StartsWith('0')) d = d.TrimStart('0');
                if (d.Length is 10 or 11) return d;
            }
            return null;
        }

        private static string Titulo(Veiculo v, OlxAnuncioDados d)
        {
            var marca = !string.IsNullOrWhiteSpace(d.MarcaNome) ? d.MarcaNome
                : (v.VeiMarca ?? "").Split(" - ").Last();
            var modelo = !string.IsNullOrWhiteSpace(d.ModeloNome)
                ? $"{d.ModeloNome} {d.VersaoNome}"
                : v.VeiModelo;
            var titulo = System.Text.RegularExpressions.Regex.Replace($"{marca} {modelo} {d.Ano ?? v.VeiAno}", @"\s+", " ").Trim();
            return titulo.Length > 90 ? titulo[..90].TrimEnd() : titulo;
        }

        private static string Descricao(Veiculo v)
        {
            var texto = (v.VeiObservacao ?? "").Trim();
            if (texto.Length < 2)
                texto = $"{(v.VeiMarca ?? "").Split(" - ").Last()} {v.VeiModelo} {v.VeiAno}, {v.VeiKm.ToString("N0", new CultureInfo("pt-BR"))} km.".Trim();
            return texto.Length > 6000 ? texto[..6000] : texto;
        }

        private async Task<List<string>> FotosAsync(int veiculoId) =>
            (await _imagens.GetByVeiculoIdAsync(veiculoId))
                .Where(i => i.ImgSts).OrderBy(i => i.ImgOrdem).Select(i => i.ImgCaminho)
                .Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().Take(MaximoFotos).ToList();

        private static string Assinatura(Dictionary<string, object> ad) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(ad, Json))));

        private async Task<OlxAnuncioDados> SugerirAsync(Veiculo v, CancellationToken ct)
        {
            var d = new OlxAnuncioDados { Ano = v.VeiAno };
            var texto = v.VeiModelo;
            try
            {
                var marca = OlxCodigos.Marca(v.VeiMarca, await MarcasAsync(ct));
                d.MarcaId = marca?.Id;
                d.MarcaNome = marca?.Nome;
                if (marca != null)
                {
                    var modelo = OlxCodigos.Modelo(v.VeiModelo, await ModelosAsync(marca.Id, ct));
                    d.ModeloId = modelo?.Id;
                    d.ModeloNome = modelo?.Nome;
                    if (modelo != null)
                    {
                        var versao = OlxCodigos.Versao(v.VeiModelo, modelo, await VersoesAsync(marca.Id, modelo.Id, ct));
                        d.VersaoId = versao?.Id;
                        d.VersaoNome = versao?.Nome;
                        texto = (versao?.Nome ?? "") + " " + v.VeiModelo;
                    }
                }
            }
            catch (OlxException ex)
            {
                _logger.LogInformation("OLX: sugestao incompleta para o veiculo {VeiculoId}: {Erro}", v.VeiId, ex.Message);
            }
            d.Combustivel = OlxCodigos.Combustivel(texto);
            d.Cambio = OlxCodigos.Cambio(texto);
            d.Portas = OlxCodigos.Portas(texto);
            d.Potencia = OlxCodigos.Potencia(texto);
            d.Cor = OlxCodigos.Cor(v.VeiCor);
            d.Opcionais = OlxCodigos.Opcionais(v.VeiOpcionais);
            return d;
        }

        // ================================================================
        // HTTP
        // ================================================================

        private Task<JsonDocument> PostJsonAsync(string caminho, object corpo, CancellationToken ct) =>
            EnviarAsync(HttpMethod.Post, caminho, corpo, ct);

        /// <summary>Chamada com o token no corpo (import, car_info).</summary>
        private async Task<JsonDocument> EnviarAsync(HttpMethod metodo, string caminho, object corpo, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(metodo, _settings.ApiUrl.TrimEnd('/') + caminho)
            {
                Content = new StringContent(JsonSerializer.Serialize(corpo, Json), Encoding.UTF8, "application/json")
            };
            return (await ResponderAsync(req, caminho, ct, aceitar410: false))!;
        }

        /// <summary>Chamada com Authorization: Bearer (balance, ads, published).</summary>
        private async Task<JsonDocument?> BearerAsync(string caminho, CancellationToken ct, bool aceitar410 = false)
        {
            var token = await AccessTokenAsync();
            using var req = new HttpRequestMessage(HttpMethod.Get, _settings.ApiUrl.TrimEnd('/') + caminho);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await ResponderAsync(req, caminho, ct, aceitar410);
        }

        private async Task<JsonDocument?> ResponderAsync(HttpRequestMessage req, string caminho, CancellationToken ct, bool aceitar410)
        {
            req.Headers.Accept.ParseAdd("application/json");
            HttpResponseMessage resp;
            try { resp = await _http.SendAsync(req, ct); }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new OlxException("Não foi possível falar com a OLX agora. Tente de novo em alguns minutos.");
            }
            using (resp)
            {
                var texto = await resp.Content.ReadAsStringAsync(ct);
                if (resp.IsSuccessStatusCode)
                {
                    try { return string.IsNullOrWhiteSpace(texto) ? JsonDocument.Parse("{}") : JsonDocument.Parse(texto); }
                    catch (JsonException) { throw new OlxException("A OLX respondeu num formato inesperado. Tente de novo em alguns minutos."); }
                }
                if (resp.StatusCode == HttpStatusCode.Gone && aceitar410) return null;
                _logger.LogWarning("OLX recusou {Metodo} {Caminho}: HTTP {Status} {Corpo}", req.Method, caminho.Split('/').Take(2).Aggregate((a, b) => a + "/" + b),
                    (int)resp.StatusCode, Cortar(texto, 500));
                if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    // Sem refresh token: a loja precisa entrar de novo.
                    await _config.SetValorAsync(ChaveToken, "");
                    throw new OlxException("A conexão com a OLX não vale mais. Conecte de novo em Integrações → OLX.");
                }
                if (resp.StatusCode == HttpStatusCode.TooManyRequests)
                    throw new OlxException("A OLX pediu para esperar (muitos envios seguidos). Tente de novo em alguns minutos.");
                if ((int)resp.StatusCode >= 500)
                    throw new OlxException("A OLX está com instabilidade. Tente de novo em alguns minutos.");
                throw new OlxException($"A OLX recusou a operação (HTTP {(int)resp.StatusCode}).");
            }
        }

        // ================================================================
        // Configuracao
        // ================================================================

        private void ExigirConfigurado()
        {
            if (!_settings.Configurado)
                throw new OlxException("A integração com a OLX ainda está em liberação (falta o cadastro do ConnectVeiculos como integrador).");
        }

        private async Task<string> AccessTokenAsync()
        {
            ExigirConfigurado();
            return (await TokenAsync())?.AccessToken ?? throw new OlxException("Conecte a OLX em Integrações.");
        }

        private async Task<TokenSalvo?> TokenAsync()
        {
            var cifrado = await _config.GetValorAsync(ChaveToken);
            if (string.IsNullOrWhiteSpace(cifrado)) return null;
            try { return JsonSerializer.Deserialize<TokenSalvo>(_protector.Unprotect(cifrado), Json); }
            catch (Exception ex) when (ex is JsonException or CryptographicException or FormatException)
            {
                _logger.LogWarning(ex, "OLX: token guardado ilegivel no tenant {Tenant}", _tenant.TenantSlug);
                return null;
            }
        }

        private Task SalvarTokenAsync(TokenSalvo t) =>
            _config.SetValorAsync(ChaveToken, _protector.Protect(JsonSerializer.Serialize(t, Json)));

        // ================================================================
        // Auxiliares
        // ================================================================

        private static OlxAnuncioDados Dados(VeiculoOlx a) =>
            JsonSerializer.Deserialize<OlxAnuncioDados>(a.VolDadosJson, Json) ?? new();

        private static OlxVeiculoInfo Info(VeiculoOlx? a, OlxAnuncioDados dados, bool sugestao, List<string> pendencias) => new()
        {
            Publicado = a?.EstaPublicado == true,
            Status = a?.VolStatus ?? "",
            Situacao = a?.VolSituacao,
            Url = a?.VolUrl,
            Erro = a?.VolErro,
            PublicadoEm = a?.VolPublicadoEm,
            AtualizadoEm = a?.VolAtualizadoEm,
            Fotos = a?.VolFotos ?? 0,
            Dados = dados,
            Sugestao = sugestao,
            Pendencias = pendencias
        };

        private static Task<string> UrlPublicaAsync(Loja? loja)
        {
            // Mesma regra do NaPista: a OLX baixa a foto de fora.
            var url = loja?.LojUrlCatalogo?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                url = (Environment.GetEnvironmentVariable("PUBLIC_SITE_URL") ?? "https://connectveiculos.dev.br").TrimEnd('/');
            return Task.FromResult(url);
        }

        private static string? Origem(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var u) ? $"{u.Scheme}://{u.Authority}" : null;

        public static string Placa(string? placa) =>
            new string((placa ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        private static string SoDigitos(string? v) => new((v ?? "").Where(char.IsDigit).ToArray());

        private static string Cortar(string v, int max) => v.Length > max ? v[..max] : v;

        private static string? Texto(JsonElement e, string nome)
            => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nome, out var p)
                ? p.ValueKind switch
                {
                    JsonValueKind.String => p.GetString(),
                    JsonValueKind.Number => p.GetRawText(),
                    _ => null
                }
                : null;

        private static int? Inteiro(JsonElement e, string nome)
            => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nome, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var v) ? v : null;
    }
}
