using System.Security.Cryptography.X509Certificates;
using ConnectVeiculos.Core.Entities.NotasFiscais;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.NotasFiscais;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Core.NotasFiscais;
using ConnectVeiculos.Core.Renave;
using ConnectVeiculos.Infrastructure.Services.NotasFiscais;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Unimake.Business.DFe.Servicos;
using Unimake.Business.DFe.Xml.NFe;
using Xunit;

namespace ConnectVeiculos.Tests.NotasFiscais
{
    public class NfeServiceTests
    {
        private sealed class Config : IConfiguracaoSistemaRepository
        {
            public readonly Dictionary<string, string> V = new();
            public Task<string> GetValorAsync(string c) => Task.FromResult(V.TryGetValue(c, out var v) ? v : null!);
            public Task SetValorAsync(string c, string v) { V[c] = v; return Task.CompletedTask; }
        }

        private sealed class Notas : INotaFiscalRepository
        {
            public readonly List<NotaFiscal> Itens = new();
            public Task<NotaFiscal?> GetByIdAsync(int id) => Task.FromResult(Itens.FirstOrDefault(n => n.NfId == id));
            public Task<IReadOnlyList<NotaFiscal>> GetByVeiculoAsync(int v) => Task.FromResult<IReadOnlyList<NotaFiscal>>(Itens.Where(n => n.R_VeiId == v).ToList());
            public Task<bool> NumeroEmUsoAsync(string a, int s, int n) => Task.FromResult(Itens.Any(x => x.NfAmbiente == a && x.NfSerie == s && x.NfNumero == n));
            public Task SalvarAsync(NotaFiscal nota)
            {
                if (nota.NfId == 0)
                {
                    typeof(NotaFiscal).GetProperty(nameof(NotaFiscal.NfId))!.SetValue(nota, Itens.Count + 1);
                    Itens.Add(nota);
                }
                return Task.CompletedTask;
            }
        }

        private sealed class SefazFalsa : INfeSefaz
        {
            public readonly Queue<NfeRetornoAutorizacao> Respostas = new();
            public readonly List<EnviNFe> Enviadas = new();
            public NfeRetornoEvento Cancelamento = new() { Registrado = true, CStat = 135, Protocolo = "135260000000001", Xml = "<procEventoNFe/>" };

            public NfeRetornoAutorizacao Autorizar(EnviNFe xml, X509Certificate2 certificado)
            {
                Enviadas.Add(xml);
                return Respostas.Count > 0 ? Respostas.Dequeue() : Autorizada();
            }

            public NfeRetornoEvento Cancelar(string chave, string protocolo, string justificativa, string cnpj, UFBrasil uf, bool homologacao, X509Certificate2 certificado)
                => Cancelamento;

            public NfeStatusSefaz Status(UFBrasil uf, bool homologacao, X509Certificate2 certificado)
                => new() { CStat = 107, Motivo = "Servico em Operacao" };
        }

        private static NfeRetornoAutorizacao Autorizada() => new()
        {
            Autorizada = true, CStat = 100, Motivo = "Autorizado o uso da NF-e", Protocolo = "135260000000123",
            DataAutorizacao = DateTime.UtcNow, XmlProc = "<nfeProc/>"
        };

        private readonly Config _config = new();
        private readonly Notas _notas = new();
        private readonly SefazFalsa _sefaz = new();
        private readonly Mock<IRenaveService> _renave = new();
        private readonly NfeService _service;

        public NfeServiceTests()
        {
            var veiculos = new Mock<IVeiculoRepository>();
            veiculos.Setup(v => v.GetByIdAsync(42)).ReturnsAsync(NfeVeiculoBuilderTests.Carro());

            var cert = new Mock<ICertificadoDigitalService>();
            cert.Setup(c => c.ObterInfoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new CertificadoDigitalInfo
            {
                Cadastrado = true, Cnpj = "11222333000181", Validade = DateTime.UtcNow.AddYears(1)
            });
            cert.Setup(c => c.CarregarAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => NfeVeiculoBuilderTests.CertificadoDeTeste());

            _renave.Setup(r => r.ObterSituacaoAsync(42, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RenaveSituacao { VeiculoId = 42 });

            var tenant = new Mock<ITenantContext>();
            tenant.SetupGet(t => t.TenantSlug).Returns("loja-teste");

            _service = new NfeService(_config, _notas, veiculos.Object, cert.Object, _sefaz, _renave.Object, tenant.Object,
                new HttpClient(), NullLogger<NfeService>.Instance);
        }

        private Task ConfigurarAsync(int proximoNumero = 10)
        {
            var cfg = NfeVeiculoBuilderTests.Config();
            cfg.ProximoNumero = proximoNumero;
            return _service.SalvarConfigAsync(cfg);
        }

        [Fact]
        public async Task Sem_Configuracao_Lista_O_Que_Falta_E_Nao_Emite()
        {
            var cfg = await _service.ObterConfigAsync();
            cfg.Pronto.Should().BeFalse();
            cfg.Pendencias.Should().Contain("CNPJ da loja").And.Contain("Inscrição estadual");

            var acao = () => _service.EmitirAsync(42, NfeVeiculoBuilderTests.Venda());
            await acao.Should().ThrowAsync<NfeException>().WithMessage("*Complete a configuração*");
            _sefaz.Enviadas.Should().BeEmpty();
        }

        [Fact]
        public async Task Autorizada_Guarda_Protocolo_E_Avanca_A_Numeracao()
        {
            await ConfigurarAsync(proximoNumero: 10);

            var nota = await _service.EmitirAsync(42, NfeVeiculoBuilderTests.Venda());

            nota.Status.Should().Be(NotaFiscal.StatusAutorizada);
            nota.Numero.Should().Be(10);
            nota.Protocolo.Should().Be("135260000000123");
            nota.Chave.Should().HaveLength(44);
            (await _service.ObterConfigAsync()).ProximoNumero.Should().Be(11);
            var ide = _sefaz.Enviadas.Single().NFe[0].InfNFeField.Ide;
            ide.CNF.Should().NotBe(ide.NNF.ToString("D8"));
        }

        [Fact]
        public async Task Rejeitada_Mantem_O_Numero_E_O_Reenvio_Usa_O_Mesmo()
        {
            await ConfigurarAsync(proximoNumero: 5);
            _sefaz.Respostas.Enqueue(new NfeRetornoAutorizacao { CStat = 539, Motivo = "Rejeicao: Duplicidade" });

            var primeira = () => _service.EmitirAsync(42, NfeVeiculoBuilderTests.Venda());
            await primeira.Should().ThrowAsync<NfeException>().WithMessage("*539*");
            _notas.Itens.Single().NfStatus.Should().Be(NotaFiscal.StatusRejeitada);

            // Emitir de novo para o mesmo veiculo reaproveita a nota rejeitada.
            var nota = await _service.EmitirAsync(42, NfeVeiculoBuilderTests.Venda());

            nota.Status.Should().Be(NotaFiscal.StatusAutorizada);
            nota.Numero.Should().Be(5);
            _notas.Itens.Should().ContainSingle();
            (await _service.ObterConfigAsync()).ProximoNumero.Should().Be(6);
        }

        [Fact]
        public async Task Nao_Emite_Segunda_Nota_De_Venda_Autorizada()
        {
            await ConfigurarAsync();
            await _service.EmitirAsync(42, NfeVeiculoBuilderTests.Venda());

            var acao = () => _service.EmitirAsync(42, NfeVeiculoBuilderTests.Venda());

            await acao.Should().ThrowAsync<NfeException>().WithMessage("*já tem NF-e de venda autorizada*");
        }

        [Fact]
        public async Task Cancelamento_Exige_Justificativa_E_Registra_O_Protocolo()
        {
            await ConfigurarAsync();
            var nota = await _service.EmitirAsync(42, NfeVeiculoBuilderTests.Venda());

            var curta = () => _service.CancelarAsync(nota.Id, "erro");
            await curta.Should().ThrowAsync<NfeException>().WithMessage("*15*");

            var cancelada = await _service.CancelarAsync(nota.Id, "Venda desfeita a pedido do cliente");
            cancelada.Status.Should().Be(NotaFiscal.StatusCancelada);
            cancelada.DataCancelamento.Should().NotBeNull();
        }

        [Fact]
        public async Task Nota_De_Venda_Autorizada_Vai_Para_O_Renave_Se_A_Saida_Ja_Foi_Registrada()
        {
            await ConfigurarAsync();
            _renave.Setup(r => r.ObterSituacaoAsync(42, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RenaveSituacao { VeiculoId = 42, IdEstoque = 1, DataSaida = DateTime.Today });

            var nota = await _service.EmitirAsync(42, NfeVeiculoBuilderTests.Venda());

            _renave.Verify(r => r.EnviarNotaFiscalAsync(42, "VENDA", nota.Chave!, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Recusa_Do_Renave_Nao_Desfaz_A_Nota_Autorizada()
        {
            await ConfigurarAsync();
            _renave.Setup(r => r.ObterSituacaoAsync(42, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RenaveSituacao { VeiculoId = 42, IdEstoque = 1, DataSaida = DateTime.Today });
            _renave.Setup(r => r.EnviarNotaFiscalAsync(42, "VENDA", It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RenaveException("Estoque cancelado"));

            var nota = await _service.EmitirAsync(42, NfeVeiculoBuilderTests.Venda());

            nota.Status.Should().Be(NotaFiscal.StatusAutorizada);
            nota.Motivo.Should().Contain("Renave recusou").And.Contain("Estoque cancelado");
        }
    }
}
