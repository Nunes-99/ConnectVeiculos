using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ConnectVeiculos.Core.Entities.Integracoes;
using ConnectVeiculos.Core.Entities.Leads;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Integracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Core.Napista;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.Infrastructure.Services.Napista
{
    /// <inheritdoc cref="INapistaService"/>
    public class NapistaService : INapistaService
    {
        public const string ChaveToken = "NAPISTA_TOKEN";
        public const string ChaveConta = "NAPISTA_CONTA";
        public const string OrigemLead = "NAPISTA";

        private static readonly TimeSpan RenovarAntes = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan DuracaoCatalogo = TimeSpan.FromHours(12);
        private const int MaximoFotos = 20;
        private static readonly string[] Categorias = { "CAR", "MOTORCYCLE", "TRUCK" };

        // Renovar o token cancela o refresh anterior: uma renovacao por loja de cada vez.
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> TravasToken = new();
        // O catalogo e' igual para todas as lojas do mesmo ambiente.
        private static readonly ConcurrentDictionary<string, (object Valor, DateTime Expira)> Catalogo = new();

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _http;
        private readonly NapistaSettings _settings;
        private readonly IConfiguracaoSistemaRepository _config;
        private readonly ITokenProtector _protector;
        private readonly IOAuthStateProtector _state;
        private readonly ITenantContext _tenant;
        private readonly IVeiculoRepository _veiculos;
        private readonly IVeiculoImagemRepository _imagens;
        private readonly ILojaRepository _lojas;
        private readonly IVeiculoNapistaRepository _anuncios;
        private readonly ConnectVeiculosDbContext _db;
        private readonly ILogger<NapistaService> _logger;

        public NapistaService(HttpClient http, IOptions<NapistaSettings> settings, IConfiguracaoSistemaRepository config,
            ITokenProtector protector, IOAuthStateProtector state, ITenantContext tenant, IVeiculoRepository veiculos,
            IVeiculoImagemRepository imagens, ILojaRepository lojas, IVeiculoNapistaRepository anuncios,
            ConnectVeiculosDbContext db, ILogger<NapistaService> logger)
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
            _db = db;
            _logger = logger;
        }

        private sealed class TokenSalvo
        {
            public string AccessToken { get; set; } = "";
            public string RefreshToken { get; set; } = "";
            public DateTime ExpiraEm { get; set; }
            /// <summary>Nulo: refresh sem validade (offline_access).</summary>
            public DateTime? RefreshExpiraEm { get; set; }
        }

        private sealed class ContaSalva
        {
            public DateTime ConectadoEm { get; set; }
            public string? SellerId { get; set; }
            public string? LojaNome { get; set; }
        }

        // ================================================================
        // Conexao (OAuth / Keycloak)
        // ================================================================

        public async Task<NapistaStatus> StatusAsync(CancellationToken ct = default)
        {
            var status = new NapistaStatus
            {
                Configurado = _settings.Configurado,
                Desenvolvimento = _settings.Desenvolvimento,
                SiteConexao = Origem(_settings.RedirectUri)
            };
            var token = await TokenAsync();
            if (!_settings.Configurado || token == null) return status;

            var conta = await ContaAsync() ?? new ContaSalva();
            status.Conectado = true;
            status.ConectadoEm = conta.ConectadoEm;
            status.ValidaAte = token.RefreshExpiraEm;
            status.SellerId = conta.SellerId;
            status.LojaNome = conta.LojaNome;
            try
            {
                status.Lojas = await ListarLojasAsync(ct);
            }
            catch (NapistaException ex)
            {
                status.Erro = ex.Message;
                status.Conectado = await TokenAsync() != null; // a conexao pode ter vencido agora
            }
            return status;
        }

        public string UrlAutorizacao()
        {
            ExigirConfigurado();
            var state = _state.Proteger(_tenant.TenantSlug ?? "");
            return $"{_settings.AuthUrl.TrimEnd('/')}/auth?response_type=code" +
                   $"&client_id={Uri.EscapeDataString(_settings.ClientId!)}" +
                   $"&redirect_uri={Uri.EscapeDataString(_settings.RedirectUri)}" +
                   $"&scope={Uri.EscapeDataString(_settings.Escopos)}" +
                   $"&state={Uri.EscapeDataString(state)}";
        }

        public async Task<NapistaStatus> ConectarAsync(string code, string? state, CancellationToken ct = default)
        {
            ExigirConfigurado();
            if (string.IsNullOrWhiteSpace(code)) throw new NapistaException("O NaPista não devolveu o código de autorização.");
            try
            {
                _state.Validar(state, _tenant.TenantSlug ?? "");
            }
            catch (OAuthStateException ex)
            {
                throw new NapistaException(ex.Message);
            }

            var token = await PedirTokenAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = _settings.ClientId!,
                ["code"] = code,
                ["redirect_uri"] = _settings.RedirectUri
            }, ct);
            await SalvarTokenAsync(token);

            var conta = new ContaSalva { ConectadoEm = DateTime.UtcNow };
            var lojas = await ListarLojasAsync(ct);
            if (lojas.Count == 1)
            {
                conta.SellerId = lojas[0].SellerId;
                conta.LojaNome = lojas[0].Nome;
            }
            await SalvarContaAsync(conta);
            _logger.LogInformation("NaPista conectado no tenant {Tenant} ({Lojas} loja(s))", _tenant.TenantSlug, lojas.Count);
            return await StatusAsync(ct);
        }

        public async Task<NapistaStatus> EscolherLojaAsync(string sellerId, CancellationToken ct = default)
        {
            var loja = (await ListarLojasAsync(ct)).FirstOrDefault(l => l.SellerId == sellerId)
                       ?? throw new NapistaException("Essa loja não aparece para o usuário conectado no NaPista.");
            var conta = await ContaAsync() ?? new ContaSalva { ConectadoEm = DateTime.UtcNow };
            conta.SellerId = loja.SellerId;
            conta.LojaNome = loja.Nome;
            await SalvarContaAsync(conta);
            return await StatusAsync(ct);
        }

        public async Task DesconectarAsync(CancellationToken ct = default)
        {
            var token = await TokenAsync();
            if (token != null && _settings.Configurado)
            {
                // Encerra a sessao no NaPista; se falhar, desconecta aqui do mesmo jeito.
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Post, $"{_settings.AuthUrl.TrimEnd('/')}/logout")
                    {
                        Content = new FormUrlEncodedContent(new Dictionary<string, string>
                        {
                            ["client_id"] = _settings.ClientId!,
                            ["refresh_token"] = token.RefreshToken
                        })
                    };
                    using var _ = await _http.SendAsync(req, ct);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    _logger.LogInformation("NaPista: logout nao respondeu ({Erro})", ex.Message);
                }
            }
            await _config.SetValorAsync(ChaveToken, "");
            await _config.SetValorAsync(ChaveConta, "");
        }

        // ================================================================
        // Catalogo
        // ================================================================

        public Task<IReadOnlyList<NapistaItem>> MarcasAsync(string categoria, CancellationToken ct = default)
        {
            var cat = Categoria(categoria);
            return EmCacheAsync($"marcas:{cat}", async () => Itens(await GetAsync($"/catalog/makes/{cat}", ct)));
        }

        public Task<IReadOnlyList<NapistaItem>> ModelosAsync(string categoria, string marcaId, int? ano, CancellationToken ct = default)
        {
            var cat = Categoria(categoria);
            if (string.IsNullOrWhiteSpace(marcaId)) throw new NapistaException("Escolha a marca.");
            var q = ano is > 1900 ? $"?modelYear={ano}" : "";
            return EmCacheAsync($"modelos:{cat}:{marcaId}:{ano}",
                async () => Itens(await GetAsync($"/catalog/{cat}/make/{Uri.EscapeDataString(marcaId)}/models{q}", ct)));
        }

        public Task<IReadOnlyList<NapistaItem>> VersoesAsync(string categoria, string modeloId, int? ano, CancellationToken ct = default)
        {
            var cat = Categoria(categoria);
            if (string.IsNullOrWhiteSpace(modeloId)) throw new NapistaException("Escolha o modelo.");
            var q = $"?modelId={Uri.EscapeDataString(modeloId)}" + (ano is > 1900 ? $"&modelYear={ano}" : "");
            return EmCacheAsync($"versoes:{cat}:{modeloId}:{ano}", async () => Itens(await GetAsync($"/catalog/versions/{cat}{q}", ct)));
        }

        public Task<NapistaAtributos> AtributosAsync(string categoria, CancellationToken ct = default)
        {
            var cat = Categoria(categoria);
            return EmCacheAsync($"atributos:{cat}", async () =>
            {
                using var doc = await GetAsync($"/catalog/attributes?category={cat}", ct);
                return LerAtributos(doc.RootElement);
            });
        }

        /// <summary>
        /// A resposta traz grupos { "colors": { "items": [...] }, "fuelTypes": ... }.
        /// So "colors" e "fuelTypes" aparecem no exemplo da documentacao; os outros
        /// grupos sao reconhecidos pelo nome, e o cambio cai no enum documentado.
        /// </summary>
        public static NapistaAtributos LerAtributos(JsonElement r)
        {
            var a = new NapistaAtributos();
            if (r.ValueKind == JsonValueKind.Object)
            {
                foreach (var grupo in r.EnumerateObject())
                {
                    var nome = grupo.Name.ToLowerInvariant();
                    var itens = Itens(grupo.Value);
                    if (nome.Contains("color")) a.Cores = itens;
                    else if (nome.Contains("fuel")) a.Combustiveis = itens;
                    else if (nome.Contains("transmission")) a.Cambios = itens;
                    else if (nome.Contains("equipment")) a.Opcionais = itens;
                }
            }
            if (a.Cambios.Count == 0)
                a.Cambios = new() { new() { Id = "MANUAL", Nome = "Manual" }, new() { Id = "AUTOMATIC", Nome = "Automático" } };
            return a;
        }

        // ================================================================
        // Anuncio do veiculo
        // ================================================================

        public async Task<NapistaVeiculoInfo> ObterVeiculoAsync(int veiculoId, CancellationToken ct = default)
        {
            var veiculo = await _veiculos.GetByIdAsync(veiculoId) ?? throw new NapistaException("Veículo não encontrado.");
            var anuncio = await _anuncios.GetByVeiculoIdAsync(veiculoId);
            if (anuncio?.VnpDadosJson is { Length: > 0 })
                return Info(anuncio, Dados(anuncio), false);

            await ContaObrigatoriaAsync();
            return Info(anuncio, await SugerirAsync(veiculo, ct), true);
        }

        public async Task<NapistaVeiculoInfo> PublicarAsync(int veiculoId, NapistaAnuncioDados dados, CancellationToken ct = default)
        {
            var conta = await ContaObrigatoriaAsync();
            var veiculo = await _veiculos.GetByIdAsync(veiculoId) ?? throw new NapistaException("Veículo não encontrado.");
            if (veiculo.VeiSts != "D") throw new NapistaException("Só veículo disponível pode ser anunciado.");
            dados.Categoria = Categoria(dados.Categoria);
            Validar(veiculo, dados);

            var anuncio = await _anuncios.GetByVeiculoIdAsync(veiculoId) ?? new VeiculoNapista(veiculoId, "");
            var anterior = string.IsNullOrEmpty(anuncio.VnpDadosJson) ? null : Dados(anuncio);
            anuncio.SalvarDados(JsonSerializer.Serialize(dados, Json));
            await EnviarAsync(conta, veiculo, anuncio, dados, anterior, ct);
            return Info(anuncio, dados, false);
        }

        public async Task<NapistaVeiculoInfo> PausarAsync(int veiculoId, CancellationToken ct = default)
        {
            var conta = await ContaObrigatoriaAsync();
            var anuncio = await _anuncios.GetByVeiculoIdAsync(veiculoId);
            if (anuncio == null || !anuncio.EstaPublicado) throw new NapistaException("O veículo não está anunciado no NaPista.");
            await MudarStatusAsync(conta, anuncio, "UNPUBLISHED", ct);
            anuncio.Removido(peloUsuario: true);
            await _anuncios.SalvarAsync(anuncio);
            return Info(anuncio, Dados(anuncio), false);
        }

        public async Task SincronizarVeiculoAsync(int veiculoId)
        {
            var anuncio = await _anuncios.GetByVeiculoIdAsync(veiculoId);
            if (anuncio == null || string.IsNullOrEmpty(anuncio.VnpDadosJson) || anuncio.Pausado) return;
            if (!_settings.Configurado || await TokenAsync() == null) return;
            var conta = await ContaAsync();
            if (conta?.SellerId == null) return;

            var veiculo = await _veiculos.GetByIdAsync(veiculoId);
            try
            {
                if (veiculo == null || veiculo.VeiSts != "D")
                {
                    if (anuncio.EstaPublicado)
                    {
                        // Vendido aparece como vendido no NaPista; inativo/reservado sai do ar.
                        await MudarStatusAsync(conta, anuncio, veiculo?.VeiSts == "V" ? "SOLD" : "UNPUBLISHED", CancellationToken.None);
                        anuncio.Removido();
                        await _anuncios.SalvarAsync(anuncio);
                    }
                    return;
                }
                await EnviarAsync(conta, veiculo, anuncio, Dados(anuncio), Dados(anuncio), CancellationToken.None);
            }
            catch (NapistaException ex)
            {
                // Em segundo plano: o erro fica no anuncio para a tela mostrar.
                _logger.LogWarning("NaPista: veiculo {VeiculoId} nao sincronizou: {Erro}", veiculoId, ex.Message);
                if (anuncio.VnpErro != ex.Message)
                {
                    anuncio.Falhou(ex.Message);
                    await _anuncios.SalvarAsync(anuncio);
                }
            }
        }

        private async Task EnviarAsync(ContaSalva conta, Veiculo veiculo, VeiculoNapista anuncio, NapistaAnuncioDados dados,
            NapistaAnuncioDados? anterior, CancellationToken ct)
        {
            var sellerId = Seller(conta);
            var corpo = new
            {
                versionId = dados.VersaoId,
                description = string.IsNullOrWhiteSpace(veiculo.VeiObservacao) ? null : veiculo.VeiObservacao.Trim(),
                modelYear = dados.AnoModelo,
                manufacturedYear = dados.AnoFabricacao,
                mileage = Math.Max(veiculo.VeiKm, 0),
                armoured = dados.Blindado,
                price = (int)Math.Round(veiculo.VeiPreco, MidpointRounding.AwayFromZero),
                plate = string.IsNullOrWhiteSpace(Placa(veiculo.VeiPlaca)) ? null : Placa(veiculo.VeiPlaca),
                fuelType = dados.Combustivel,
                color = dados.Cor,
                transmissionType = dados.Cambio,
                equipments = dados.Opcionais,
                numberOfDoors = dados.Portas,
                status = "PUBLISHED"
            };

            try
            {
                // Versao e anos nao mudam num anuncio existente (documentacao):
                // trocou, o anuncio antigo sai e entra um novo.
                if (anuncio.TemAnuncio && anterior != null && MudouIdentidade(anterior, dados))
                {
                    await RemoverNoNapistaAsync(sellerId, anuncio.VnpAnuncioId, ct);
                    anuncio.Removido();
                    anuncio.Publicado(""); // zera o id para o POST abaixo
                }

                string id;
                string? statusNoNapista;
                if (anuncio.TemAnuncio)
                {
                    using var doc = await EnviarJsonAsync(HttpMethod.Patch, $"/seller/{sellerId}/offer/{anuncio.VnpAnuncioId}", corpo, ct, aceitar404: true);
                    if (doc == null)
                    {
                        // Sumiu do NaPista (apagado la): publica de novo.
                        using var novo = await EnviarJsonAsync(HttpMethod.Post, $"/seller/{sellerId}/offer", corpo, ct);
                        (id, statusNoNapista) = (Texto(novo!.RootElement, "id") ?? "", Texto(novo.RootElement, "status"));
                    }
                    else
                    {
                        (id, statusNoNapista) = (anuncio.VnpAnuncioId, Texto(doc.RootElement, "status"));
                    }
                }
                else
                {
                    using var doc = await EnviarJsonAsync(HttpMethod.Post, $"/seller/{sellerId}/offer", corpo, ct);
                    (id, statusNoNapista) = (Texto(doc!.RootElement, "id") ?? "", Texto(doc.RootElement, "status"));
                }
                if (string.IsNullOrEmpty(id)) throw new NapistaException("O NaPista não devolveu o número do anúncio.");

                anuncio.Publicado(id);
                await _anuncios.SalvarAsync(anuncio);

                // O exemplo da documentacao volta sem status: confere e publica se precisar.
                if (statusNoNapista != "PUBLISHED")
                    await MudarStatusAsync(conta, anuncio, "PUBLISHED", ct);
            }
            catch (NapistaException ex)
            {
                anuncio.Falhou(ex.Message);
                await _anuncios.SalvarAsync(anuncio);
                throw;
            }

            try
            {
                await EnviarFotosAsync(sellerId, veiculo, anuncio, ct);
            }
            catch (NapistaException ex)
            {
                // O anuncio esta no ar; foto pendente nao desfaz isso.
                anuncio.Falhou("Anúncio publicado, mas as fotos não foram: " + ex.Message);
                await _anuncios.SalvarAsync(anuncio);
            }
        }

        /// <summary>
        /// Fotos por URL publica, na ordem do cadastro. O NaPista substitui as
        /// anteriores a cada envio, entao so manda quando a lista mudou.
        /// </summary>
        private async Task EnviarFotosAsync(string sellerId, Veiculo veiculo, VeiculoNapista anuncio, CancellationToken ct)
        {
            var fotos = (await _imagens.GetByVeiculoIdAsync(veiculo.VeiId))
                .Where(i => i.ImgSts).OrderBy(i => i.ImgOrdem).Take(MaximoFotos).ToList();
            var assinatura = string.Join("|", fotos.Select(f => f.ImgCaminho));
            if (fotos.Count == 0 || assinatura == (anuncio.VnpFotosAssinatura ?? "")) return;

            var urlBase = await UrlPublicaAsync(veiculo.R_LojId);
            var lista = fotos.Select((f, i) => new
            {
                // O endpoint publico ja converte (HEIC/WebP/PNG) para JPEG de ate 1200px.
                url = $"{urlBase}/api/imagens/file?path={Uri.EscapeDataString(f.ImgCaminho)}&max=1200&format=jpeg",
                order = i
            }).ToList();
            using (await EnviarJsonAsync(HttpMethod.Post, $"/seller/{sellerId}/offer/{anuncio.VnpAnuncioId}/photos/url", new { list = lista }, ct)) { }
            anuncio.FotosEnviadas(assinatura, fotos.Count);
            await _anuncios.SalvarAsync(anuncio);
        }

        private async Task MudarStatusAsync(ContaSalva conta, VeiculoNapista anuncio, string status, CancellationToken ct)
        {
            using (await EnviarJsonAsync(HttpMethod.Put, $"/seller/{Seller(conta)}/offer/{anuncio.VnpAnuncioId}/{status}", null, ct)) { }
        }

        private async Task RemoverNoNapistaAsync(string sellerId, string anuncioId, CancellationToken ct)
        {
            using (await EnviarJsonAsync(HttpMethod.Delete, $"/seller/{sellerId}/offer/{anuncioId}", null, ct, aceitar404: true)) { }
        }

        private static bool MudouIdentidade(NapistaAnuncioDados a, NapistaAnuncioDados b) =>
            a.VersaoId != b.VersaoId || a.AnoModelo != b.AnoModelo || a.AnoFabricacao != b.AnoFabricacao;

        public static void Validar(Veiculo veiculo, NapistaAnuncioDados d)
        {
            var faltando = new List<string>();
            if (string.IsNullOrWhiteSpace(d.VersaoId)) faltando.Add("versão");
            if (d.AnoModelo is not > 1900) faltando.Add("ano do modelo");
            if (d.AnoFabricacao is not > 1900) faltando.Add("ano de fabricação");
            if (string.IsNullOrWhiteSpace(d.Cambio)) faltando.Add("câmbio");
            if (string.IsNullOrWhiteSpace(d.Combustivel)) faltando.Add("combustível");
            if (string.IsNullOrWhiteSpace(d.Cor)) faltando.Add("cor");
            if (faltando.Count > 0) throw new NapistaException("Preencha: " + string.Join(", ", faltando) + ".");
            if (d.AnoFabricacao > d.AnoModelo || d.AnoModelo - d.AnoFabricacao > 1)
                throw new NapistaException("O ano de fabricação tem de ser o do modelo ou um a menos.");
            if (veiculo.VeiPreco <= 0) throw new NapistaException("Cadastre o preço do veículo.");
        }

        private async Task<NapistaAnuncioDados> SugerirAsync(Veiculo v, CancellationToken ct)
        {
            var d = new NapistaAnuncioDados { AnoModelo = v.VeiAno, AnoFabricacao = v.VeiAno };
            try
            {
                var marca = NapistaSugestao.Marca(v.VeiMarca, await MarcasAsync(d.Categoria, ct));
                d.MarcaId = marca?.Id;
                NapistaItem? versao = null;
                if (marca != null)
                {
                    var modelo = NapistaSugestao.Modelo(v.VeiModelo, await ModelosAsync(d.Categoria, marca.Id, null, ct));
                    d.ModeloId = modelo?.Id;
                    if (modelo != null)
                    {
                        versao = NapistaSugestao.Versao(v.VeiModelo, modelo, await VersoesAsync(d.Categoria, modelo.Id, v.VeiAno, ct));
                        d.VersaoId = versao?.Id;
                    }
                }
                var texto = (versao?.Nome ?? "") + " " + v.VeiModelo;
                var atributos = await AtributosAsync(d.Categoria, ct);
                d.Combustivel = NapistaSugestao.Combustivel(texto);
                d.Cambio = NapistaSugestao.Cambio(texto);
                d.Portas = NapistaSugestao.Portas(texto);
                d.Cor = NapistaSugestao.Cor(v.VeiCor, atributos.Cores);
                d.Opcionais = NapistaSugestao.Opcionais(v.VeiOpcionais, atributos.Opcionais);
            }
            catch (NapistaException ex)
            {
                _logger.LogInformation("NaPista: sugestao incompleta para o veiculo {VeiculoId}: {Erro}", v.VeiId, ex.Message);
            }
            return d;
        }

        // ================================================================
        // Leads
        // ================================================================

        public async Task<NapistaLeadsResultado> ImportarLeadsAsync(int dias, CancellationToken ct = default)
        {
            var conta = await ContaObrigatoriaAsync();
            var sellerId = Seller(conta);
            dias = Math.Clamp(dias, 1, 365);
            var hoje = DateTime.UtcNow.Date;
            var filtro = $"initialDate={hoje.AddDays(-dias):yyyy-MM-dd}&endDate={hoje:yyyy-MM-dd}";

            var leads = new List<JsonElement>();
            for (var pagina = 1; pagina <= 20; pagina++)
            {
                using var doc = await GetAsync($"/seller/{sellerId}/leads?page={pagina}&size=200&{filtro}", ct);
                var r = doc.RootElement;
                if (r.TryGetProperty("pageData", out var dados) && dados.ValueKind == JsonValueKind.Array)
                    leads.AddRange(dados.EnumerateArray().Select(x => x.Clone()));
                var total = r.TryGetProperty("totalPages", out var tp) && tp.TryGetInt32(out var t) ? t : 1;
                if (pagina >= total) break;
            }

            var resultado = new NapistaLeadsResultado { Encontrados = leads.Count };
            if (leads.Count == 0) return resultado;

            var existentes = (await _db.Leads.AsNoTracking()
                    .Where(l => l.LeaExternoId != null && l.LeaExternoId.StartsWith("napista:"))
                    .Select(l => l.LeaExternoId).ToListAsync(ct))
                .ToHashSet();
            var veiculos = (await _veiculos.GetAllAsync()).ToList();
            var lojaPadrao = (await _lojas.GetAllAsync()).OrderBy(l => l.LojId).FirstOrDefault()?.LojId;

            foreach (var l in leads)
            {
                var externo = "napista:" + (Texto(l, "id") ?? "");
                if (externo == "napista:" || existentes.Contains(externo)) { resultado.JaExistiam++; continue; }

                var placa = Placa(Texto(l, "vehiclePlate"));
                var veiculo = placa.Length > 0 ? veiculos.FirstOrDefault(v => Placa(v.VeiPlaca) == placa) : null;
                var tel = l.TryGetProperty("clientPhone", out var fone) && fone.ValueKind == JsonValueKind.Object
                    ? (Texto(fone, "areaCode") ?? "") + (Texto(fone, "number") ?? "") : "";

                var obs = new StringBuilder("Lead do NaPista");
                if (Texto(l, "updatedAt") is { } quando && DateTime.TryParse(quando, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dt))
                    obs.Append($" em {dt.ToLocalTime():dd/MM/yyyy HH:mm}");
                obs.Append('.');
                if (Texto(l, "vehicleVersion") is { Length: > 0 } versao) obs.Append($" Veículo: {versao}");
                if (placa.Length > 0) obs.Append($" ({placa})");
                if (Texto(l, "contactMessage") is { Length: > 0 } msg) obs.Append($". Mensagem: {msg}");
                if (Texto(l, "clientCreditStatus") == "QUALIFIED") obs.Append(". Crédito pré-aprovado no NaPista");
                if (Decimal(l, "installmentValue") is { } parcela && Inteiro(l, "installments") is { } n)
                    obs.Append($". Simulou {n}x de {parcela.ToString("C", new CultureInfo("pt-BR"))}");
                var observacao = obs.ToString();

                var lead = new Lead(0, veiculo?.VeiId, veiculo?.R_LojId ?? lojaPadrao,
                    Limitar(Texto(l, "clientName") ?? "Cliente NaPista", 200), Limitar(SoDigitos(tel), 20),
                    Limitar(Texto(l, "clientEmail") ?? "", 255), OrigemLead, "NOVO", Limitar(observacao, 1000),
                    leaEntrada: Decimal(l, "upfrontValue"), leaParcelas: Inteiro(l, "installments"));
                lead.DefinirExternoId(externo);
                _db.Leads.Add(lead);
                existentes.Add(externo);
                resultado.Importados++;
            }
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("NaPista: {Importados} lead(s) importado(s), {Ja} ja existiam", resultado.Importados, resultado.JaExistiam);
            return resultado;
        }

        // ================================================================
        // HTTP
        // ================================================================

        private async Task<List<NapistaLoja>> ListarLojasAsync(CancellationToken ct)
        {
            using var doc = await GetAsync("/seller/access", ct);
            var r = doc.RootElement;
            if (!r.TryGetProperty("sellers", out var lista) || lista.ValueKind != JsonValueKind.Array) return new();
            return lista.EnumerateArray()
                .Select(s => new NapistaLoja
                {
                    SellerId = Texto(s, "sellerId") ?? "",
                    Nome = Texto(s, "name") ?? "",
                    Cnpj = Texto(s, "sellerDocument")
                })
                .Where(s => s.SellerId != "")
                .ToList();
        }

        private async Task<JsonDocument> GetAsync(string caminho, CancellationToken ct)
            => (await EnviarJsonAsync(HttpMethod.Get, caminho, null, ct))
               ?? throw new NapistaException("O NaPista não encontrou o que foi pedido.");

        /// <summary>
        /// Chamada a API com o token da loja. 401 renova o token uma vez e
        /// repete. aceitar404 devolve null em vez de erro (anuncio apagado la).
        /// </summary>
        private async Task<JsonDocument?> EnviarJsonAsync(HttpMethod metodo, string caminho, object? corpo, CancellationToken ct, bool aceitar404 = false)
        {
            ExigirConfigurado();
            for (var tentativa = 0; ; tentativa++)
            {
                var token = await AccessTokenAsync(forcarRenovacao: tentativa > 0, ct);
                using var req = new HttpRequestMessage(metodo, _settings.ApiUrl.TrimEnd('/') + caminho);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                req.Headers.Accept.ParseAdd("application/json");
                if (corpo != null)
                    req.Content = new StringContent(JsonSerializer.Serialize(corpo, Json), Encoding.UTF8, "application/json");

                HttpResponseMessage resp;
                try { resp = await _http.SendAsync(req, ct); }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    throw new NapistaException("Não foi possível falar com o NaPista agora. Tente de novo em alguns minutos.");
                }
                using (resp)
                {
                    if (resp.StatusCode == HttpStatusCode.Unauthorized && tentativa == 0) continue;
                    var texto = await resp.Content.ReadAsStringAsync(ct);
                    if (resp.IsSuccessStatusCode)
                        return string.IsNullOrWhiteSpace(texto) ? JsonDocument.Parse("{}") : JsonDocument.Parse(texto);
                    if (resp.StatusCode == HttpStatusCode.NotFound && aceitar404) return null;
                    _logger.LogWarning("NaPista recusou {Metodo} {Caminho}: HTTP {Status} {Corpo}", metodo, caminho, (int)resp.StatusCode,
                        texto.Length > 500 ? texto[..500] : texto);
                    throw new NapistaException(MensagemDeErro(resp.StatusCode, texto));
                }
            }
        }

        public static string MensagemDeErro(HttpStatusCode status, string corpo)
        {
            if (status == HttpStatusCode.Unauthorized)
                return "A conexão com o NaPista não vale mais. Conecte de novo em Integrações → NaPista.";
            if (status == HttpStatusCode.Forbidden)
                return "A loja escolhida em Integrações → NaPista não tem acesso a este anúncio.";
            string? detalhe = null;
            try
            {
                using var doc = JsonDocument.Parse(corpo);
                var r = doc.RootElement;
                detalhe = Texto(r, "message") ?? Texto(r, "detail") ?? Texto(r, "error_description");
                if (r.ValueKind == JsonValueKind.Object && r.TryGetProperty("errors", out var erros) && erros.ValueKind == JsonValueKind.Array)
                {
                    var lista = erros.EnumerateArray()
                        .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : Texto(e, "message") ?? Texto(e, "defaultMessage"))
                        .Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
                    if (lista.Count > 0) detalhe = string.Join("; ", lista);
                }
            }
            catch (JsonException) { }
            if ((int)status >= 500)
                return "O NaPista está com instabilidade. Tente de novo em alguns minutos.";
            return !string.IsNullOrWhiteSpace(detalhe)
                ? $"O NaPista recusou: {detalhe}"
                : $"O NaPista recusou a operação (HTTP {(int)status}).";
        }

        private async Task<string> AccessTokenAsync(bool forcarRenovacao, CancellationToken ct)
        {
            var token = await TokenAsync() ?? throw new NapistaException("Conecte o NaPista em Integrações.");
            if (!forcarRenovacao && token.ExpiraEm - DateTime.UtcNow > RenovarAntes) return token.AccessToken;

            var trava = TravasToken.GetOrAdd(_tenant.TenantSlug ?? "", _ => new SemaphoreSlim(1, 1));
            await trava.WaitAsync(ct);
            try
            {
                // Reler dentro da trava: outra requisicao pode ter acabado de renovar.
                var atual = await TokenAsync() ?? throw new NapistaException("Conecte o NaPista em Integrações.");
                if (atual.AccessToken != token.AccessToken || (!forcarRenovacao && atual.ExpiraEm - DateTime.UtcNow > RenovarAntes))
                    return atual.AccessToken;
                try
                {
                    var novo = await PedirTokenAsync(new Dictionary<string, string>
                    {
                        ["grant_type"] = "refresh_token",
                        ["client_id"] = _settings.ClientId!,
                        ["refresh_token"] = atual.RefreshToken
                    }, ct);
                    await SalvarTokenAsync(novo);
                    return novo.AccessToken;
                }
                catch (NapistaException)
                {
                    // Refresh vencido: a loja precisa entrar de novo. Limpa para a tela mostrar "Conectar".
                    await _config.SetValorAsync(ChaveToken, "");
                    throw new NapistaException("A conexão com o NaPista venceu. Conecte de novo em Integrações → NaPista.");
                }
            }
            finally
            {
                trava.Release();
            }
        }

        private async Task<TokenSalvo> PedirTokenAsync(Dictionary<string, string> campos, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_settings.AuthUrl.TrimEnd('/')}/token")
            {
                Content = new FormUrlEncodedContent(campos)
            };
            HttpResponseMessage resp;
            try { resp = await _http.SendAsync(req, ct); }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new NapistaException("Não foi possível falar com o NaPista agora. Tente de novo em instantes.");
            }
            using (resp)
            {
                var texto = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("NaPista: token recusado ({Status}) {Corpo}", (int)resp.StatusCode, texto.Length > 300 ? texto[..300] : texto);
                    throw new NapistaException("O NaPista recusou a autorização. Conecte de novo em Integrações.");
                }
                using var doc = JsonDocument.Parse(texto);
                var r = doc.RootElement;
                var access = Texto(r, "access_token");
                var refresh = Texto(r, "refresh_token");
                if (string.IsNullOrEmpty(access) || string.IsNullOrEmpty(refresh))
                    throw new NapistaException("O NaPista não devolveu a chave de acesso.");
                var agora = DateTime.UtcNow;
                var expira = Inteiro(r, "expires_in") ?? 300;
                var refreshExpira = Inteiro(r, "refresh_expires_in") ?? 0;
                return new TokenSalvo
                {
                    AccessToken = access,
                    RefreshToken = refresh,
                    ExpiraEm = agora.AddSeconds(expira),
                    // 0 = sem validade (offline_access no Keycloak).
                    RefreshExpiraEm = refreshExpira > 0 ? agora.AddSeconds(refreshExpira) : null
                };
            }
        }

        // ================================================================
        // Configuracao
        // ================================================================

        private void ExigirConfigurado()
        {
            if (!_settings.Configurado)
                throw new NapistaException("A integração com o NaPista ainda está em liberação (falta o client_id do Gericar).");
        }

        private async Task<ContaSalva> ContaObrigatoriaAsync()
        {
            ExigirConfigurado();
            if (await TokenAsync() == null) throw new NapistaException("Conecte o NaPista em Integrações.");
            var conta = await ContaAsync();
            if (conta?.SellerId == null) throw new NapistaException("Escolha a loja do NaPista em Integrações → NaPista.");
            return conta;
        }

        private static string Seller(ContaSalva conta) =>
            conta.SellerId ?? throw new NapistaException("Escolha a loja do NaPista em Integrações → NaPista.");

        private async Task<TokenSalvo?> TokenAsync()
        {
            var cifrado = await _config.GetValorAsync(ChaveToken);
            if (string.IsNullOrWhiteSpace(cifrado)) return null;
            try { return JsonSerializer.Deserialize<TokenSalvo>(_protector.Unprotect(cifrado), Json); }
            catch (Exception ex) when (ex is JsonException or System.Security.Cryptography.CryptographicException or FormatException)
            {
                _logger.LogWarning(ex, "NaPista: token guardado ilegivel no tenant {Tenant}", _tenant.TenantSlug);
                return null;
            }
        }

        private Task SalvarTokenAsync(TokenSalvo t) =>
            _config.SetValorAsync(ChaveToken, _protector.Protect(JsonSerializer.Serialize(t, Json)));

        private async Task<ContaSalva?> ContaAsync()
        {
            var json = await _config.GetValorAsync(ChaveConta);
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonSerializer.Deserialize<ContaSalva>(json, Json); }
            catch (JsonException) { return null; }
        }

        private Task SalvarContaAsync(ContaSalva c) => _config.SetValorAsync(ChaveConta, JsonSerializer.Serialize(c, Json));

        // ================================================================
        // Auxiliares
        // ================================================================

        private async Task<T> EmCacheAsync<T>(string chave, Func<Task<T>> carregar) where T : class
        {
            chave = _settings.ApiUrl + "|" + chave;
            if (Catalogo.TryGetValue(chave, out var c) && c.Expira > DateTime.UtcNow && c.Valor is T v) return v;
            var valor = await carregar();
            Catalogo[chave] = (valor, DateTime.UtcNow.Add(DuracaoCatalogo));
            return valor;
        }

        private static string Categoria(string? categoria)
        {
            var c = (categoria ?? "CAR").Trim().ToUpperInvariant();
            return Categorias.Contains(c) ? c : throw new NapistaException("Categoria inválida (CAR, MOTORCYCLE ou TRUCK).");
        }

        private static IReadOnlyList<NapistaItem> Itens(JsonDocument doc) => Itens(doc.RootElement);

        private static List<NapistaItem> Itens(JsonElement r)
        {
            var lista = r.ValueKind == JsonValueKind.Array ? r
                : r.ValueKind == JsonValueKind.Object && r.TryGetProperty("items", out var i) && i.ValueKind == JsonValueKind.Array ? i
                : default;
            if (lista.ValueKind != JsonValueKind.Array) return new();
            return lista.EnumerateArray()
                .Select(x => new NapistaItem { Id = Texto(x, "id") ?? "", Nome = (Texto(x, "name") ?? Texto(x, "id") ?? "").Trim() })
                .Where(x => x.Id != "")
                .OrderBy(x => x.Nome, StringComparer.Create(new CultureInfo("pt-BR"), true))
                .ToList();
        }

        private static NapistaAnuncioDados Dados(VeiculoNapista a) =>
            JsonSerializer.Deserialize<NapistaAnuncioDados>(a.VnpDadosJson, Json) ?? new();

        private static NapistaVeiculoInfo Info(VeiculoNapista? a, NapistaAnuncioDados dados, bool sugestao) => new()
        {
            Publicado = a?.EstaPublicado == true,
            AnuncioId = string.IsNullOrEmpty(a?.VnpAnuncioId) ? null : a.VnpAnuncioId,
            Status = a?.VnpStatus ?? "",
            Erro = a?.VnpErro,
            PublicadoEm = a?.VnpPublicadoEm,
            AtualizadoEm = a?.VnpAtualizadoEm,
            Fotos = a?.VnpFotos ?? 0,
            Dados = dados,
            Sugestao = sugestao
        };

        private async Task<string> UrlPublicaAsync(int lojaId)
        {
            // Mesma regra do Mercado Livre e da Webmotors: o NaPista baixa a foto de fora.
            var loja = await _lojas.GetByIdAsync(lojaId);
            var url = loja?.LojUrlCatalogo?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                url = (Environment.GetEnvironmentVariable("PUBLIC_SITE_URL") ?? "https://connectveiculos.dev.br").TrimEnd('/');
            return url;
        }

        private static string? Origem(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var u) ? $"{u.Scheme}://{u.Authority}" : null;

        public static string Placa(string? placa) =>
            new string((placa ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        private static string SoDigitos(string? v) => new((v ?? "").Where(char.IsDigit).ToArray());

        private static string Limitar(string v, int max) => v.Length > max ? v[..max] : v;

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

        private static decimal? Decimal(JsonElement e, string nome)
            => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nome, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDecimal() : null;
    }
}
