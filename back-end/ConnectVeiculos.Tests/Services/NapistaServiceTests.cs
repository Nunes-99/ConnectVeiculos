using System.Net;
using System.Text;
using System.Text.Json;
using ConnectVeiculos.Core.Entities.Integracoes;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Entities.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Integracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Core.Napista;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using ConnectVeiculos.Infrastructure.Services.Napista;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.Services
{
    /// <summary>
    /// NaPista com servidor falso (Keycloak + API de estoque), no formato da
    /// documentacao em developers.napista.com.br.
    /// </summary>
    public class NapistaServiceTests : IDisposable
    {
        private const string Auth = "https://auth.development.napista.com.br/realms/marketplace-dealer/protocol/openid-connect";
        private const string Api = "https://api.development.napista.com.br/seller-inventory-api";
        private const string Seller = "d94918bd-fbeb-40c7-b0ac-ffb8208e329a";

        private sealed class NapistaFalso : HttpMessageHandler
        {
            public readonly List<(string Metodo, string Url, string? Corpo, string? Auth)> Chamadas = new();
            /// <summary>Resposta pelo inicio de "METODO url"; a mais longa que casar vence.</summary>
            public readonly Dictionary<string, Queue<(HttpStatusCode, string)>> Rotas = new();

            public void Responder(string rota, HttpStatusCode status, string corpo)
            {
                if (!Rotas.TryGetValue(rota, out var fila)) Rotas[rota] = fila = new();
                fila.Enqueue((status, corpo));
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                var chave = request.Method.Method + " " + request.RequestUri;
                var corpo = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
                Chamadas.Add((request.Method.Method, request.RequestUri!.ToString(), corpo, request.Headers.Authorization?.Parameter));
                var rota = Rotas.Keys.Where(k => chave.StartsWith(k, StringComparison.Ordinal)).OrderByDescending(k => k.Length).FirstOrDefault();
                if (rota == null) return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"message\":\"rota nao simulada\"}") };
                var fila = Rotas[rota];
                var (status, resposta) = fila.Count > 1 ? fila.Dequeue() : fila.Peek();
                return new HttpResponseMessage(status) { Content = new StringContent(resposta, Encoding.UTF8, "application/json") };
            }
        }

        private sealed class ConfigEmMemoria : IConfiguracaoSistemaRepository
        {
            public readonly Dictionary<string, string> Valores = new();
            public Task<string> GetValorAsync(string chave) => Task.FromResult(Valores.TryGetValue(chave, out var v) ? v : null!);
            public Task SetValorAsync(string chave, string valor) { Valores[chave] = valor; return Task.CompletedTask; }
        }

        private sealed class AnunciosEmMemoria : IVeiculoNapistaRepository
        {
            public readonly Dictionary<int, VeiculoNapista> Itens = new();
            public Task<VeiculoNapista?> GetByVeiculoIdAsync(int veiculoId) => Task.FromResult(Itens.TryGetValue(veiculoId, out var a) ? a : null);
            public Task SalvarAsync(VeiculoNapista anuncio) { Itens[anuncio.R_VeiId] = anuncio; return Task.CompletedTask; }
        }

        private sealed class StateFalso : IOAuthStateProtector
        {
            public string Proteger(string tenantSlug) => "st-" + tenantSlug;
            public OAuthStatePayload Validar(string? state, string tenantSlugAtual) =>
                state == "st-" + tenantSlugAtual ? new(tenantSlugAtual, "n", DateTime.UtcNow.AddMinutes(10)) : throw new OAuthStateException("Link de conexão inválido ou vencido.");
            public OAuthStatePayload Decifrar(string? state) => new((state ?? "")[3..], "n", DateTime.UtcNow.AddMinutes(10));
        }

        private readonly NapistaFalso _napista = new();
        private readonly ConfigEmMemoria _config = new();
        private readonly AnunciosEmMemoria _anuncios = new();
        private readonly List<Veiculo> _veiculos = new();
        private readonly List<VeiculoImagem> _fotos = new();
        private readonly SqliteConnection _conexao;
        private readonly ConnectVeiculosDbContext _db;
        private readonly NapistaService _service;

        public NapistaServiceTests()
        {
            _veiculos.Add(new Veiculo(7, 1, 1, "VW - VolksWagen", "GOL 1.0 Mi Total Flex 8V 4p", 2020, "ABC-1D23", "9BWZZZ377VT004251",
                "Prata", 45000, 52990.50m, DateTime.Now, "D", "A", 40000m, veiObservacao: "Único dono", veiRenavam: "123456789"));
            _fotos.Add(new VeiculoImagem(1, 7, "uploads/veiculos/7/frente.jpg", 0, true));
            _fotos.Add(new VeiculoImagem(2, 7, "uploads/veiculos/7/lado.jpg", 1, true));

            var veiculos = new Mock<IVeiculoRepository>();
            veiculos.Setup(v => v.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((int id) => _veiculos.FirstOrDefault(x => x.VeiId == id)!);
            veiculos.Setup(v => v.GetAllAsync()).ReturnsAsync(() => _veiculos.ToList());
            var imagens = new Mock<IVeiculoImagemRepository>();
            imagens.Setup(i => i.GetByVeiculoIdAsync(It.IsAny<int>())).ReturnsAsync(() => _fotos.ToList());
            var lojas = new Mock<ILojaRepository>();
            lojas.Setup(l => l.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Loja)null!);
            lojas.Setup(l => l.GetAllAsync()).ReturnsAsync(Array.Empty<Loja>());
            var protector = new Mock<ITokenProtector>();
            protector.Setup(p => p.Protect(It.IsAny<string>())).Returns((string s) => "x" + s);
            protector.Setup(p => p.Unprotect(It.IsAny<string>())).Returns((string s) => s[1..]);
            var tenant = new Mock<ITenantContext>();
            tenant.SetupGet(t => t.TenantSlug).Returns("loja-teste");
            tenant.SetupGet(t => t.IsResolved).Returns(true);

            _conexao = new SqliteConnection("DataSource=:memory:");
            _conexao.Open();
            _db = new ConnectVeiculosDbContext(new DbContextOptionsBuilder<ConnectVeiculosDbContext>().UseSqlite(_conexao).Options);
            _db.Database.EnsureCreated();

            _service = new NapistaService(new HttpClient(_napista), Options.Create(new NapistaSettings { ClientId = "connectveiculos" }),
                _config, protector.Object, new StateFalso(), tenant.Object, veiculos.Object, imagens.Object, lojas.Object,
                _anuncios, _db, NullLogger<NapistaService>.Instance);
        }

        public void Dispose()
        {
            _db.Dispose();
            _conexao.Dispose();
        }

        private static string Token(string access = "acesso-1", int expira = 300, int refreshExpira = 1800) =>
            JsonSerializer.Serialize(new { access_token = access, refresh_token = "renova-" + access, expires_in = expira, refresh_expires_in = refreshExpira, token_type = "Bearer" });

        private void UmaLoja() =>
            _napista.Responder($"GET {Api}/seller/access", HttpStatusCode.OK,
                $$"""{"sellers":[{"sellerId":"{{Seller}}","name":"LOJA DE TESTES 1","sellerDocument":"59792667000117"}]}""");

        private async Task ConectarAsync()
        {
            _napista.Responder($"POST {Auth}/token", HttpStatusCode.OK, Token());
            UmaLoja();
            await _service.ConectarAsync("codigo-123", "st-loja-teste");
            _napista.Chamadas.Clear();
        }

        private static readonly NapistaAnuncioDados Dados = new()
        {
            Categoria = "CAR", MarcaId = "VOLKSWAGEN", ModeloId = "GOL", VersaoId = "75630682016",
            AnoModelo = 2020, AnoFabricacao = 2020, Cor = "SILVER", Combustivel = "FLEX", Cambio = "MANUAL", Portas = 4,
            Opcionais = new() { "CAR_1_AIRBAG" }
        };

        // ---------------- Conexao ----------------

        [Fact]
        public void Link_de_conexao_leva_client_id_retorno_escopo_e_state()
        {
            var url = _service.UrlAutorizacao();

            url.Should().StartWith(Auth + "/auth?response_type=code");
            url.Should().Contain("client_id=connectveiculos");
            url.Should().Contain("redirect_uri=" + Uri.EscapeDataString("https://connectveiculos.dev.br/integracoes/napista"));
            url.Should().Contain("scope=openid");
            url.Should().Contain("state=st-loja-teste");
        }

        [Fact]
        public async Task Conectar_troca_o_codigo_sem_segredo_guarda_o_token_cifrado_e_escolhe_a_unica_loja()
        {
            await ConectarAsync();
            var status = await _service.StatusAsync();

            status.Conectado.Should().BeTrue();
            status.Desenvolvimento.Should().BeTrue();
            status.SellerId.Should().Be(Seller);
            status.LojaNome.Should().Be("LOJA DE TESTES 1");
            status.ValidaAte.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(1800), TimeSpan.FromMinutes(1));
            _config.Valores[NapistaService.ChaveToken].Should().StartWith("x", "o token fica cifrado");
        }

        [Fact]
        public async Task Troca_do_codigo_vai_como_formulario_no_padrao_do_keycloak()
        {
            _napista.Responder($"POST {Auth}/token", HttpStatusCode.OK, Token());
            UmaLoja();

            await _service.ConectarAsync("codigo-123", "st-loja-teste");

            var troca = _napista.Chamadas.First(c => c.Url.EndsWith("/token"));
            troca.Corpo.Should().Contain("grant_type=authorization_code").And.Contain("client_id=connectveiculos")
                .And.Contain("code=codigo-123").And.Contain("redirect_uri=https%3A%2F%2Fconnectveiculos.dev.br%2Fintegracoes%2Fnapista");
            troca.Corpo.Should().NotContain("client_secret");
            _napista.Chamadas.Where(c => c.Url.EndsWith("/seller/access")).Should().OnlyContain(c => c.Auth == "acesso-1");
        }

        [Fact]
        public async Task State_de_outra_loja_e_recusado_sem_trocar_codigo()
        {
            var acao = () => _service.ConectarAsync("codigo-123", "st-outra-loja");

            await acao.Should().ThrowAsync<NapistaException>().WithMessage("*inválido ou vencido*");
            _napista.Chamadas.Should().BeEmpty();
        }

        [Fact]
        public async Task Token_vencido_renova_pelo_refresh_antes_de_chamar_a_api()
        {
            _napista.Responder($"POST {Auth}/token", HttpStatusCode.OK, Token("acesso-1", expira: 1));
            UmaLoja();
            await _service.ConectarAsync("codigo-123", "st-loja-teste");
            _napista.Chamadas.Clear();
            _napista.Rotas.Remove($"POST {Auth}/token");
            _napista.Responder($"POST {Auth}/token", HttpStatusCode.OK, Token("acesso-2"));

            await _service.StatusAsync();

            _napista.Chamadas[0].Corpo.Should().Contain("grant_type=refresh_token").And.Contain("refresh_token=renova-acesso-1");
            _napista.Chamadas.Single(c => c.Url.EndsWith("/seller/access")).Auth.Should().Be("acesso-2");
        }

        [Fact]
        public async Task Refresh_recusado_desconecta_e_pede_para_conectar_de_novo()
        {
            await ConectarAsync();
            _napista.Rotas.Remove($"GET {Api}/seller/access");
            _napista.Responder($"GET {Api}/seller/access", HttpStatusCode.Unauthorized, "");
            _napista.Rotas.Remove($"POST {Auth}/token");
            _napista.Responder($"POST {Auth}/token", HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""");

            var status = await _service.StatusAsync();

            status.Erro.Should().Contain("venceu");
            status.Conectado.Should().BeFalse();
            _config.Valores[NapistaService.ChaveToken].Should().BeEmpty();
        }

        // ---------------- Anuncio ----------------

        [Fact]
        public async Task Publicar_cria_o_anuncio_no_formato_da_documentacao_publica_e_manda_as_fotos_por_url()
        {
            await ConectarAsync();
            _napista.Responder($"POST {Api}/seller/{Seller}/offer", HttpStatusCode.Created, """{"id":"110ec39a-7754","versionId":"75630682016"}""");
            _napista.Responder($"PUT {Api}/seller/{Seller}/offer/110ec39a-7754/PUBLISHED", HttpStatusCode.OK, "{}");
            _napista.Responder($"POST {Api}/seller/{Seller}/offer/110ec39a-7754/photos/url", HttpStatusCode.OK, "{}");

            var info = await _service.PublicarAsync(7, Dados);

            var criar = JsonDocument.Parse(_napista.Chamadas.Single(c => c.Metodo == "POST" && c.Url.EndsWith("/offer")).Corpo!).RootElement;
            criar.GetProperty("versionId").GetString().Should().Be("75630682016");
            criar.GetProperty("price").GetInt32().Should().Be(52991, "o NaPista recebe o preço inteiro");
            criar.GetProperty("mileage").GetInt32().Should().Be(45000);
            criar.GetProperty("plate").GetString().Should().Be("ABC1D23");
            criar.GetProperty("fuelType").GetString().Should().Be("FLEX");
            criar.GetProperty("color").GetString().Should().Be("SILVER");
            criar.GetProperty("transmissionType").GetString().Should().Be("MANUAL");
            criar.GetProperty("numberOfDoors").GetInt32().Should().Be(4);
            criar.GetProperty("description").GetString().Should().Be("Único dono");
            criar.GetProperty("equipments")[0].GetString().Should().Be("CAR_1_AIRBAG");

            // Sem status na resposta (como no exemplo da doc): publica pelo endpoint de status.
            _napista.Chamadas.Should().Contain(c => c.Metodo == "PUT" && c.Url.EndsWith("/110ec39a-7754/PUBLISHED"));

            var fotos = JsonDocument.Parse(_napista.Chamadas.Single(c => c.Url.EndsWith("/photos/url")).Corpo!).RootElement.GetProperty("list");
            fotos.GetArrayLength().Should().Be(2);
            fotos[0].GetProperty("url").GetString().Should().Be(
                "https://connectveiculos.dev.br/api/imagens/file?path=uploads%2Fveiculos%2F7%2Ffrente.jpg&max=1200&format=jpeg");
            fotos[1].GetProperty("order").GetInt32().Should().Be(1);

            info.Publicado.Should().BeTrue();
            info.AnuncioId.Should().Be("110ec39a-7754");
            info.Fotos.Should().Be(2);
        }

        [Fact]
        public async Task Atualizar_usa_patch_e_nao_reenvia_fotos_iguais()
        {
            await ConectarAsync();
            _napista.Responder($"POST {Api}/seller/{Seller}/offer", HttpStatusCode.Created, """{"id":"of-1","status":"PUBLISHED"}""");
            _napista.Responder($"POST {Api}/seller/{Seller}/offer/of-1/photos/url", HttpStatusCode.OK, "{}");
            await _service.PublicarAsync(7, Dados);
            _napista.Chamadas.Clear();
            _napista.Responder($"PATCH {Api}/seller/{Seller}/offer/of-1", HttpStatusCode.OK, """{"id":"of-1","status":"PUBLISHED"}""");

            await _service.SincronizarVeiculoAsync(7);

            _napista.Chamadas.Select(c => c.Metodo + " " + c.Url.Replace(Api, ""))
                .Should().Equal($"PATCH /seller/{Seller}/offer/of-1");
        }

        [Fact]
        public async Task Trocar_a_versao_tira_o_anuncio_antigo_e_cria_outro()
        {
            await ConectarAsync();
            _napista.Responder($"POST {Api}/seller/{Seller}/offer", HttpStatusCode.Created, """{"id":"of-1","status":"PUBLISHED"}""");
            _napista.Responder($"POST {Api}/seller/{Seller}/offer", HttpStatusCode.Created, """{"id":"of-2","status":"PUBLISHED"}""");
            _napista.Responder($"POST {Api}/seller/{Seller}/offer/", HttpStatusCode.OK, "{}");
            _napista.Responder($"DELETE {Api}/seller/{Seller}/offer/of-1", HttpStatusCode.OK, "");
            await _service.PublicarAsync(7, Dados);

            var outra = JsonSerializer.Deserialize<NapistaAnuncioDados>(JsonSerializer.Serialize(Dados))!;
            outra.VersaoId = "80913612020";
            var info = await _service.PublicarAsync(7, outra);

            _napista.Chamadas.Should().Contain(c => c.Metodo == "DELETE" && c.Url.EndsWith("/offer/of-1"));
            info.AnuncioId.Should().Be("of-2");
            _napista.Chamadas.Count(c => c.Url.EndsWith("/photos/url")).Should().Be(2, "anúncio novo recebe as fotos de novo");
        }

        [Fact]
        public async Task Vendido_vira_sold_inativo_sai_do_ar_e_pausado_nao_volta_sozinho()
        {
            await ConectarAsync();
            _napista.Responder($"POST {Api}/seller/{Seller}/offer", HttpStatusCode.Created, """{"id":"of-1","status":"PUBLISHED"}""");
            _napista.Responder($"POST {Api}/seller/{Seller}/offer/of-1/photos/url", HttpStatusCode.OK, "{}");
            _napista.Responder($"PUT {Api}/seller/{Seller}/offer/of-1/", HttpStatusCode.OK, "{}");
            await _service.PublicarAsync(7, Dados);

            _veiculos[0].AlterarStatus("V");
            await _service.SincronizarVeiculoAsync(7);
            _napista.Chamadas.Should().Contain(c => c.Metodo == "PUT" && c.Url.EndsWith("/of-1/SOLD"));
            _anuncios.Itens[7].VnpStatus.Should().Be(VeiculoNapista.StatusRemovido);

            // Estorno: volta a disponivel e o anuncio volta.
            _veiculos[0].AlterarStatus("D");
            _napista.Responder($"PATCH {Api}/seller/{Seller}/offer/of-1", HttpStatusCode.OK, """{"id":"of-1","status":"PUBLISHED"}""");
            await _service.SincronizarVeiculoAsync(7);
            _anuncios.Itens[7].EstaPublicado.Should().BeTrue();

            await _service.PausarAsync(7);
            _napista.Chamadas.Should().Contain(c => c.Metodo == "PUT" && c.Url.EndsWith("/of-1/UNPUBLISHED"));
            _napista.Chamadas.Clear();
            await _service.SincronizarVeiculoAsync(7);
            _napista.Chamadas.Should().BeEmpty("pausado pelo usuário só volta pelo botão Anunciar");
        }

        [Fact]
        public async Task Recusa_do_napista_fica_no_anuncio_com_a_mensagem_dele()
        {
            await ConectarAsync();
            _napista.Responder($"POST {Api}/seller/{Seller}/offer", HttpStatusCode.BadRequest, """{"message":"versionId inválido para o ano informado"}""");

            var acao = () => _service.PublicarAsync(7, Dados);

            await acao.Should().ThrowAsync<NapistaException>().WithMessage("O NaPista recusou: versionId inválido para o ano informado");
            _anuncios.Itens[7].VnpStatus.Should().Be(VeiculoNapista.StatusErro);
        }

        [Fact]
        public void Validacao_aponta_tudo_o_que_falta()
        {
            var acao = () => NapistaService.Validar(_veiculos[0], new NapistaAnuncioDados { AnoModelo = 2020, AnoFabricacao = 2020 });
            acao.Should().Throw<NapistaException>().WithMessage("Preencha: versão, câmbio, combustível, cor.");
        }

        // ---------------- Sugestao ----------------

        [Fact]
        public void Sugestao_acha_marca_modelo_versao_e_valores_pelo_cadastro_da_fipe()
        {
            var marcas = new[] { new NapistaItem { Id = "FIAT", Nome = "Fiat" }, new NapistaItem { Id = "VOLKSWAGEN", Nome = "Volkswagen" } };
            var modelos = new[] { new NapistaItem { Id = "GOL", Nome = "Gol" }, new NapistaItem { Id = "GOLF", Nome = "Golf" } };
            var versoes = new[] { new NapistaItem { Id = "1", Nome = "1.6 MSI TOTAL FLEX" }, new NapistaItem { Id = "2", Nome = "1.0 MI TOTAL FLEX 8V" } };
            var cores = new[] { new NapistaItem { Id = "SILVER", Nome = "Prata" }, new NapistaItem { Id = "BLACK", Nome = "Preto" } };

            NapistaSugestao.Marca("VW - VolksWagen", marcas)!.Id.Should().Be("VOLKSWAGEN");
            var modelo = NapistaSugestao.Modelo("GOL 1.0 Mi Total Flex 8V 4p", modelos)!;
            modelo.Id.Should().Be("GOL");
            NapistaSugestao.Versao("GOL 1.0 Mi Total Flex 8V 4p", modelo, versoes)!.Id.Should().Be("2");
            NapistaSugestao.Combustivel("GOL 1.0 Mi Total Flex 8V 4p").Should().Be("FLEX");
            NapistaSugestao.Cambio("COROLLA XEi 2.0 Flex 16V Aut.").Should().Be("AUTOMATIC");
            NapistaSugestao.Portas("GOL 1.0 Mi Total Flex 8V 4p").Should().Be(4);
            NapistaSugestao.Cor("prata", cores).Should().Be("SILVER");
        }

        [Fact]
        public void Atributos_reconhecem_os_grupos_e_o_cambio_tem_valor_padrao()
        {
            var a = NapistaService.LerAtributos(JsonDocument.Parse(
                """{"colors":{"items":[{"id":"YELLOW","name":"Amarelo"}]},"fuelTypes":{"items":[{"id":"ALCOHOL","name":"Álcool"}]},"equipments":{"items":[{"id":"CAR_1_AIRBAG","name":"Airbag"}]}}""").RootElement);

            a.Cores.Should().ContainSingle(c => c.Id == "YELLOW" && c.Nome == "Amarelo");
            a.Combustiveis.Should().ContainSingle(c => c.Id == "ALCOHOL");
            a.Opcionais.Should().ContainSingle(c => c.Id == "CAR_1_AIRBAG");
            a.Cambios.Select(c => c.Id).Should().Equal("MANUAL", "AUTOMATIC");
        }

        // ---------------- Leads ----------------

        [Fact]
        public async Task Leads_entram_na_lista_com_veiculo_pela_placa_e_nao_duplicam()
        {
            await ConectarAsync();
            _napista.Responder($"GET {Api}/seller/{Seller}/leads?page=1", HttpStatusCode.OK, """
                {"pageNumber":1,"pageSize":200,"totalPages":1,"pageData":[
                  {"id":"75647db6","clientName":"Maria Teste","clientEmail":"maria@teste.com.br",
                   "clientPhone":{"phoneType":"MOBILE","areaCode":"11","number":"988887777"},
                   "vehicleVersion":"GOL 1.0","contactMessage":"Tenho interesse.","updatedAt":"2026-11-03T20:07:54.695Z",
                   "upfrontValue":10000,"installments":48,"installmentValue":1287.45,"clientCreditStatus":"QUALIFIED","vehiclePlate":"ABC-1D23"},
                  {"id":"c3dcbd91","clientName":"João","clientEmail":"joao@teste.com.br",
                   "clientPhone":{"areaCode":"19","number":"977776666"},"vehiclePlate":"ZZZ9Z99","updatedAt":"2026-11-02T10:00:00Z"}
                ]}
                """);

            var r = await _service.ImportarLeadsAsync(30);

            r.Should().BeEquivalentTo(new NapistaLeadsResultado { Encontrados = 2, Importados = 2, JaExistiam = 0 });
            var leads = await _db.Leads.AsNoTracking().OrderBy(l => l.LeaNomeCliente).ToListAsync();
            var joao = leads[0];
            var maria = leads[1];
            maria.LeaOrigem.Should().Be("NAPISTA");
            maria.LeaTelefone.Should().Be("11988887777");
            maria.R_VeiId.Should().Be(7, "a placa ABC-1D23 é do Gol do estoque");
            maria.LeaEntrada.Should().Be(10000);
            maria.LeaParcelas.Should().Be(48);
            maria.LeaObservacao.Should().Contain("Tenho interesse").And.Contain("pré-aprovado").And.Contain("48x");
            joao.R_VeiId.Should().BeNull();

            var deNovo = await _service.ImportarLeadsAsync(30);
            deNovo.Importados.Should().Be(0);
            deNovo.JaExistiam.Should().Be(2);
            (await _db.Leads.CountAsync()).Should().Be(2);

            _napista.Chamadas.Should().Contain(c => c.Url.Contains("initialDate=") && c.Url.Contains("endDate="));
        }
    }
}
