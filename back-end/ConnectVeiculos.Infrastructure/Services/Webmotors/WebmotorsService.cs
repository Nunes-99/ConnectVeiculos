using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ConnectVeiculos.Core.Entities.Integracoes;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Integracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Core.Webmotors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConnectVeiculos.Infrastructure.Services.Webmotors
{
    /// <inheritdoc cref="IWebmotorsService"/>
    public class WebmotorsService : IWebmotorsService
    {
        public const string ChaveConta = "WEBMOTORS_CONTA";

        /// <summary>A sessao dura 1000 minutos (manual); renova antes.</summary>
        private static readonly TimeSpan DuracaoSessao = TimeSpan.FromHours(15);
        private static readonly TimeSpan DuracaoTabelas = TimeSpan.FromHours(12);
        private const int MaximoFotos = 20;

        /// <summary>
        /// Anuncio recem-incluido recusa foto por alguns segundos (codigo 42|4,
        /// visto na homologacao: 5s depois entra). Espera e tenta de novo.
        /// Campo para os testes nao esperarem de verdade.
        /// </summary>
        public static TimeSpan[] EsperasFotoNova = { TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(8) };
        private const string AnuncioAindaProcessando = "42|4";

        // Sessao por loja (tenant + usuario). Estatico: o servico e' por requisicao.
        private static readonly ConcurrentDictionary<string, (string Hash, DateTime Expira)> Sessoes = new();
        // Tabelas sao iguais para todas as lojas do mesmo ambiente.
        private static readonly ConcurrentDictionary<string, (object Valor, DateTime Expira)> Tabelas = new();

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _http;
        private readonly WebmotorsSettings _settings;
        private readonly IConfiguracaoSistemaRepository _config;
        private readonly ITokenProtector _protector;
        private readonly ITenantContext _tenant;
        private readonly IVeiculoRepository _veiculos;
        private readonly IVeiculoImagemRepository _imagens;
        private readonly ILojaRepository _lojas;
        private readonly IVeiculoWebmotorsRepository _anuncios;
        private readonly ILogger<WebmotorsService> _logger;

        public WebmotorsService(HttpClient http, IOptions<WebmotorsSettings> settings, IConfiguracaoSistemaRepository config,
            ITokenProtector protector, ITenantContext tenant, IVeiculoRepository veiculos, IVeiculoImagemRepository imagens,
            ILojaRepository lojas, IVeiculoWebmotorsRepository anuncios, ILogger<WebmotorsService> logger)
        {
            _http = http;
            _settings = settings.Value;
            _config = config;
            _protector = protector;
            _tenant = tenant;
            _veiculos = veiculos;
            _imagens = imagens;
            _lojas = lojas;
            _anuncios = anuncios;
            _logger = logger;
        }

        private sealed class Conta
        {
            public string Cnpj { get; set; } = "";
            public string Email { get; set; } = "";
            public string Senha { get; set; } = "";
            public DateTime ConectadoEm { get; set; }
            public long? ModalidadeId { get; set; }
        }

        // ---------------- Conta ----------------

        public async Task<WebmotorsStatus> StatusAsync()
        {
            var conta = await LerContaAsync();
            var status = new WebmotorsStatus { Homologacao = _settings.Homologacao };
            if (conta == null) return status;

            status.Conectado = true;
            status.Cnpj = conta.Cnpj;
            status.Email = conta.Email;
            status.ConectadoEm = conta.ConectadoEm;
            status.ModalidadeId = conta.ModalidadeId;
            try
            {
                status.Modalidades = await ModalidadesAsync(conta);
            }
            catch (WebmotorsException ex)
            {
                status.Erro = ex.Message;
            }
            return status;
        }

        public async Task<WebmotorsStatus> ConectarAsync(WebmotorsConectarInput input)
        {
            var cnpj = new string((input.Cnpj ?? "").Where(char.IsDigit).ToArray());
            var email = (input.Email ?? "").Trim();
            if (cnpj.Length != 14) throw new WebmotorsException("Informe o CNPJ da loja com 14 dígitos.");
            if (!email.Contains('@')) throw new WebmotorsException("Informe o e-mail do usuário de integração da Webmotors.");
            if (string.IsNullOrEmpty(input.Senha)) throw new WebmotorsException("Informe a senha do usuário de integração.");

            var conta = new Conta { Cnpj = cnpj, Email = email, Senha = input.Senha, ConectadoEm = DateTime.UtcNow };
            Sessoes.TryRemove(ChaveSessao(conta), out _);
            await LoginAsync(conta);

            var modalidades = await ModalidadesAsync(conta);
            conta.ModalidadeId = modalidades.FirstOrDefault(m => m.TipoAnuncio == "U")?.Codigo ?? modalidades.FirstOrDefault()?.Codigo;
            await GravarContaAsync(conta);
            return await StatusAsync();
        }

        public async Task<WebmotorsStatus> EscolherModalidadeAsync(long modalidadeId)
        {
            var conta = await ContaObrigatoriaAsync();
            if ((await ModalidadesAsync(conta)).All(m => m.Codigo != modalidadeId))
                throw new WebmotorsException("Modalidade não encontrada no plano da loja na Webmotors.");
            conta.ModalidadeId = modalidadeId;
            await GravarContaAsync(conta);
            return await StatusAsync();
        }

        public async Task DesconectarAsync()
        {
            var conta = await LerContaAsync();
            if (conta != null) Sessoes.TryRemove(ChaveSessao(conta), out _);
            await _config.SetValorAsync(ChaveConta, "");
        }

        // ---------------- Tabelas ----------------

        public async Task<IReadOnlyList<WebmotorsItem>> TabelaAsync(string tabela)
        {
            var (operacao, tipo, codigo, nome) = (tabela ?? "").ToLowerInvariant() switch
            {
                "marcas" => ("ObterMarca", "MarcaWM", "CodigoMarca", "NomeMarca"),
                "cores" => ("ObterCores", "CorWM", "CodigoCor", "Descricao"),
                "cambios" => ("ObterCambio", "TipoCambioWM", "CodigoCambio", "Descricao"),
                "combustiveis" => ("ObterCombustivel", "CombustivelWM", "CodigoCombustivel", "Descricao"),
                "portas" => ("ObterNumeroPortas", "NumeroPortasWM", "CodigoPortas", "Descricao"),
                "opcionais" => ("ObterOpcionais", "OpcionalWM", "CodigoOpcional", "Descricao"),
                _ => throw new WebmotorsException("Tabela desconhecida.")
            };
            var conta = await ContaObrigatoriaAsync();
            return await EmCacheAsync(operacao, () => ListaAsync(conta, operacao, tipo, codigo, nome));
        }

        public async Task<IReadOnlyList<WebmotorsItem>> ModelosAsync(long marcaId)
        {
            var conta = await ContaObrigatoriaAsync();
            return await EmCacheAsync($"ObterModelo:{marcaId}",
                () => ListaAsync(conta, "ObterModelo", "ModeloWM", "CodigoModelo", "NomeModelo", ("pCodigoMarca", marcaId)));
        }

        public async Task<IReadOnlyList<WebmotorsVersao>> VersoesAsync(long modeloId, int? ano)
        {
            var conta = await ContaObrigatoriaAsync();
            var todas = await EmCacheAsync<IReadOnlyList<WebmotorsVersao>>($"ObterVersao:{modeloId}", async () =>
            {
                var r = await ChamarAsync(conta, "ObterVersao",
                    ("pCodigoModelo", modeloId.ToString(CultureInfo.InvariantCulture)),
                    // Intervalo de "atualizacao" largo = todas as versoes do modelo.
                    ("pDataInicioAtualizacao", new DateTime(1900, 1, 1)),
                    ("pDataFimAtualizacao", DateTime.Today.AddYears(2)));
                return WebmotorsSoap.Itens(r, "Versao")
                    .Where(v => WebmotorsSoap.Retorno(v) == WebmotorsSoap.Sucesso)
                    .Select(v => new WebmotorsVersao
                    {
                        Codigo = WebmotorsSoap.Numero(v, "CodigoVersao"),
                        Nome = WebmotorsSoap.Campo(v, "NomeVersao") ?? "",
                        Anos = WebmotorsSoap.Itens(v, "AnoModeloWM").Select(a => (int)WebmotorsSoap.Numero(a, "AnoModelo")).Where(a => a > 0).OrderBy(a => a).ToList()
                    })
                    .Where(v => v.Codigo > 0)
                    .OrderBy(v => v.Nome)
                    .ToList();
            });
            return ano is > 0 ? todas.Where(v => v.Anos.Count == 0 || v.Anos.Contains(ano.Value)).ToList() : todas;
        }

        // ---------------- Anuncio do veiculo ----------------

        public async Task<WebmotorsVeiculoInfo> ObterVeiculoAsync(int veiculoId)
        {
            var veiculo = await _veiculos.GetByIdAsync(veiculoId) ?? throw new WebmotorsException("Veículo não encontrado.");
            var anuncio = await _anuncios.GetByVeiculoIdAsync(veiculoId);
            if (anuncio?.VwmDadosJson is { Length: > 0 } json)
                return Info(anuncio, JsonSerializer.Deserialize<WebmotorsAnuncioDados>(json, Json) ?? new(), false);

            await ContaObrigatoriaAsync();
            return Info(anuncio, await SugerirAsync(veiculo), true);
        }

        public async Task<WebmotorsVeiculoInfo> PublicarAsync(int veiculoId, WebmotorsAnuncioDados dados)
        {
            var conta = await ContaObrigatoriaAsync();
            var veiculo = await _veiculos.GetByIdAsync(veiculoId) ?? throw new WebmotorsException("Veículo não encontrado.");
            if (veiculo.VeiSts != "D") throw new WebmotorsException("Só veículo disponível pode ser anunciado.");
            Validar(veiculo, dados);

            var anuncio = await _anuncios.GetByVeiculoIdAsync(veiculoId) ?? new VeiculoWebmotors(veiculoId, "");
            anuncio.SalvarDados(JsonSerializer.Serialize(dados, Json));
            await EnviarAsync(conta, veiculo, anuncio, dados);
            return Info(anuncio, dados, false);
        }

        public async Task<WebmotorsVeiculoInfo> RemoverAsync(int veiculoId)
        {
            var conta = await ContaObrigatoriaAsync();
            var anuncio = await _anuncios.GetByVeiculoIdAsync(veiculoId);
            if (anuncio == null || !anuncio.EstaPublicado) throw new WebmotorsException("O veículo não está anunciado na Webmotors.");
            await ExcluirAsync(conta, anuncio, WebmotorsSoap.MotivoOutros, peloUsuario: true);
            return Info(anuncio, Dados(anuncio), false);
        }

        public async Task SincronizarVeiculoAsync(int veiculoId)
        {
            var anuncio = await _anuncios.GetByVeiculoIdAsync(veiculoId);
            if (anuncio == null || string.IsNullOrEmpty(anuncio.VwmDadosJson) || anuncio.Pausado) return;
            var conta = await LerContaAsync();
            if (conta == null) return;

            var veiculo = await _veiculos.GetByIdAsync(veiculoId);
            try
            {
                if (veiculo == null || veiculo.VeiSts != "D")
                {
                    if (anuncio.EstaPublicado)
                        await ExcluirAsync(conta, anuncio, veiculo?.VeiSts == "V" ? WebmotorsSoap.MotivoVendiNaLoja : WebmotorsSoap.MotivoOutros);
                    return;
                }
                await EnviarAsync(conta, veiculo, anuncio, Dados(anuncio));
            }
            catch (WebmotorsException ex)
            {
                // Rodando em segundo plano: o erro fica no anuncio para a tela mostrar.
                _logger.LogWarning("Webmotors: veiculo {VeiculoId} nao sincronizou: {Erro}", veiculoId, ex.Message);
            }
        }

        private async Task EnviarAsync(Conta conta, Veiculo veiculo, VeiculoWebmotors anuncio, WebmotorsAnuncioDados dados)
        {
            var modalidade = conta.ModalidadeId ?? throw new WebmotorsException("Escolha a modalidade de anúncio em Integrações → Webmotors.");
            var alterar = anuncio.EstaPublicado;
            var operacao = alterar ? "AlterarCarro" : "IncluirCarro";
            var corpo = WebmotorsSoap.Anuncio(alterar ? anuncio.VwmCodigoAnuncio!.Value : 0, modalidade, dados,
                veiculo.VeiKm, Placa(veiculo.VeiPlaca), veiculo.VeiPreco, veiculo.VeiObservacao);

            try
            {
                var r = await ChamarAsync(conta, operacao, corpo);
                var retorno = WebmotorsSoap.Campo(r, "CodigoRetorno") ?? "";
                var codigo = WebmotorsSoap.Numero(r, "CodigoAnuncio");
                if (retorno != WebmotorsSoap.Sucesso || (codigo <= 0 && !alterar))
                    throw new WebmotorsException(WebmotorsSoap.MensagemDoRetorno(retorno, alterar ? "a alteração do anúncio" : "o anúncio"));

                anuncio.Publicado(codigo > 0 ? codigo : anuncio.VwmCodigoAnuncio!.Value);
                await _anuncios.SalvarAsync(anuncio);
            }
            catch (WebmotorsException ex)
            {
                anuncio.Falhou(ex.Message);
                await _anuncios.SalvarAsync(anuncio);
                throw;
            }

            try
            {
                await EnviarFotosAsync(conta, veiculo, anuncio);
            }
            catch (WebmotorsException ex)
            {
                // O anuncio esta no ar; foto pendente nao desfaz isso.
                anuncio.Falhou("Anúncio publicado, mas as fotos não foram: " + ex.Message);
                await _anuncios.SalvarAsync(anuncio);
            }
        }

        /// <summary>
        /// Manda as fotos (o arquivo, em base64). So troca quando a lista mudou:
        /// apaga as anteriores e envia de novo, na ordem do cadastro.
        /// IncluirFotoUrl existe, mas a Webmotors recusa (codigo 21|10) ate URL
        /// publica de .jpg, por isso o servidor baixa a foto e envia o arquivo.
        /// </summary>
        private async Task EnviarFotosAsync(Conta conta, Veiculo veiculo, VeiculoWebmotors anuncio)
        {
            var fotos = (await _imagens.GetByVeiculoIdAsync(veiculo.VeiId))
                .Where(i => i.ImgSts).OrderBy(i => i.ImgOrdem).Take(MaximoFotos).ToList();
            var assinatura = string.Join("|", fotos.Select(f => f.ImgCaminho));
            if (assinatura == (anuncio.VwmFotosAssinatura ?? "")) return;

            var codigoAnuncio = anuncio.VwmCodigoAnuncio!.Value;
            var anteriores = string.IsNullOrEmpty(anuncio.VwmFotosJson)
                ? new List<long>() : JsonSerializer.Deserialize<List<long>>(anuncio.VwmFotosJson) ?? new();
            foreach (var foto in anteriores)
                await ChamarAsync(conta, "ExcluirFoto", ("pCodigoFoto", foto), ("pCodigoAnuncio", codigoAnuncio));

            var urlBase = await UrlPublicaAsync(veiculo.R_LojId);
            var enviadas = new List<long>();
            try
            {
                foreach (var f in fotos)
                {
                    // O endpoint publico ja converte (HEIC/WebP/PNG) para JPEG de ate 1200px.
                    var url = $"{urlBase}/api/imagens/file?path={Uri.EscapeDataString(f.ImgCaminho)}&max=1200&format=jpeg";
                    var bytes = await BaixarFotoAsync(url, enviadas.Count + 1);
                    XElement r;
                    string retorno;
                    for (var espera = 0; ; espera++)
                    {
                        r = await ChamarAsync(conta, "IncluirFoto", ("pByteImage", bytes), ("pCodigoAnuncio", codigoAnuncio));
                        retorno = WebmotorsSoap.Campo(r, "CodigoRetorno") ?? "";
                        if (retorno != AnuncioAindaProcessando || espera >= EsperasFotoNova.Length) break;
                        await Task.Delay(EsperasFotoNova[espera]);
                    }
                    if (retorno != WebmotorsSoap.Sucesso)
                        throw new WebmotorsException(WebmotorsSoap.MensagemDoRetorno(retorno, $"a foto {enviadas.Count + 1}"));
                    enviadas.Add(WebmotorsSoap.Numero(r, "CodigoFoto"));
                }
            }
            finally
            {
                // Guarda o que entrou mesmo se parou no meio: a proxima vez apaga certo.
                anuncio.FotosEnviadas(JsonSerializer.Serialize(enviadas),
                    enviadas.Count == fotos.Count ? assinatura : "");
                await _anuncios.SalvarAsync(anuncio);
            }
        }

        private async Task<byte[]> BaixarFotoAsync(string url, int numero)
        {
            try
            {
                using var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) throw new WebmotorsException($"não consegui ler a foto {numero} do Gericar (HTTP {(int)resp.StatusCode}).");
                return await resp.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new WebmotorsException($"não consegui ler a foto {numero} do Gericar.");
            }
        }

        private async Task ExcluirAsync(Conta conta, VeiculoWebmotors anuncio, int motivo, bool peloUsuario = false)
        {
            var r = await ChamarAsync(conta, "ExcluirCarro",
                ("pCodigoAnuncio", anuncio.VwmCodigoAnuncio!.Value), ("pMotivoExclusao", motivo.ToString(CultureInfo.InvariantCulture)));
            var retorno = WebmotorsSoap.Campo(r, "CodigoRetorno") ?? "";
            if (retorno != WebmotorsSoap.Sucesso)
                throw new WebmotorsException(WebmotorsSoap.MensagemDoRetorno(retorno, "a exclusão do anúncio"));
            anuncio.Removido(peloUsuario);
            await _anuncios.SalvarAsync(anuncio);
        }

        private static void Validar(Veiculo veiculo, WebmotorsAnuncioDados d)
        {
            var faltando = new List<string>();
            if (d.MarcaId is not > 0) faltando.Add("marca");
            if (d.ModeloId is not > 0) faltando.Add("modelo");
            if (d.VersaoId is not > 0) faltando.Add("versão");
            if (d.AnoModelo is not > 1900) faltando.Add("ano do modelo");
            if (d.AnoFabricacao is not > 1900) faltando.Add("ano de fabricação");
            if (d.CambioId is not > 0) faltando.Add("câmbio");
            if (d.CombustivelId is not > 0) faltando.Add("combustível");
            if (d.CorId is not > 0) faltando.Add("cor");
            if (d.Portas is null or < 0) faltando.Add("portas");
            if (faltando.Count > 0) throw new WebmotorsException("Preencha: " + string.Join(", ", faltando) + ".");
            if (d.AnoFabricacao > d.AnoModelo || d.AnoModelo - d.AnoFabricacao > 1)
                throw new WebmotorsException("O ano de fabricação tem de ser o do modelo ou um a menos.");
            if (string.IsNullOrWhiteSpace(Placa(veiculo.VeiPlaca))) throw new WebmotorsException("Cadastre a placa do veículo: a Webmotors exige para usados.");
            if (veiculo.VeiPreco <= 0) throw new WebmotorsException("Cadastre o preço do veículo.");
        }

        private static string Placa(string? placa) =>
            new string((placa ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        private async Task<WebmotorsAnuncioDados> SugerirAsync(Veiculo v)
        {
            var d = new WebmotorsAnuncioDados { AnoModelo = v.VeiAno, AnoFabricacao = v.VeiAno };
            try
            {
                var marca = WebmotorsSugestao.Marca(v.VeiMarca, await TabelaAsync("marcas"));
                d.MarcaId = marca?.Codigo;
                WebmotorsVersao? versao = null;
                if (marca != null)
                {
                    var modelo = WebmotorsSugestao.Modelo(v.VeiModelo, await ModelosAsync(marca.Codigo));
                    d.ModeloId = modelo?.Codigo;
                    if (modelo != null)
                    {
                        versao = WebmotorsSugestao.Versao(v.VeiModelo, modelo, v.VeiAno, await VersoesAsync(modelo.Codigo, null));
                        d.VersaoId = versao?.Codigo;
                    }
                }
                // A versao da Webmotors traz combustivel/portas/cambio por extenso;
                // sem ela, o modelo da FIPE ("Flex", "4p", "Aut.").
                var texto = (versao?.Nome ?? "") + " " + v.VeiModelo;
                d.CombustivelId = WebmotorsSugestao.Combustivel(texto, await TabelaAsync("combustiveis"))?.Codigo;
                d.CambioId = WebmotorsSugestao.Cambio(texto, await TabelaAsync("cambios"))?.Codigo;
                d.Portas = WebmotorsSugestao.Portas(texto);
                d.CorId = WebmotorsSugestao.Cor(v.VeiCor, await TabelaAsync("cores"))?.Codigo;
                d.Opcionais = WebmotorsSugestao.Opcionais(v.VeiOpcionais, await TabelaAsync("opcionais"));
            }
            catch (WebmotorsException ex)
            {
                _logger.LogInformation("Webmotors: sugestao incompleta para o veiculo {VeiculoId}: {Erro}", v.VeiId, ex.Message);
            }
            return d;
        }

        private static WebmotorsAnuncioDados Dados(VeiculoWebmotors a) =>
            JsonSerializer.Deserialize<WebmotorsAnuncioDados>(a.VwmDadosJson, Json) ?? new();

        private static WebmotorsVeiculoInfo Info(VeiculoWebmotors? a, WebmotorsAnuncioDados dados, bool sugestao) => new()
        {
            Publicado = a?.EstaPublicado == true,
            CodigoAnuncio = a?.VwmCodigoAnuncio,
            Status = a?.VwmStatus ?? "",
            Erro = a?.VwmErro,
            PublicadoEm = a?.VwmPublicadoEm,
            AtualizadoEm = a?.VwmAtualizadoEm,
            Fotos = string.IsNullOrEmpty(a?.VwmFotosJson) ? 0 : JsonSerializer.Deserialize<List<long>>(a.VwmFotosJson)?.Count ?? 0,
            Dados = dados,
            Sugestao = sugestao
        };

        private async Task<string> UrlPublicaAsync(int lojaId)
        {
            // Mesma regra do Mercado Livre: a Webmotors baixa a foto de fora.
            var loja = await _lojas.GetByIdAsync(lojaId);
            var url = loja?.LojUrlCatalogo?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                url = (Environment.GetEnvironmentVariable("PUBLIC_SITE_URL") ?? "https://connectveiculos.dev.br").TrimEnd('/');
            return url;
        }

        // ---------------- SOAP ----------------

        private async Task<List<WebmotorsModalidade>> ModalidadesAsync(Conta conta)
        {
            var r = await ChamarAsync(conta, "ObterModalidade");
            return WebmotorsSoap.Itens(r, "ModalidadeWM")
                .Where(m => WebmotorsSoap.Retorno(m) == WebmotorsSoap.Sucesso)
                .Select(m => new WebmotorsModalidade
                {
                    Codigo = WebmotorsSoap.Numero(m, "CodigoModalidade"),
                    Nome = WebmotorsSoap.Campo(m, "Descricao") ?? "",
                    TipoAnuncio = WebmotorsSoap.Campo(m, "TipoAnuncio") ?? "",
                    Limite = (int)WebmotorsSoap.Numero(m, "QuantidadeAnunciosTotal"),
                    EmUso = (int)WebmotorsSoap.Numero(m, "QuantidadeAnuncios")
                })
                .Where(m => m.Codigo > 0)
                .ToList();
        }

        private async Task<IReadOnlyList<WebmotorsItem>> ListaAsync(Conta conta, string operacao, string tipo, string codigo, string nome,
            params (string, object?)[] campos)
        {
            var r = await ChamarAsync(conta, operacao, campos);
            return WebmotorsSoap.Itens(r, tipo)
                .Where(i => WebmotorsSoap.Retorno(i) == WebmotorsSoap.Sucesso)
                .Select(i => new WebmotorsItem { Codigo = WebmotorsSoap.Numero(i, codigo), Nome = (WebmotorsSoap.Campo(i, nome) ?? "").Trim() })
                .Where(i => i.Nome != "")
                .OrderBy(i => i.Nome, StringComparer.Create(new CultureInfo("pt-BR"), true))
                .ToList();
        }

        private async Task<T> EmCacheAsync<T>(string chave, Func<Task<T>> carregar) where T : class
        {
            chave = _settings.BaseUrl + "|" + chave;
            if (Tabelas.TryGetValue(chave, out var c) && c.Expira > DateTime.UtcNow && c.Valor is T v) return v;
            var valor = await carregar();
            Tabelas[chave] = (valor, DateTime.UtcNow.Add(DuracaoTabelas));
            return valor;
        }

        private Task<XElement> ChamarAsync(Conta conta, string operacao, params (string Nome, object? Valor)[] campos) =>
            ChamarAsync(conta, operacao, WebmotorsSoap.Campos(campos));

        /// <summary>Chama o servico de estoque com a sessao da loja; renova uma vez se vencida.</summary>
        private async Task<XElement> ChamarAsync(Conta conta, string operacao, string corpo)
        {
            for (var tentativa = 0; ; tentativa++)
            {
                var hash = await LoginAsync(conta);
                var xml = WebmotorsSoap.Envelope(WebmotorsSoap.NsEstoque, operacao,
                    $"<pHashAutenticacao>{System.Security.SecurityElement.Escape(hash)}</pHashAutenticacao>{corpo}");
                var resposta = await PostarAsync(WebmotorsSoap.ServicoEstoque, WebmotorsSoap.NsEstoque, operacao, xml);
                var resultado = WebmotorsSoap.Resultado(resposta, operacao);
                if (WebmotorsSoap.Retorno(resultado) == WebmotorsSoap.SessaoInvalida && tentativa == 0)
                {
                    Sessoes.TryRemove(ChaveSessao(conta), out _);
                    continue;
                }
                return resultado;
            }
        }

        private async Task<string> LoginAsync(Conta conta)
        {
            var chave = ChaveSessao(conta);
            if (Sessoes.TryGetValue(chave, out var s) && s.Expira > DateTime.UtcNow) return s.Hash;

            var xml = WebmotorsSoap.Envelope(WebmotorsSoap.NsLogin, "autenticar",
                ("cnpj", conta.Cnpj), ("email", conta.Email), ("senha", conta.Senha));
            var r = WebmotorsSoap.Resultado(await PostarAsync(WebmotorsSoap.ServicoLogin, WebmotorsSoap.NsLogin, "autenticar", xml), "autenticar");
            var retorno = WebmotorsSoap.Campo(r, "CodigoRetorno") ?? "";
            var hash = WebmotorsSoap.Campo(r, "HashAutenticacao") ?? "";
            if (retorno != WebmotorsSoap.Sucesso || hash.Length < 8)
                throw new WebmotorsException(WebmotorsSoap.MensagemDoRetorno(retorno == "" ? WebmotorsSoap.LoginRecusado : retorno, "o login"));

            Sessoes[chave] = (hash, DateTime.UtcNow.Add(DuracaoSessao));
            return hash;
        }

        private async Task<string> PostarAsync(string servico, string ns, string operacao, string xml)
        {
            var url = _settings.BaseUrl.TrimEnd('/') + "/" + servico;
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(xml, Encoding.UTF8, "text/xml")
            };
            req.Headers.TryAddWithoutValidation("SOAPAction", $"\"{ns}/{operacao}\"");
            if (!string.IsNullOrWhiteSpace(_settings.Token))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.Token);

            HttpResponseMessage resp;
            try
            {
                resp = await _http.SendAsync(req);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new WebmotorsException("Não foi possível falar com a Webmotors agora. Tente de novo em alguns minutos.");
            }
            using (resp)
            {
                var corpo = await resp.Content.ReadAsStringAsync();
                if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    throw new WebmotorsException("A Webmotors bloqueou o acesso: o IP do servidor não está liberado neste ambiente.");
                // SOAP Fault vem com 500 e corpo XML; Resultado() le a mensagem.
                if (!resp.IsSuccessStatusCode && !corpo.Contains("Fault", StringComparison.Ordinal))
                    throw new WebmotorsException($"A Webmotors respondeu HTTP {(int)resp.StatusCode}. Tente de novo em alguns minutos.");
                return corpo;
            }
        }

        private string ChaveSessao(Conta conta) =>
            $"{_settings.BaseUrl}|{_tenant.TenantSlug}|{conta.Cnpj}|{conta.Email}";

        // ---------------- Configuracao ----------------

        private async Task<Conta> ContaObrigatoriaAsync() =>
            await LerContaAsync() ?? throw new WebmotorsException("Conecte a conta da Webmotors em Integrações.");

        private async Task<Conta?> LerContaAsync()
        {
            var json = await _config.GetValorAsync(ChaveConta);
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var conta = JsonSerializer.Deserialize<Conta>(json, Json);
                if (conta == null || string.IsNullOrEmpty(conta.Senha)) return null;
                conta.Senha = _protector.Unprotect(conta.Senha);
                return conta;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Webmotors: configuracao ilegivel no tenant {Tenant}", _tenant.TenantSlug);
                return null;
            }
        }

        private async Task GravarContaAsync(Conta conta)
        {
            var gravar = new Conta
            {
                Cnpj = conta.Cnpj,
                Email = conta.Email,
                Senha = _protector.Protect(conta.Senha),
                ConectadoEm = conta.ConectadoEm,
                ModalidadeId = conta.ModalidadeId
            };
            await _config.SetValorAsync(ChaveConta, JsonSerializer.Serialize(gravar, Json));
        }
    }
}
