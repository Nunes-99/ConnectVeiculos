using ConnectVeiculos.Core.Entities.Consultas;
using ConnectVeiculos.Core.Entities.Tenants;
using ConnectVeiculos.Core.Entities.Usuarios;
using ConnectVeiculos.Core.Exceptions;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using ConnectVeiculos.Infrastructure.Services.ConsultaPlaca;
using ConnectVeiculos.Infrastructure.Services.Pagamentos;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ConnectVeiculos.Tests.Services
{
    public class ConsultaPlacaServiceTests : IDisposable
    {
        // Trecho do GET /v1/servicos real (custos de 27/11/2026).
        private const string Catalogo = """
        {"servicos":[
          {"servico":"consulta","nome":"Consulta Simples","categoria":"CONSULTAS","disponivel_api":true,"ordem":10,"requer":["placa"],"preco":5.98},
          {"servico":"consulta-completa","nome":"Consulta Completa","categoria":"CONSULTAS","disponivel_api":true,"ordem":20,"requer":["placa"],"preco":36},
          {"servico":"comunicacao-venda","nome":"Detalhe Comunicação de Venda","categoria":"CONSULTAS","disponivel_api":true,"ordem":66,"requer":["placa"],"preco":6.99},
          {"servico":"credito-unico-cpf","nome":"Crédito Único CPF","categoria":"ANALISE_PESSOA","disponivel_api":true,"ordem":95,"requer":["documento"],"preco":24.3},
          {"servico":"consulta-nfe","nome":"Consulta NF-e","categoria":"DOCUMENTOS","disponivel_api":true,"ordem":140,"requer":["chave"],"preco":1.99},
          {"servico":"atpve","nome":"2ª via do ATPV-e","categoria":"DOCUMENTOS","disponivel_api":true,"ordem":120,"requer":["placa"],"preco":39.99},
          {"servico":"crlv","nome":"CRLV Digital","categoria":"DOCUMENTOS","disponivel_api":true,"ordem":130,"requer":["placa","uf"],"preco":null,
           "preco_por_uf":[{"uf":"AC","preco":80,"requer":["placa","uf"],"disponivel":false},{"uf":"SP","preco":29.99,"requer":["placa","uf"],"disponivel":true},{"uf":"RJ","preco":37.5,"requer":["placa","uf","cpf","renavam"],"disponivel":true}]},
          {"servico":"fipe","nome":"Tabela FIPE","categoria":"CONSULTAS","disponivel_api":true,"ordem":5,"requer":["placa"],"preco":0.25},
          {"servico":"novo-sem-api","nome":"X","categoria":"CONSULTAS","disponivel_api":false,"requer":["placa"],"preco":1}
        ]}
        """;

        private sealed class ZapCarFalso : IZapCarClient
        {
            public Exception? FalhaAoCriar;
            public ZapCarResultado Resultado = new() { Status = "processando" };
            public readonly List<ZapCarPedido> Criadas = new();
            public readonly List<ZapCarServico> Servicos = ZapCarClient.LerCatalogo(Catalogo);

            public Task<IReadOnlyList<ZapCarServico>> ServicosAsync(CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<ZapCarServico>>(Servicos);

            public Task<AvisoAtpve?> AvisoAtpveAsync(CancellationToken ct = default)
                => Task.FromResult<AvisoAtpve?>(new AvisoAtpve { Versao = "2026-09-23" });

            public Task<ZapCarCriada> CriarAsync(ZapCarPedido pedido, string chave, CancellationToken ct = default)
            {
                if (FalhaAoCriar != null) throw FalhaAoCriar;
                Criadas.Add(pedido);
                return Task.FromResult(new ZapCarCriada { Id = "zc-" + Criadas.Count, ValorCobrado = 36m, Teste = true });
            }

            public Task<ZapCarResultado> ObterAsync(string id, CancellationToken ct = default) => Task.FromResult(Resultado);

            public Task<(byte[] Conteudo, string Tipo)?> DocumentoAsync(string id, CancellationToken ct = default)
                => Task.FromResult<(byte[], string)?>(null);
        }

        private readonly SqliteConnection _conexaoLoja;
        private readonly SqliteConnection _conexaoMaster;
        private readonly ConnectVeiculosDbContext _db;
        private readonly MasterDbContext _master;
        private readonly CreditosConsultaService _creditos;
        private readonly PrecosConsultaService _precos;
        private readonly ZapCarFalso _zapcar = new();
        private readonly ConsultaPlacaService _service;
        private readonly int _loja;
        private readonly int _premium;

        public ConsultaPlacaServiceTests()
        {
            _conexaoLoja = new SqliteConnection("DataSource=:memory:");
            _conexaoLoja.Open();
            _db = new ConnectVeiculosDbContext(new DbContextOptionsBuilder<ConnectVeiculosDbContext>().UseSqlite(_conexaoLoja).Options);
            _db.Database.EnsureCreated();
            _db.Usuarios.Add(new Usuario(1, "Vendedor", "", "", "v@x.com", "hash", "Vendedor", true));
            _db.SaveChanges();

            _conexaoMaster = new SqliteConnection("DataSource=:memory:");
            _conexaoMaster.Open();
            _master = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>().UseSqlite(_conexaoMaster).Options);
            _master.Database.EnsureCreated();
            var free = new Plano("Free", 0, 5, 1, 1, 20, 1);
            var premium = new Plano("Premium", 699m, 200, 5, 15, null, 4);
            _master.Planos.AddRange(free, premium);
            _master.SaveChanges();
            var tenant = new Tenant("loja", "Loja");
            tenant.AlterarPlano(free.PlaId);
            _master.Tenants.Add(tenant);
            _master.SaveChanges();
            _loja = tenant.TenId;
            _premium = premium.PlaId;

            var settings = Options.Create(new ZapCarSettings { ApiKey = "zc_test_x" });
            _creditos = new CreditosConsultaService(_master);
            _precos = new PrecosConsultaService(_master, settings);
            _service = new ConsultaPlacaService(_db, _creditos, _zapcar, _precos, _master, settings, NullLogger<ConsultaPlacaService>.Instance);
        }

        public void Dispose()
        {
            _db.Dispose();
            _master.Dispose();
            _conexaoLoja.Dispose();
            _conexaoMaster.Dispose();
        }

        private static PedidoConsulta Placa(string servico, string placa) => new() { Servico = servico, Placa = placa };

        private void MudarPlano(int planoId)
        {
            var t = _master.Tenants.Single(x => x.TenId == _loja);
            t.AlterarPlano(planoId);
            _master.SaveChanges();
        }

        [Fact]
        public void Catalogo_traz_so_o_que_a_api_aceita_e_tem_preco()
        {
            _zapcar.Servicos.Select(s => s.Id).Should().BeEquivalentTo(new[]
                { "consulta", "consulta-completa", "comunicacao-venda", "credito-unico-cpf", "atpve", "crlv", "consulta-nfe" });
            _zapcar.Servicos.Single(s => s.Id == "crlv").PorUf.Select(u => u.Uf).Should().Equal("SP", "RJ");
        }

        [Fact]
        public void Acrescimo_soma_o_valor_fixo_do_plano_ao_custo()
        {
            PrecosConsultaService.ComAcrescimo(36m, 20m).Should().Be(56m);
            PrecosConsultaService.ComAcrescimo(36m, 5m).Should().Be(41m);
            PrecosConsultaService.ComAcrescimo(5.98m, 20m).Should().Be(25.98m);
            PrecosConsultaService.ComAcrescimo(1.99m, 0m).Should().Be(1.99m);
        }

        [Theory]
        [InlineData("Free", 20)]
        [InlineData("Essencial", 15)]
        [InlineData("Profissional", 10)]
        [InlineData("premium", 5)]
        [InlineData("Plano novo", 10)]
        public void Acrescimo_padrao_por_plano(string plano, decimal esperado)
        {
            new ZapCarSettings().AcrescimoDoPlano(plano).Should().Be(esperado);
        }

        [Fact]
        public async Task Preco_depende_do_plano_e_o_valor_fixo_vence_o_acrescimo()
        {
            var (free, plano) = await _service.ServicosAsync(_loja);
            plano.Should().Be("Free");
            free.Single(s => s.Id == "consulta-completa").Preco.Should().Be(56m); // 36 + R$ 20

            MudarPlano(_premium);
            var (premium, _) = await _service.ServicosAsync(_loja);
            premium.Single(s => s.Id == "consulta-completa").Preco.Should().Be(41m); // 36 + R$ 5
            premium.Single(s => s.Id == "crlv").PrecosUf.Single(u => u.Uf == "SP").Preco.Should().Be(34.99m); // 29,99 + R$ 5

            await _precos.DefinirAsync("consulta-completa", null, 49.90m);
            (await _service.ServicosAsync(_loja)).Servicos.Single(s => s.Id == "consulta-completa").Preco.Should().Be(49.90m);
            await _precos.DefinirAsync("consulta-completa", _premium, 39.90m);
            (await _service.ServicosAsync(_loja)).Servicos.Single(s => s.Id == "consulta-completa").Preco.Should().Be(39.90m);
            await _precos.DefinirAsync("consulta-completa", _premium, null);
            (await _service.ServicosAsync(_loja)).Servicos.Single(s => s.Id == "consulta-completa").Preco.Should().Be(49.90m);
        }

        [Fact]
        public async Task Sem_creditos_nao_consulta_nem_chama_a_zapcar()
        {
            var acao = () => _service.ConsultarAsync(_loja, Placa(ConsultaPlacaService.Completa, "ABC1D23"), 1);

            await acao.Should().ThrowAsync<DomainException>().WithMessage("*Créditos insuficientes*");
            _zapcar.Criadas.Should().BeEmpty();
        }

        [Fact]
        public async Task Consulta_debita_o_preco_do_plano_e_guarda_o_resultado()
        {
            await _creditos.CreditarCompraAsync(_loja, 100m, "pix-1");

            var item = await _service.ConsultarAsync(_loja, Placa(ConsultaPlacaService.Completa, "abc-1d23"), 1);

            item.Placa.Should().Be("ABC1D23");
            item.Valor.Should().Be(56m);
            (await _creditos.SaldoAsync(_loja)).Should().Be(44m);

            _zapcar.Resultado = ZapCarClient.LerResultado("""
                {"id":"zc-1","status":"concluido","dados":{"x":1},"pdf_url":"/v1/consultas/zc-1/pdf",
                 "veiculo":{"placa":"ABC1D23","baixado":false,"restricoes":[{"tipo":"FINANCEIRA","ativa":true,"descricao":"ALIENACAO FIDUCIARIA - BANCO X"}],"debitos":[],"debitos_total_centavos":0,"leilao":{"consta":false,"ocorrencias":0,"fotos":0},"sinistro":false}}
                """);
            var pronto = await _service.ObterAsync(_loja, item.Id);

            pronto!.Status.Should().Be(ConsultaVeicular.Concluida);
            pronto.TemDocumento.Should().BeTrue();
            pronto.Dados.Should().NotBeNull();
            pronto.Usuario.Should().Be("Vendedor");
            pronto.NadaConsta.Should().BeFalse();
            pronto.Alertas.Should().ContainSingle(a => a.Grave && a.Texto.Contains("Gravame"));
        }

        [Fact]
        public async Task Erro_da_base_devolve_os_creditos_uma_vez_so()
        {
            await _creditos.CreditarCompraAsync(_loja, 60m, "pix-1");
            var item = await _service.ConsultarAsync(_loja, Placa(ConsultaPlacaService.Completa, "TST0404"), 1);

            _zapcar.Resultado = new ZapCarResultado { Status = "erro", ErroCodigo = "QUERY_NOT_FOUND", Erro = "..." };
            var falhou = await _service.ObterAsync(_loja, item.Id);
            await _service.ObterAsync(_loja, item.Id);

            falhou!.Status.Should().Be(ConsultaVeicular.Erro);
            falhou.Estornada.Should().BeTrue();
            falhou.Erro.Should().Contain("Nada foi encontrado");
            (await _creditos.SaldoAsync(_loja)).Should().Be(60m);
        }

        [Fact]
        public async Task Zapcar_recusando_devolve_na_hora()
        {
            await _creditos.CreditarCompraAsync(_loja, 60m, "pix-1");
            _zapcar.FalhaAoCriar = new ZapCarException("A consulta está indisponível no momento.", "SALDO_INSUFICIENTE", false, 402);

            var acao = () => _service.ConsultarAsync(_loja, Placa(ConsultaPlacaService.Simples, "ABC1234"), 1);

            await acao.Should().ThrowAsync<DomainException>().WithMessage("*Nada foi cobrado*");
            (await _creditos.SaldoAsync(_loja)).Should().Be(60m);
            (await _db.ConsultasVeiculares.SingleAsync()).CvcStatus.Should().Be(ConsultaVeicular.Erro);
        }

        [Fact]
        public async Task Consulta_de_pessoa_exige_finalidade_e_guarda_o_cpf_mascarado()
        {
            await _creditos.CreditarCompraAsync(_loja, 100m, "pix-1");
            var pedido = new PedidoConsulta { Servico = "credito-unico-cpf", Documento = "123.456.789-09" };

            await FluentActions.Awaiting(() => _service.ConsultarAsync(_loja, pedido, 1))
                .Should().ThrowAsync<DomainException>().WithMessage("*finalidade*");
            pedido.Finalidade = "ANALISE_CREDITO";
            await FluentActions.Awaiting(() => _service.ConsultarAsync(_loja, pedido, 1))
                .Should().ThrowAsync<DomainException>().WithMessage("Confirme*");
            (await _creditos.SaldoAsync(_loja)).Should().Be(100m);

            pedido.DeclaracaoFinalidade = true;
            var item = await _service.ConsultarAsync(_loja, pedido, 1);

            item.Placa.Should().BeEmpty();
            item.Identificador.Should().Be("***.456.789-**");
            _zapcar.Criadas.Single().Documento.Should().Be("12345678909");
            _zapcar.Criadas.Single().DeclaracaoFinalidade.Should().BeTrue();
            var json = _zapcar.Criadas.Single().ParaJson().ToJsonString();
            json.Should().Contain("\"finalidade\":\"ANALISE_CREDITO\"").And.NotContain("placa");
        }

        [Fact]
        public void Crlv_pede_os_campos_do_estado_escolhido()
        {
            var crlv = _zapcar.Servicos.Single(s => s.Id == "crlv");

            FluentActions.Invoking(() => ConsultaPlacaService.Validar(crlv, new PedidoConsulta { Servico = "crlv", Placa = "ABC1D23", Uf = "AC" }))
                .Should().Throw<DomainException>().WithMessage("*estado disponível*");
            FluentActions.Invoking(() => ConsultaPlacaService.Validar(crlv, new PedidoConsulta { Servico = "crlv", Placa = "ABC1D23", Uf = "RJ" }))
                .Should().Throw<DomainException>().WithMessage("*Renavam*");

            var (pedido, _, placa) = ConsultaPlacaService.Validar(crlv, new PedidoConsulta
                { Servico = "crlv", Placa = "ABC1D23", Uf = "rj", Renavam = "12345678901", Cpf = "123.456.789-09" });
            placa.Should().Be("ABC1D23");
            pedido.Uf.Should().Be("RJ");
            pedido.Cpf.Should().Be("12345678909");
        }

        [Fact]
        public void Atpve_exige_o_aceite_do_aviso_e_nfe_a_chave_de_44_numeros()
        {
            var atpve = _zapcar.Servicos.Single(s => s.Id == "atpve");
            FluentActions.Invoking(() => ConsultaPlacaService.Validar(atpve, Placa("atpve", "ABC1D23")))
                .Should().Throw<DomainException>().WithMessage("*ATPV-e*");
            var (pedido, _, _) = ConsultaPlacaService.Validar(atpve, new PedidoConsulta { Servico = "atpve", Placa = "ABC1D23", AceiteAtpveVersao = "2026-09-23" });
            pedido.ParaJson().ToJsonString().Should().Contain("\"aceite_atpve\":{\"aceito\":true,\"versao\":\"2026-09-23\"}");

            var nfe = _zapcar.Servicos.Single(s => s.Id == "consulta-nfe");
            FluentActions.Invoking(() => ConsultaPlacaService.Validar(nfe, new PedidoConsulta { Servico = "consulta-nfe", Chave = "123" }))
                .Should().Throw<DomainException>().WithMessage("*44*");
        }

        [Fact]
        public async Task Placa_invalida_e_recusada_sem_debitar()
        {
            await _creditos.CreditarCompraAsync(_loja, 60m, "pix-1");

            var acao = () => _service.ConsultarAsync(_loja, Placa(ConsultaPlacaService.Simples, "12AB"), 1);

            await acao.Should().ThrowAsync<DomainException>().WithMessage("Placa inválida*");
            (await _creditos.SaldoAsync(_loja)).Should().Be(60m);
        }

        [Fact]
        public async Task Mesmo_pix_nao_credita_duas_vezes_e_saldo_e_por_loja()
        {
            (await _creditos.CreditarCompraAsync(_loja, 180m, "pix-9")).Should().BeTrue();
            (await _creditos.CreditarCompraAsync(_loja, 180m, "pix-9")).Should().BeFalse();
            await _creditos.CreditarCompraAsync(99, 60m, "pix-10");

            (await _creditos.SaldoAsync(_loja)).Should().Be(180m);
            (await _creditos.SaldoAsync(99)).Should().Be(60m);
            (await _creditos.ExtratoAsync(_loja, 10)).Should().ContainSingle(m => m.Valor == 180m);
        }

        [Fact]
        public async Task Debito_nao_deixa_o_saldo_negativo()
        {
            await _creditos.CreditarCompraAsync(_loja, 60m, "pix-1");

            (await _creditos.DebitarAsync(_loja, 60m, "a", "x")).Should().BeTrue();
            (await _creditos.DebitarAsync(_loja, 15m, "b", "x")).Should().BeFalse();
            (await _creditos.SaldoAsync(_loja)).Should().Be(0m);
        }

        [Fact]
        public void Le_o_resultado_do_sandbox_da_zapcar()
        {
            const string corpo = """{"id":"d50a","servico":"consulta-completa","placa":"TST0005","ambiente":"test","status":"concluido","veiculo":{"placa":"TST0005","baixado":false,"recall":false,"sinistro":true,"leilao":{"consta":true,"ocorrencias":1,"fotos":3},"restricoes":[{"tipo":"FINANCEIRA","ativa":false,"descricao":null},{"tipo":"RENAJUD","ativa":false,"descricao":null}],"debitos":[],"debitos_total_centavos":0},"pdf_url":"/v1/consultas/d50a/pdf"}""";

            var r = ZapCarClient.LerResultado(corpo);
            using var doc = System.Text.Json.JsonDocument.Parse(r.RespostaJson!);
            var alertas = ResumoConsulta.Avaliar(doc.RootElement.GetProperty("veiculo").GetRawText());

            r.Status.Should().Be("concluido");
            r.TemDocumento.Should().BeTrue();
            alertas.Select(a => a.Texto).Should().BeEquivalentTo(new[] { "Passou por leilão (1 registro)", "Registro de sinistro (batida/perda)" });
            alertas.Should().OnlyContain(a => a.Grave);
        }

        [Fact]
        public void Nada_verificado_nao_vira_alerta_e_debito_aparece()
        {
            var alertas = ResumoConsulta.Avaliar("""{"restricoes":[],"debitos":[{"tipo":"IPVA","valor_centavos":97617,"valor_informado":true}],"debitos_total_centavos":97617,"leilao":null,"sinistro":null,"recall":null}""");

            alertas.Should().ContainSingle().Which.Texto.Should().Be("Débitos em aberto: R$ 976,17");
        }

        [Fact]
        public void Referencia_do_pix_de_creditos_vai_e_volta()
        {
            var r = MercadoPagoAssinaturaService.ReferenciaCreditos(12, 180m);

            r.Should().Be("cvcred:12:18000");
            MercadoPagoAssinaturaService.LerReferenciaCreditos(r, out var tenant, out var valor).Should().BeTrue();
            tenant.Should().Be(12);
            valor.Should().Be(180m);
            MercadoPagoAssinaturaService.LerReferenciaCreditos("cvpix:12:3", out _, out _).Should().BeFalse();
        }
    }
}
