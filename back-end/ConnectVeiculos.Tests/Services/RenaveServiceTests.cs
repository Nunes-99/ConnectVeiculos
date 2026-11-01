using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using ConnectVeiculos.Core.Entities.Renave;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Renave;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Core.Renave;
using ConnectVeiculos.Infrastructure.Services.Renave;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.Services
{
    public class RenaveServiceTests
    {
        private const string CpfValido = "52998224725";
        private const string CnpjValido = "11222333000181";

        /// <summary>Guarda a ultima requisicao e responde o que o teste mandar.</summary>
        private sealed class TransporteFalso : IRenaveTransporte
        {
            public HttpRequestMessage? Ultima;
            public string? CorpoEnviado;
            public HttpStatusCode Status = HttpStatusCode.OK;
            public string Resposta = "{}";

            public async Task<HttpResponseMessage> EnviarAsync(HttpRequestMessage request, X509Certificate2? certificado, CancellationToken ct)
            {
                Ultima = request;
                CorpoEnviado = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
                return new HttpResponseMessage(Status) { Content = new StringContent(Resposta, Encoding.UTF8, "application/json") };
            }
        }

        private sealed class ConfigEmMemoria : IConfiguracaoSistemaRepository
        {
            public readonly Dictionary<string, string> Valores = new();
            public Task<string> GetValorAsync(string chave) => Task.FromResult(Valores.TryGetValue(chave, out var v) ? v : null!);
            public Task SetValorAsync(string chave, string valor) { Valores[chave] = valor; return Task.CompletedTask; }
        }

        private sealed class RegistrosEmMemoria : IVeiculoRenaveRepository
        {
            public readonly Dictionary<int, VeiculoRenave> Itens = new();
            public Task<VeiculoRenave?> GetByVeiculoAsync(int veiculoId) => Task.FromResult(Itens.TryGetValue(veiculoId, out var r) ? r : null);
            public Task<IReadOnlyList<VeiculoRenave>> GetByVeiculosAsync(IEnumerable<int> ids)
                => Task.FromResult<IReadOnlyList<VeiculoRenave>>(ids.Where(Itens.ContainsKey).Select(i => Itens[i]).ToList());
            public Task SalvarAsync(VeiculoRenave registro) { Itens[registro.R_VeiId] = registro; return Task.CompletedTask; }
        }

        private readonly TransporteFalso _http = new();
        private readonly ConfigEmMemoria _config = new();
        private readonly RegistrosEmMemoria _registros = new();
        private readonly RenaveService _service;

        public RenaveServiceTests()
        {
            var veiculos = new Mock<IVeiculoRepository>();
            veiculos.Setup(v => v.GetByIdAsync(7)).ReturnsAsync(new Veiculo(
                7, 1, 1, "Toyota", "Corolla", 2020, "ABC-1D23", "9BWZZZ377VT004251",
                "Prata", 45000, 90000m, DateTime.Now, "D", "A", 80000m, veiRenavam: "123456789"));

            var protector = new Mock<ITokenProtector>();
            protector.Setup(p => p.Protect(It.IsAny<string>())).Returns((string s) => "x" + s);
            protector.Setup(p => p.Unprotect(It.IsAny<string>())).Returns((string s) => s[1..]);

            _service = new RenaveService(_http, Options.Create(new RenaveSettings()), _config, _registros,
                veiculos.Object, protector.Object, NullLogger<RenaveService>.Instance);
        }

        private Task ConfigurarAsync(string ambiente = RenaveAmbiente.Homologacao, string? cnpjEstab = null)
            => _service.SalvarConfigAsync(new RenaveConfigInput
            {
                Ambiente = ambiente, OperadorCpf = CpfValido, OperadorNome = "Operador", EmailLoja = "loja@ex.com",
                CnpjEstabelecimento = cnpjEstab
            });

        private static RenaveEntradaInput Entrada() => new()
        {
            TipoCrv = "verde", NumeroCrv = "777227019811", CodigoSegurancaCrv = "77722719811",
            DocumentoVendedor = "529.982.247-25", EmailVendedor = "vend@ex.com",
            DataCompra = new DateTime(2026, 11, 1), ValorCompra = 50000m, QuilometragemHodometro = 45000,
            DataHoraMedicaoHodometro = new DateTime(2026, 11, 1, 10, 0, 0)
        };

        [Fact]
        public async Task Entrada_Monta_O_Corpo_No_Formato_Do_Renave_E_Guarda_O_Estoque()
        {
            await ConfigurarAsync();
            _http.Status = HttpStatusCode.Created;
            _http.Resposta = """{"id": 634005, "estado": "SOLICITADO"}""";

            var situacao = await _service.SolicitarEntradaAsync(7, Entrada());

            _http.Ultima!.RequestUri!.ToString().Should().Be("https://hom.renave.estaleiro.serpro.gov.br/renave-ws/api/solicitacoes-entrada-estoque");
            using var corpo = JsonDocument.Parse(_http.CorpoEnviado!);
            var r = corpo.RootElement;
            r.GetProperty("dataCompra").GetString().Should().Be("2026-11-01");
            r.GetProperty("cpfOperadorResponsavel").GetString().Should().Be(CpfValido);
            r.GetProperty("emailEstabelecimento").GetString().Should().Be("loja@ex.com");
            var v = r.GetProperty("veiculo");
            v.GetProperty("placa").GetString().Should().Be("ABC1D23");
            v.GetProperty("renavam").GetString().Should().Be("00123456789"); // 9 digitos -> 11
            v.GetProperty("tipoCrv").GetString().Should().Be("VERDE");
            // Java LocalDateTime: sem fuso, senao o Renave recusa.
            v.GetProperty("dataHoraMedicaoHodometro").GetString().Should().Be("2026-11-01T10:00:00");
            v.GetProperty("documentoProprietarioAtual").GetString().Should().Be(CpfValido);
            v.GetProperty("tipoDocumentoProprietarioAtual").GetString().Should().Be("CPF");

            situacao.IdEstoque.Should().Be(634005);
            situacao.Estado.Should().Be("SOLICITADO");
            situacao.ProximoPasso.Should().Be("Enviar a assinatura do vendedor (ATPV)");
        }

        [Fact]
        public async Task Recusa_Do_Renave_Vira_Mensagem_Para_A_Tela_E_Fica_No_Veiculo()
        {
            await ConfigurarAsync();
            _registros.Itens[7] = new VeiculoRenave(7);
            _registros.Itens[7].RegistrarEntrada(634005, "CONFIRMADO", DateTime.Today, 50000m);
            _http.Status = (HttpStatusCode)422;
            _http.Resposta = """{"titulo":"Erro de negócio","detalhe":"x","mensagemParaUsuarioFinal":"Código de segurança CRV informado não bate."}""";

            var acao = () => _service.SolicitarSaidaAsync(7, new RenaveSaidaInput
            {
                CodigoSegurancaCrv = "1", DocumentoComprador = CpfValido, CodigoMunicipio = 7005,
                DataVenda = DateTime.Today, ValorVenda = 60000m
            });

            (await acao.Should().ThrowAsync<RenaveException>()).Which.Message.Should().Be("Código de segurança CRV informado não bate.");
            _registros.Itens[7].RnvUltimoErro.Should().Be("Código de segurança CRV informado não bate.");
            _registros.Itens[7].RnvDtSaida.Should().BeNull();
        }

        [Fact]
        public async Task Producao_Sem_Certificado_Nao_Chama_O_Renave()
        {
            await ConfigurarAsync(RenaveAmbiente.Producao);

            var acao = () => _service.TestarConexaoAsync();

            await acao.Should().ThrowAsync<RenaveException>().WithMessage("*certificado A1*");
            _http.Ultima.Should().BeNull();
        }

        [Fact]
        public async Task Integradora_Envia_O_Cnpj_Da_Loja_No_Cabecalho()
        {
            await ConfigurarAsync(cnpjEstab: CnpjValido);
            _http.Resposta = """{"cnpj":"85106037000585","nome":"Estabelecimento padrão de teste"}""";

            await _service.TestarConexaoAsync();

            _http.Ultima!.Headers.GetValues("CNPJ-Estabelecimento").Should().ContainSingle().Which.Should().Be(CnpjValido);
        }

        [Fact]
        public async Task Nota_De_Venda_Antes_Da_Saida_E_Recusada_Sem_Chamar_O_Renave()
        {
            await ConfigurarAsync();
            _registros.Itens[7] = new VeiculoRenave(7);
            _registros.Itens[7].RegistrarEntrada(634005, "CONFIRMADO", DateTime.Today, 50000m);

            var acao = () => _service.EnviarNotaFiscalAsync(7, "VENDA", ChaveValida());

            await acao.Should().ThrowAsync<RenaveException>().WithMessage("*saída*");
            _http.Ultima.Should().BeNull();
        }

        [Fact]
        public void Erro_Interno_Do_Renave_Nao_Mostra_O_Texto_Tecnico()
        {
            var msg = RenaveService.MensagemDeErro(HttpStatusCode.InternalServerError,
                """{"titulo":"Erro Interno","detalhe":"NullPointerException","logIdRastreabilidade":"abc123"}""");

            msg.Should().Contain("instabilidade").And.Contain("abc123").And.NotContain("NullPointer");
        }

        [Fact]
        public void Proximo_Passo_Segue_A_Ordem_Do_Manual()
        {
            var r = new VeiculoRenave(1);
            RenaveService.ParaSituacao(1, r).ProximoPasso.Should().Be("Registrar a entrada no Renave");
            r.RegistrarEntrada(1, "SOLICITADO", DateTime.Today, 1m);
            RenaveService.ParaSituacao(1, r).ProximoPasso.Should().Be("Enviar a assinatura do vendedor (ATPV)");
            r.RegistrarAssinaturaVendedor();
            RenaveService.ParaSituacao(1, r).ProximoPasso.Should().Be("Informar a chave da NF-e de compra");
            r.RegistrarNotaEntrada(ChaveValida());
            RenaveService.ParaSituacao(1, r).ProximoPasso.Should().Be("Aguardando o Detran confirmar a entrada");
            r.AtualizarEstado("CONFIRMADO");
            RenaveService.ParaSituacao(1, r).ProximoPasso.Should().StartWith("Em estoque no Renave");
            r.RegistrarSaida("CONFIRMADO", DateTime.Today, 2m, CpfValido, "Fulano");
            RenaveService.ParaSituacao(1, r).ProximoPasso.Should().Be("Informar a chave da NF-e de venda");
            r.RegistrarNotaSaida(ChaveValida());
            RenaveService.ParaSituacao(1, r).ProximoPasso.Should().Be("Concluído");
        }

        [Theory]
        [InlineData("52998224725", true)]
        [InlineData("52998224724", false)]
        [InlineData("11111111111", false)]
        [InlineData("11222333000181", true)]
        [InlineData("11222333000180", false)]
        public void Documento_Confere_Os_Digitos(string doc, bool valido)
            => RenaveService.DocumentoValido(doc).Should().Be(valido);

        [Fact]
        public void Chave_Da_NFe_Confere_O_Digito()
        {
            RenaveService.ChaveNfeValida(ChaveValida()).Should().BeTrue();
            RenaveService.ChaveNfeValida(new string('3', 44)).Should().BeFalse();
            RenaveService.ChaveNfeValida("123").Should().BeFalse();
        }

        // SP, 11/2026, CNPJ 11222333000181, modelo 55, serie 1, numero 123; DV 4 (modulo 11).
        private static string ChaveValida() => "35261111222333000181550010000001231000001234";
    }
}
