using ConnectVeiculos.Core.Entities.Consultas;
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
        private const int Loja = 7;

        private sealed class ZapCarFalso : IZapCarClient
        {
            public Exception? FalhaAoCriar;
            public ZapCarResultado Resultado = new() { Status = "processando" };
            public readonly List<string> Criadas = new();

            public Task<ZapCarCriada> CriarAsync(string servico, string placa, string chave, CancellationToken ct = default)
            {
                if (FalhaAoCriar != null) throw FalhaAoCriar;
                Criadas.Add($"{servico}:{placa}");
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
        private readonly ZapCarFalso _zapcar = new();
        private readonly ConsultaPlacaService _service;

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

            _creditos = new CreditosConsultaService(_master);
            var settings = Options.Create(new ZapCarSettings { ApiKey = "zc_test_x", PrecoCompleta = 60m, PrecoSimples = 15m });
            _service = new ConsultaPlacaService(_db, _creditos, _zapcar, settings, NullLogger<ConsultaPlacaService>.Instance);
        }

        public void Dispose()
        {
            _db.Dispose();
            _master.Dispose();
            _conexaoLoja.Dispose();
            _conexaoMaster.Dispose();
        }

        [Fact]
        public async Task Sem_creditos_nao_consulta_nem_chama_a_zapcar()
        {
            var acao = () => _service.ConsultarAsync(Loja, "ABC1D23", ConsultaPlacaService.Completa, 1);

            await acao.Should().ThrowAsync<DomainException>().WithMessage("*Créditos insuficientes*");
            _zapcar.Criadas.Should().BeEmpty();
        }

        [Fact]
        public async Task Consulta_debita_e_guarda_o_resultado_quando_conclui()
        {
            (await _creditos.CreditarCompraAsync(Loja, 100m, "pix-1")).Should().BeTrue();

            var item = await _service.ConsultarAsync(Loja, "abc-1d23", ConsultaPlacaService.Completa, 1);

            item.Placa.Should().Be("ABC1D23");
            item.Status.Should().Be(ConsultaVeicular.Processando);
            (await _creditos.SaldoAsync(Loja)).Should().Be(40m);

            _zapcar.Resultado = new ZapCarResultado
            {
                Status = "concluido",
                VeiculoJson = """{"placa":"ABC1D23","baixado":false,"restricoes":[{"tipo":"FINANCEIRA","ativa":true,"descricao":"ALIENACAO FIDUCIARIA - BANCO X"}],"debitos":[],"debitos_total_centavos":0,"leilao":{"consta":false,"ocorrencias":0,"fotos":0},"sinistro":false}"""
            };
            var pronto = await _service.ObterAsync(Loja, item.Id);

            pronto!.Status.Should().Be(ConsultaVeicular.Concluida);
            pronto.Usuario.Should().Be("Vendedor");
            pronto.NadaConsta.Should().BeFalse();
            pronto.Alertas.Should().ContainSingle(a => a.Grave && a.Texto.Contains("Gravame"));
            (await _creditos.SaldoAsync(Loja)).Should().Be(40m);
        }

        [Fact]
        public async Task Erro_da_base_devolve_os_creditos_uma_vez_so()
        {
            await _creditos.CreditarCompraAsync(Loja, 60m, "pix-1");
            var item = await _service.ConsultarAsync(Loja, "TST0404", ConsultaPlacaService.Completa, 1);
            (await _creditos.SaldoAsync(Loja)).Should().Be(0m);

            _zapcar.Resultado = new ZapCarResultado { Status = "erro", ErroCodigo = "QUERY_NOT_FOUND", Erro = "..." };
            var falhou = await _service.ObterAsync(Loja, item.Id);
            await _service.ObterAsync(Loja, item.Id);

            falhou!.Status.Should().Be(ConsultaVeicular.Erro);
            falhou.Estornada.Should().BeTrue();
            falhou.Erro.Should().Contain("não foi encontrada");
            (await _creditos.SaldoAsync(Loja)).Should().Be(60m);
        }

        [Fact]
        public async Task Zapcar_recusando_devolve_na_hora()
        {
            await _creditos.CreditarCompraAsync(Loja, 60m, "pix-1");
            _zapcar.FalhaAoCriar = new ZapCarException("A consulta por placa está indisponível no momento.", "SALDO_INSUFICIENTE", false, 402);

            var acao = () => _service.ConsultarAsync(Loja, "ABC1234", ConsultaPlacaService.Completa, 1);

            await acao.Should().ThrowAsync<DomainException>().WithMessage("*Nada foi cobrado*");
            (await _creditos.SaldoAsync(Loja)).Should().Be(60m);
            (await _db.ConsultasVeiculares.SingleAsync()).CvcStatus.Should().Be(ConsultaVeicular.Erro);
        }

        [Fact]
        public async Task Placa_invalida_e_recusada_sem_debitar()
        {
            await _creditos.CreditarCompraAsync(Loja, 60m, "pix-1");

            var acao = () => _service.ConsultarAsync(Loja, "12AB", ConsultaPlacaService.Simples, 1);

            await acao.Should().ThrowAsync<DomainException>().WithMessage("Placa inválida*");
            (await _creditos.SaldoAsync(Loja)).Should().Be(60m);
        }

        [Fact]
        public async Task Mesmo_pix_nao_credita_duas_vezes_e_saldo_e_por_loja()
        {
            (await _creditos.CreditarCompraAsync(Loja, 180m, "pix-9")).Should().BeTrue();
            (await _creditos.CreditarCompraAsync(Loja, 180m, "pix-9")).Should().BeFalse();
            await _creditos.CreditarCompraAsync(99, 60m, "pix-10");

            (await _creditos.SaldoAsync(Loja)).Should().Be(180m);
            (await _creditos.SaldoAsync(99)).Should().Be(60m);
            (await _creditos.ExtratoAsync(Loja, 10)).Should().ContainSingle(m => m.Valor == 180m);
        }

        [Fact]
        public async Task Debito_nao_deixa_o_saldo_negativo()
        {
            await _creditos.CreditarCompraAsync(Loja, 60m, "pix-1");

            (await _creditos.DebitarAsync(Loja, 60m, "a", "x")).Should().BeTrue();
            (await _creditos.DebitarAsync(Loja, 15m, "b", "x")).Should().BeFalse();
            (await _creditos.SaldoAsync(Loja)).Should().Be(0m);
        }

        [Fact]
        public void Le_o_resultado_do_sandbox_da_zapcar()
        {
            const string corpo = """{"id":"d50a","servico":"consulta-completa","placa":"TST0005","ambiente":"test","status":"concluido","veiculo":{"placa":"TST0005","baixado":false,"recall":false,"sinistro":true,"leilao":{"consta":true,"ocorrencias":1,"fotos":3},"restricoes":[{"tipo":"FINANCEIRA","ativa":false,"descricao":null},{"tipo":"RENAJUD","ativa":false,"descricao":null}],"debitos":[],"debitos_total_centavos":0},"pdf_url":"/v1/consultas/d50a/pdf"}""";

            var r = ZapCarClient.LerResultado(corpo);
            var alertas = ResumoConsulta.Avaliar(r.VeiculoJson);

            r.Status.Should().Be("concluido");
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
