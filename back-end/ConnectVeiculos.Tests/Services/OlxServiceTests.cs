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
using ConnectVeiculos.Core.Olx;
using ConnectVeiculos.Infrastructure.Services.Olx;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.Services
{
    /// <summary>
    /// OLX com servidor falso (auth.olx.com.br + apps.olx.com.br/autoupload),
    /// no formato da documentacao em developers.olx.com.br/anuncio/api.
    /// </summary>
    public class OlxServiceTests
    {
        private const string Auth = "https://auth.olx.com.br";
        private const string Api = "https://apps.olx.com.br/autoupload";

        private sealed class OlxFalsa : HttpMessageHandler
        {
            public readonly List<(string Metodo, string Url, string? Corpo, string? Auth)> Chamadas = new();
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
                var rota = Rotas.Keys.Where(k => chave == k).FirstOrDefault()
                           ?? Rotas.Keys.Where(k => chave.StartsWith(k, StringComparison.Ordinal)).OrderByDescending(k => k.Length).FirstOrDefault();
                if (rota == null) return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") };
                var fila = Rotas[rota];
                var (status, resposta) = fila.Count > 1 ? fila.Dequeue() : fila.Peek();
                return new HttpResponseMessage(status) { Content = new StringContent(resposta, Encoding.UTF8, "application/json") };
            }

            public List<JsonElement> Importacoes() => Chamadas
                .Where(c => c.Metodo == "PUT" && c.Url == Api + "/import")
                .Select(c => JsonDocument.Parse(c.Corpo!).RootElement)
                .ToList();
        }

        private sealed class ConfigEmMemoria : IConfiguracaoSistemaRepository
        {
            public readonly Dictionary<string, string> Valores = new();
            public Task<string> GetValorAsync(string chave) => Task.FromResult(Valores.TryGetValue(chave, out var v) ? v : null!);
            public Task SetValorAsync(string chave, string valor) { Valores[chave] = valor; return Task.CompletedTask; }
        }

        private sealed class AnunciosEmMemoria : IVeiculoOlxRepository
        {
            public readonly Dictionary<int, VeiculoOlx> Itens = new();
            public Task<VeiculoOlx?> GetByVeiculoIdAsync(int veiculoId) => Task.FromResult(Itens.TryGetValue(veiculoId, out var a) ? a : null);
            public Task SalvarAsync(VeiculoOlx anuncio) { Itens[anuncio.R_VeiId] = anuncio; return Task.CompletedTask; }
            public Task<HashSet<int>> VeiculosNoArAsync() =>
                Task.FromResult(Itens.Values.Where(a => a.EstaPublicado).Select(a => a.R_VeiId).ToHashSet());
        }

        private sealed class StateFalso : IOAuthStateProtector
        {
            public string Proteger(string tenantSlug) => "st-" + tenantSlug;
            public OAuthStatePayload Validar(string? state, string tenantSlugAtual) =>
                state == "st-" + tenantSlugAtual ? new(tenantSlugAtual, "n", DateTime.UtcNow.AddMinutes(10)) : throw new OAuthStateException("Link de conexão inválido ou vencido.");
            public OAuthStatePayload Decifrar(string? state) => new((state ?? "")[3..], "n", DateTime.UtcNow.AddMinutes(10));
        }

        private readonly OlxFalsa _olx = new();
        private readonly ConfigEmMemoria _config = new();
        private readonly AnunciosEmMemoria _anuncios = new();
        private readonly List<Veiculo> _veiculos = new();
        private readonly List<VeiculoImagem> _fotos = new();
        private Loja _loja = LojaCompleta();
        private readonly OlxService _service;

        private static Loja LojaCompleta(string? whats = "(21) 99876-5432", string? cep = "24230-090") =>
            new(1, "Loja Teste", "Rua A", "10", "Centro", "Niterói", "RJ", cep!, "", "loja@teste.com", "", "", whats!, "", "", "", true,
                lojUrlCatalogo: "https://loja.teste.com.br");

        public OlxServiceTests()
        {
            _veiculos.Add(new Veiculo(7, 1, 1, "VW - VolksWagen", "GOL 1.0 Mi Total Flex 8V 4p", 2020, "ABC-1D23", "9BWZZZ377VT004251",
                "Prata", 45000, 52990.50m, DateTime.Now, "D", "A", 40000m, veiObservacao: "Único dono, revisado.",
                veiOpcionais: "Ar condicionado, Vidros elétricos, Airbag"));
            _fotos.Add(new VeiculoImagem(1, 7, "uploads/veiculos/7/frente.jpg", 0, true));
            _fotos.Add(new VeiculoImagem(2, 7, "uploads/veiculos/7/lado.jpg", 1, true));

            var veiculos = new Mock<IVeiculoRepository>();
            veiculos.Setup(v => v.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((int id) => _veiculos.FirstOrDefault(x => x.VeiId == id)!);
            var imagens = new Mock<IVeiculoImagemRepository>();
            imagens.Setup(i => i.GetByVeiculoIdAsync(It.IsAny<int>())).ReturnsAsync((int id) => _fotos.Where(f => f.R_VeiId == id).ToList());
            var lojas = new Mock<ILojaRepository>();
            lojas.Setup(l => l.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((int id) => id == 1 ? _loja : null!);
            var protector = new Mock<ITokenProtector>();
            protector.Setup(p => p.Protect(It.IsAny<string>())).Returns((string s) => "x" + s);
            protector.Setup(p => p.Unprotect(It.IsAny<string>())).Returns((string s) => s[1..]);
            var tenant = new Mock<ITenantContext>();
            tenant.SetupGet(t => t.TenantSlug).Returns("loja-teste");
            tenant.SetupGet(t => t.IsResolved).Returns(true);

            OlxService.LimparCatalogoEmCache();
            OlxService.EsperaFotos = TimeSpan.Zero;
            _service = new OlxService(new HttpClient(_olx), Options.Create(new OlxSettings { ClientId = "cv-id", ClientSecret = "cv-segredo" }),
                _config, protector.Object, new StateFalso(), tenant.Object, veiculos.Object, imagens.Object, lojas.Object,
                _anuncios, NullLogger<OlxService>.Instance);
        }

        private async Task ConectarAsync()
        {
            _olx.Responder($"POST {Auth}/oauth/token", HttpStatusCode.OK, """{"access_token":"tok-olx","token_type":"Bearer"}""");
            _olx.Responder($"GET {Api}/balance", HttpStatusCode.OK,
                """{"id":1,"name":"Plano Profissional","ads":{"performed":3,"available":47,"total":50},"next_renew_date":"2026-12-20T03:00:00Z"}""");
            await _service.ConectarAsync("codigo-1", "st-loja-teste");
            _olx.Chamadas.Clear();
        }

        private void ImportacaoAceita(string token = "imp-1") =>
            _olx.Responder($"PUT {Api}/import", HttpStatusCode.OK,
                $$"""{"token":"{{token}}","statusCode":0,"statusMessage":"The ads were imported and will be processed","errors":[]}""");

        private static OlxAnuncioDados Dados() => new()
        {
            MarcaId = "60", ModeloId = "12", VersaoId = "345", MarcaNome = "VW - VOLKSWAGEN", ModeloNome = "GOL", VersaoNome = "1.0 MI TOTAL FLEX 8V 4P",
            Ano = 2020, Combustivel = "3", Cambio = "1", Cor = "3", Portas = "2", Potencia = "1", Opcionais = new() { "1", "3", "5" },
            AceitaTroca = true
        };

        private void MudarVeiculo(string status = "D", decimal preco = 52990.50m)
        {
            _veiculos.Clear();
            _veiculos.Add(new Veiculo(7, 1, 1, "VW - VolksWagen", "GOL 1.0 Mi Total Flex 8V 4p", 2020, "ABC-1D23", "9BWZZZ377VT004251",
                "Prata", 45000, preco, DateTime.Now, status, "A", 40000m, veiObservacao: "Único dono, revisado.",
                veiOpcionais: "Ar condicionado, Vidros elétricos, Airbag"));
        }

        // ---------------- Conexao ----------------

        [Fact]
        public void Link_de_conexao_leva_client_id_retorno_escopo_autoupload_e_state()
        {
            var url = _service.UrlAutorizacao();

            url.Should().StartWith(Auth + "/oauth?response_type=code");
            url.Should().Contain("client_id=cv-id");
            url.Should().Contain("redirect_uri=" + Uri.EscapeDataString("https://connectveiculos.dev.br/integracoes/olx"));
            url.Should().Contain("scope=" + Uri.EscapeDataString("autoupload basic_user_info"));
            url.Should().Contain("state=st-loja-teste");
        }

        [Fact]
        public async Task Conectar_troca_o_codigo_com_segredo_guarda_o_token_cifrado_e_mostra_o_saldo()
        {
            _olx.Responder($"POST {Auth}/oauth/token", HttpStatusCode.OK, """{"access_token":"tok-olx","token_type":"Bearer"}""");
            _olx.Responder($"GET {Api}/balance", HttpStatusCode.OK,
                """{"id":1,"name":"Plano Profissional","ads":{"performed":3,"available":47,"total":50},"next_renew_date":"2026-12-20T03:00:00Z"}""");

            var status = await _service.ConectarAsync("codigo-1", "st-loja-teste");

            var troca = _olx.Chamadas.First(c => c.Url == Auth + "/oauth/token");
            troca.Corpo.Should().Contain("grant_type=authorization_code").And.Contain("client_secret=cv-segredo").And.Contain("code=codigo-1");
            _config.Valores[OlxService.ChaveToken].Should().StartWith("x", "o token fica cifrado");
            _olx.Chamadas.Last().Auth.Should().Be("tok-olx");
            status.Conectado.Should().BeTrue();
            status.Conta.Should().Be("Plano Profissional");
            status.AnunciosDisponiveis.Should().Be(47);
            status.AnunciosTotal.Should().Be(50);
        }

        [Fact]
        public async Task State_de_outra_loja_nao_conecta()
        {
            var acao = () => _service.ConectarAsync("codigo-1", "st-outra-loja");
            await acao.Should().ThrowAsync<OlxException>();
            _olx.Chamadas.Should().BeEmpty();
        }

        [Fact]
        public async Task Conta_sem_plano_profissional_avisa_no_status()
        {
            await ConectarAsync();
            _olx.Rotas.Remove($"GET {Api}/balance");
            _olx.Responder($"GET {Api}/balance", HttpStatusCode.Gone, """{"error":"PRODUCT_NOT_FOUND_BY_ACCOUNT"}""");

            var status = await _service.StatusAsync();

            status.Conectado.Should().BeTrue();
            status.Erro.Should().Contain("plano profissional");
        }

        [Fact]
        public async Task Token_recusado_desconecta_e_pede_para_conectar_de_novo()
        {
            await ConectarAsync();
            _olx.Rotas.Remove($"GET {Api}/balance");
            _olx.Responder($"GET {Api}/balance", HttpStatusCode.Unauthorized, """{"error":"ACCESS_DENIED"}""");

            var status = await _service.StatusAsync();

            status.Conectado.Should().BeFalse();
            status.Erro.Should().Contain("Conecte de novo");
            _config.Valores[OlxService.ChaveToken].Should().BeEmpty();
        }

        [Fact]
        public async Task Sem_chaves_do_integrador_avisa_que_esta_em_liberacao()
        {
            var service = new OlxService(new HttpClient(_olx), Options.Create(new OlxSettings()), _config,
                Mock.Of<ITokenProtector>(), new StateFalso(), Mock.Of<ITenantContext>(), Mock.Of<IVeiculoRepository>(),
                Mock.Of<IVeiculoImagemRepository>(), Mock.Of<ILojaRepository>(), _anuncios, NullLogger<OlxService>.Instance);

            (await service.StatusAsync()).Configurado.Should().BeFalse();
            var acao = () => service.UrlAutorizacao();
            acao.Should().Throw<OlxException>().WithMessage("*em liberação*");
        }

        // ---------------- Catalogo e sugestao ----------------

        [Fact]
        public async Task Catalogo_manda_o_token_no_corpo_e_le_o_mapa_nome_para_id()
        {
            await ConectarAsync();
            _olx.Responder($"POST {Api}/car_info", HttpStatusCode.OK, """{"status":"ok","data":{"VW - VOLKSWAGEN":60,"AUDI":6}}""");

            var marcas = await _service.MarcasAsync();

            marcas.Select(m => (m.Id, m.Nome)).Should().Equal(("6", "AUDI"), ("60", "VW - VOLKSWAGEN"));
            JsonDocument.Parse(_olx.Chamadas.Single().Corpo!).RootElement.GetProperty("access_token").GetString().Should().Be("tok-olx");
            await _service.MarcasAsync();
            _olx.Chamadas.Should().HaveCount(1, "o catalogo fica em cache");
        }

        [Fact]
        public async Task Sugestao_acha_marca_modelo_versao_e_codigos_pelo_cadastro()
        {
            await ConectarAsync();
            _olx.Responder($"POST {Api}/car_info/60/12", HttpStatusCode.OK, """{"status":"ok","data":{"1.0 MI TOTAL FLEX 8V 4P":345,"1.6 POWER 8V":346}}""");
            _olx.Responder($"POST {Api}/car_info/60", HttpStatusCode.OK, """{"status":"ok","data":{"GOL":12,"GOLF":13}}""");
            _olx.Responder($"POST {Api}/car_info", HttpStatusCode.OK, """{"status":"ok","data":{"VW - VOLKSWAGEN":60,"FIAT":21}}""");

            var info = await _service.ObterVeiculoAsync(7);

            info.Sugestao.Should().BeTrue();
            info.Dados.MarcaId.Should().Be("60");
            info.Dados.ModeloId.Should().Be("12");
            info.Dados.VersaoId.Should().Be("345");
            info.Dados.Ano.Should().Be(2020);
            info.Dados.Combustivel.Should().Be("3");
            info.Dados.Cambio.Should().BeNull("o nome nao diz o cambio");
            info.Dados.Cor.Should().Be("3");
            info.Dados.Portas.Should().Be("2");
            info.Dados.Potencia.Should().Be("1");
            info.Dados.Opcionais.Should().Equal("1", "3", "5");
            info.Pendencias.Should().BeEmpty();
        }

        // ---------------- Anunciar ----------------

        [Fact]
        public async Task Anunciar_manda_o_anuncio_no_formato_da_olx_e_guarda_o_token()
        {
            await ConectarAsync();
            ImportacaoAceita();

            var info = await _service.PublicarAsync(7, Dados());

            var corpo = _olx.Importacoes().Single();
            corpo.GetProperty("access_token").GetString().Should().Be("tok-olx");
            var ad = corpo.GetProperty("ad_list")[0];
            ad.GetProperty("id").GetString().Should().Be("cv7");
            ad.GetProperty("operation").GetString().Should().Be("insert");
            ad.GetProperty("category").GetInt32().Should().Be(2020);
            ad.GetProperty("subject").GetString().Should().Be("VW - VOLKSWAGEN GOL 1.0 MI TOTAL FLEX 8V 4P 2020");
            ad.GetProperty("body").GetString().Should().Be("Único dono, revisado.");
            ad.GetProperty("phone").GetInt64().Should().Be(21998765432);
            ad.GetProperty("type").GetString().Should().Be("s");
            ad.GetProperty("price").GetInt64().Should().Be(52991);
            ad.GetProperty("zipcode").GetString().Should().Be("24230090");
            ad.GetProperty("images").EnumerateArray().Select(i => i.GetString()).Should().Equal(
                "https://loja.teste.com.br/api/imagens/file?path=uploads%2Fveiculos%2F7%2Ffrente.jpg&max=1600&format=jpeg",
                "https://loja.teste.com.br/api/imagens/file?path=uploads%2Fveiculos%2F7%2Flado.jpg&max=1600&format=jpeg");
            var p = ad.GetProperty("params");
            p.GetProperty("vehicle_brand").GetString().Should().Be("60");
            p.GetProperty("vehicle_model").GetString().Should().Be("12");
            p.GetProperty("vehicle_version").GetString().Should().Be("345");
            p.GetProperty("regdate").GetString().Should().Be("2020");
            p.GetProperty("mileage").GetInt32().Should().Be(45000);
            p.GetProperty("vehicle_tag").GetString().Should().Be("ABC1D23");
            p.GetProperty("fuel").GetString().Should().Be("3");
            p.GetProperty("gearbox").GetString().Should().Be("1");
            p.GetProperty("carcolor").GetString().Should().Be("3");
            p.GetProperty("doors").GetString().Should().Be("2");
            p.GetProperty("motorpower").GetString().Should().Be("1");
            p.GetProperty("exchange").GetString().Should().Be("1");
            p.GetProperty("car_features").EnumerateArray().Select(i => i.GetString()).Should().Equal("1", "3", "5");
            p.TryGetProperty("cartype", out _).Should().BeFalse("opcional vazio nao vai (a OLX recusa vazio)");
            p.TryGetProperty("owner", out _).Should().BeFalse();

            info.Publicado.Should().BeTrue();
            info.Situacao.Should().Be("pending");
            info.Fotos.Should().Be(2);
            _anuncios.Itens[7].VolToken.Should().Be("imp-1");
        }

        [Fact]
        public async Task Recusa_na_validacao_traduz_o_erro_e_nao_fica_no_ar()
        {
            await ConectarAsync();
            _olx.Responder($"PUT {Api}/import", HttpStatusCode.OK,
                """{"token":null,"statusCode":-4,"statusMessage":"error","errors":[{"id":"cv7","status":"error","messages":[{"category":"ERROR_VEHICLE_VERSION_INVALID"},{"category":"NO_REGION"}]}]}""");

            var acao = () => _service.PublicarAsync(7, Dados());

            (await acao.Should().ThrowAsync<OlxException>()).Which.Message
                .Should().Contain("versão inválida").And.Contain("CEP da loja");
            _anuncios.Itens[7].VolStatus.Should().Be(VeiculoOlx.StatusErro);
            (await _anuncios.VeiculosNoArAsync()).Should().BeEmpty();
        }

        [Fact]
        public async Task Conta_sem_permissao_explica_o_plano_profissional()
        {
            await ConectarAsync();
            _olx.Responder($"PUT {Api}/import", HttpStatusCode.OK, """{"token":null,"statusCode":-6,"statusMessage":"without permission","errors":[]}""");

            var acao = () => _service.PublicarAsync(7, Dados());

            (await acao.Should().ThrowAsync<OlxException>()).Which.Message.Should().Contain("plano profissional");
        }

        [Fact]
        public async Task Loja_sem_telefone_e_cep_nao_envia_e_diz_o_que_falta()
        {
            await ConectarAsync();
            _loja = LojaCompleta(whats: "", cep: "");

            var acao = () => _service.PublicarAsync(7, Dados());

            (await acao.Should().ThrowAsync<OlxException>()).Which.Message
                .Should().Contain("telefone").And.Contain("CEP");
            _olx.Importacoes().Should().BeEmpty();
        }

        [Fact]
        public async Task Campos_obrigatorios_do_anuncio()
        {
            await ConectarAsync();
            var acao = () => _service.PublicarAsync(7, new OlxAnuncioDados { MarcaId = "60" });
            (await acao.Should().ThrowAsync<OlxException>()).Which.Message
                .Should().Contain("modelo").And.Contain("versão").And.Contain("combustível").And.Contain("câmbio").And.Contain("cor");
        }

        [Fact]
        public async Task Conferir_o_resultado_guarda_o_link_e_o_numero_do_anuncio()
        {
            await ConectarAsync();
            ImportacaoAceita();
            await _service.PublicarAsync(7, Dados());
            _olx.Responder($"POST {Api}/import/imp-1", HttpStatusCode.OK,
                """{"autoupload_status":"done","ads":{"cv7":{"status":"accepted","operation":"insert","message":[],"list_id":998877,"url":"https://rj.olx.com.br/autos/998877"}}}""");

            var info = await _service.ObterVeiculoAsync(7);

            info.Situacao.Should().Be("accepted");
            info.Url.Should().Be("https://rj.olx.com.br/autos/998877");
            _anuncios.Itens[7].VolListId.Should().Be("998877");
            _anuncios.Itens[7].VolToken.Should().BeNull();
            JsonDocument.Parse(_olx.Chamadas.Last().Corpo!).RootElement.GetProperty("access_token").GetString().Should().Be("tok-olx");
        }

        [Fact]
        public async Task Resultado_pendente_continua_esperando_e_recusado_mostra_o_motivo()
        {
            await ConectarAsync();
            ImportacaoAceita();
            await _service.PublicarAsync(7, Dados());
            _olx.Responder($"POST {Api}/import/imp-1", HttpStatusCode.OK,
                """{"autoupload_status":"pending","ads":{"cv7":{"status":"queued","operation":"insert"}}}""");
            _olx.Responder($"POST {Api}/import/imp-1", HttpStatusCode.OK,
                """{"autoupload_status":"done","ads":{"cv7":{"status":"refused","operation":"insert","message":[{"error":"ERROR_DOWNLOADING_IMAGE"}]}}}""");

            (await _service.ObterVeiculoAsync(7)).Situacao.Should().Be("queued");
            _anuncios.Itens[7].VolToken.Should().Be("imp-1");

            var info = await _service.ObterVeiculoAsync(7);
            info.Situacao.Should().Be("refused");
            info.Erro.Should().Contain("não conseguiu baixar uma das fotos");
            (await _anuncios.VeiculosNoArAsync()).Should().Contain(7, "o repositorio em memoria nao filtra; o real esconde recusados");
        }

        // ---------------- Automatico ----------------

        [Fact]
        public async Task Edicao_sem_mudanca_nao_reenvia_e_mudanca_de_preco_reenvia()
        {
            await ConectarAsync();
            ImportacaoAceita();
            await _service.PublicarAsync(7, Dados());

            await _service.SincronizarVeiculoAsync(7);
            _olx.Importacoes().Should().HaveCount(1, "nada mudou");

            MudarVeiculo(preco: 49900m);
            await _service.SincronizarVeiculoAsync(7);
            var ultimo = _olx.Importacoes().Last().GetProperty("ad_list")[0];
            ultimo.GetProperty("operation").GetString().Should().Be("insert", "insert com o mesmo id altera o anuncio");
            ultimo.GetProperty("id").GetString().Should().Be("cv7");
            ultimo.GetProperty("price").GetInt64().Should().Be(49900);
        }

        [Fact]
        public async Task Foto_nova_reenvia_com_a_lista_nova()
        {
            await ConectarAsync();
            ImportacaoAceita();
            await _service.PublicarAsync(7, Dados());

            _fotos.Add(new VeiculoImagem(3, 7, "uploads/veiculos/7/traseira.jpg", 2, true));
            await _service.FotosMudaramAsync(7);

            _olx.Importacoes().Should().HaveCount(2);
            _olx.Importacoes().Last().GetProperty("ad_list")[0].GetProperty("images").GetArrayLength().Should().Be(3);
        }

        [Fact]
        public async Task Vendido_tira_do_ar_e_estorno_volta()
        {
            await ConectarAsync();
            ImportacaoAceita();
            await _service.PublicarAsync(7, Dados());

            MudarVeiculo("V");
            await _service.SincronizarVeiculoAsync(7);
            var remocao = _olx.Importacoes().Last().GetProperty("ad_list")[0];
            remocao.GetProperty("operation").GetString().Should().Be("delete");
            remocao.GetProperty("id").GetString().Should().Be("cv7");
            _anuncios.Itens[7].VolStatus.Should().Be(VeiculoOlx.StatusRemovido);

            await _service.SincronizarVeiculoAsync(7);
            _olx.Importacoes().Should().HaveCount(2, "ja estava fora do ar");

            MudarVeiculo("D");
            await _service.SincronizarVeiculoAsync(7);
            _olx.Importacoes().Last().GetProperty("ad_list")[0].GetProperty("operation").GetString().Should().Be("insert");
            _anuncios.Itens[7].VolStatus.Should().Be(VeiculoOlx.StatusAtivo);
        }

        [Fact]
        public async Task Tirar_do_ar_pelo_botao_nao_volta_sozinho()
        {
            await ConectarAsync();
            ImportacaoAceita();
            await _service.PublicarAsync(7, Dados());

            var info = await _service.PausarAsync(7);
            info.Publicado.Should().BeFalse();
            info.Status.Should().Be(VeiculoOlx.StatusPausado);

            MudarVeiculo(preco: 47000m);
            await _service.SincronizarVeiculoAsync(7);
            _olx.Importacoes().Should().HaveCount(2, "anuncio + delete; pausado nao reenvia");

            await _service.PublicarAsync(7, Dados());
            _olx.Importacoes().Last().GetProperty("ad_list")[0].GetProperty("operation").GetString().Should().Be("insert");
        }

        [Fact]
        public async Task Veiculo_nunca_anunciado_ou_sem_conexao_nao_chama_a_olx()
        {
            await _service.SincronizarVeiculoAsync(7);
            await ConectarAsync();
            await _service.SincronizarVeiculoAsync(7);
            _olx.Chamadas.Should().BeEmpty();
        }

        // ---------------- Codigos ----------------

        [Theory]
        [InlineData("GOL 1.0 Mi Total Flex 8V 4p", "1")]
        [InlineData("COROLLA XEI 2.0 Flex 16V Aut.", "10")]
        [InlineData("ONIX HATCH LT 1.4 8V", "4")]
        [InlineData("S10 LTZ 2.8 4x4 Diesel", "10")]
        [InlineData("RANGER XLT 3.2", "11")]
        [InlineData("MUSTANG GT 5.0 V8", "12")]
        [InlineData("UP! TAKE", null)]
        public void Potencia_pelo_nome_do_modelo(string modelo, string? esperado) =>
            OlxCodigos.Potencia(modelo).Should().Be(esperado);

        [Theory]
        [InlineData(2020, "2020")]
        [InlineData(1980, "1980")]
        [InlineData(1978, "1975")]
        [InlineData(1969, "1965")]
        [InlineData(1940, "1950")]
        public void Ano_antigo_vai_na_faixa_da_olx(int ano, string esperado) => OlxCodigos.AnoOlx(ano).Should().Be(esperado);

        [Theory]
        [InlineData("+55 (21) 99876-5432", "21998765432")]
        [InlineData("(11) 3333-4444", "1133334444")]
        [InlineData("021 99876-5432", "21998765432")]
        [InlineData("99876-5432", null)]
        public void Telefone_da_loja_vai_com_ddd_e_sem_55(string tel, string? esperado) =>
            OlxService.Telefone(LojaCompleta(whats: tel)).Should().Be(esperado);

        [Fact]
        public void Cambio_automatizado_e_automatico_tem_codigos_diferentes()
        {
            OlxCodigos.Cambio("ARGO DRIVE 1.3 AUTOMATIZADO").Should().Be("4");
            OlxCodigos.Cambio("COROLLA XEI 2.0 Aut.").Should().Be("2");
            OlxCodigos.Cambio("GOL 1.0 MANUAL").Should().Be("1");
        }
    }
}
