using System.Security.Cryptography.X509Certificates;
using System.Net;
using ConnectVeiculos.Core.Entities.NotasFiscais;
using ConnectVeiculos.Core.Entities.Renave;
using ConnectVeiculos.Core.Entities.Vendas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Renave;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas;
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
            public readonly List<NotaFiscalInutilizacao> Inutilizacoes = new();
            public Task<bool> NumeroEmUsoAsync(string a, int s, int n) => Task.FromResult(
                Itens.Any(x => x.NfAmbiente == a && x.NfSerie == s && x.NfNumero == n)
                || Inutilizacoes.Any(i => i.NfiAmbiente == a && i.NfiSerie == s && i.Contem(n)));
            public Task<IReadOnlyList<NotaFiscal>> GetNoIntervaloAsync(string a, int s, int ini, int fim)
                => Task.FromResult<IReadOnlyList<NotaFiscal>>(Itens.Where(x => x.NfAmbiente == a && x.NfSerie == s && x.NfNumero >= ini && x.NfNumero <= fim).ToList());
            public Task<IReadOnlyList<NotaFiscalInutilizacao>> ListarInutilizacoesAsync() => Task.FromResult<IReadOnlyList<NotaFiscalInutilizacao>>(Inutilizacoes.ToList());
            public Task<NotaFiscalInutilizacao?> ObterInutilizacaoAsync(int id) => Task.FromResult(Inutilizacoes.FirstOrDefault(i => i.NfiId == id));
            public Task SalvarInutilizacaoAsync(NotaFiscalInutilizacao i)
            {
                typeof(NotaFiscalInutilizacao).GetProperty(nameof(NotaFiscalInutilizacao.NfiId))!.SetValue(i, Inutilizacoes.Count + 1);
                Inutilizacoes.Add(i);
                return Task.CompletedTask;
            }
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

            public readonly List<(int Sequencia, string Texto)> Correcoes = new();
            public NfeRetornoEvento? RespostaCorrecao;
            public NfeRetornoEvento CartaCorrecao(string chave, int sequencia, string correcao, string cnpj, UFBrasil uf, bool homologacao, X509Certificate2 certificado)
            {
                Correcoes.Add((sequencia, correcao));
                return RespostaCorrecao ?? new() { Registrado = true, CStat = 135, Protocolo = "13526000000" + sequencia, Xml = "<procEventoNFe/>" };
            }

            public readonly List<(int Serie, int Ini, int Fim)> Inutilizadas = new();
            public NfeRetornoEvento? RespostaInutilizacao;
            public NfeRetornoEvento Inutilizar(int serie, int inicial, int final, string justificativa, string cnpj, UFBrasil uf, bool homologacao, X509Certificate2 certificado)
            {
                Inutilizadas.Add((serie, inicial, final));
                return RespostaInutilizacao ?? new() { Registrado = true, CStat = 102, Protocolo = "135260000009", Xml = "<procInutNFe/>" };
            }
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
        private readonly Mock<IVendaRepository> _vendas = new();
        private readonly Mock<IVeiculoRenaveRepository> _registrosRenave = new();

        /// <summary>IBGE falso: devolve sempre os mesmos municipios.</summary>
        private sealed class IbgeFalso : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
                => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[{\"id\":3545209,\"nome\":\"Salto\"},{\"id\":3550308,\"nome\":\"São Paulo\"}]")
                });
        }
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

            _service = new NfeService(_config, _notas, veiculos.Object, _vendas.Object, _registrosRenave.Object,
                cert.Object, _sefaz, _renave.Object, tenant.Object,
                new HttpClient(new IbgeFalso()), NullLogger<NfeService>.Instance);
        }

        private Task ConfigurarAsync(int proximoNumero = 10)
        {
            var cfg = NfeVeiculoBuilderTests.Config();
            cfg.ProximoNumero = proximoNumero;
            return _service.SalvarConfigAsync(cfg);
        }

        [Fact]
        public async Task Sugestao_Da_Venda_Traz_Comprador_Endereco_E_Municipio()
        {
            await ConfigurarAsync();
            _vendas.Setup(v => v.GetByVeiculoIdAsync(42)).ReturnsAsync(new[]
            {
                new Venda(1, 42, 1, DateTime.Today, "Toyota", "Corolla", 2020, "9BRBL3HE0L0123456", 87000m, 0, 0,
                    "Maria Souza", "529.982.247-25", "11999990000", "maria@exemplo.com",
                    "Rua das Flores, 120, Centro, Salto, SP, 13320-000", "PIX")
            });

            var s = await _service.SugerirAsync(42, "SAIDA");

            s.Origem.Should().Be("venda");
            s.Dados.Documento.Should().Be("52998224725");
            s.Dados.Nome.Should().Be("Maria Souza");
            s.Dados.Valor.Should().Be(87000m);
            s.Dados.MeioPagamento.Should().Be("17");
            s.Dados.Endereco.Logradouro.Should().Be("Rua das Flores");
            s.Dados.Endereco.Numero.Should().Be("120");
            s.Dados.Endereco.Bairro.Should().Be("Centro");
            s.Dados.Endereco.Cep.Should().Be("13320000");
            s.Dados.Endereco.Uf.Should().Be("SP");
            s.Dados.Endereco.CodigoMunicipio.Should().Be(3545209);
        }

        [Fact]
        public async Task Sugestao_Ignora_Venda_Estornada_E_Usa_O_Renave()
        {
            await ConfigurarAsync();
            var estornada = new Venda(1, 42, 1, DateTime.Today, "Toyota", "Corolla", 2020, "9BRBL3HE0L0123456", 87000m, 0, 0,
                "Outra Pessoa", "52998224725");
            estornada.Estornar();
            _vendas.Setup(v => v.GetByVeiculoIdAsync(42)).ReturnsAsync(new[] { estornada });
            var registro = new VeiculoRenave(42);
            registro.RegistrarEntrada(1, "CONFIRMADO", DateTime.Today, 50000m, "11144477735", "vendedor@exemplo.com");
            registro.RegistrarSaida("CONFIRMADO", DateTime.Today, 88000m, "52998224725", "Maria Souza", "maria@exemplo.com");
            _registrosRenave.Setup(r => r.GetByVeiculoAsync(42)).ReturnsAsync(registro);

            var venda = await _service.SugerirAsync(42, "SAIDA");
            venda.Origem.Should().Be("renave");
            venda.Dados.Nome.Should().Be("Maria Souza");
            venda.Dados.Email.Should().Be("maria@exemplo.com");
            venda.Dados.Valor.Should().Be(88000m);

            var compra = await _service.SugerirAsync(42, "ENTRADA");
            compra.Origem.Should().Be("renave");
            compra.Dados.Documento.Should().Be("11144477735");
            compra.Dados.Email.Should().Be("vendedor@exemplo.com");
            compra.Dados.Valor.Should().Be(50000m);
        }

        [Fact]
        public async Task Sugestao_Prefere_A_Nota_Anterior_Do_Veiculo()
        {
            await ConfigurarAsync();
            _sefaz.Respostas.Enqueue(new NfeRetornoAutorizacao { Autorizada = false, CStat = 539, Motivo = "Duplicidade" });
            var dados = NfeVeiculoBuilderTests.Venda();
            await FluentActions.Awaiting(() => _service.EmitirAsync(42, dados)).Should().ThrowAsync<NfeException>();

            var s = await _service.SugerirAsync(42, "SAIDA");

            s.Origem.Should().Be("nota");
            s.Dados.Documento.Should().Be(dados.Documento);
            s.Dados.Endereco.CodigoMunicipio.Should().Be(dados.Endereco.CodigoMunicipio);
        }

        [Fact]
        public async Task Sem_Dados_A_Sugestao_Vem_So_Com_Valor_E_UF()
        {
            await ConfigurarAsync();
            _vendas.Setup(v => v.GetByVeiculoIdAsync(42)).ReturnsAsync(Array.Empty<Venda>());

            var s = await _service.SugerirAsync(42, "ENTRADA");

            s.Origem.Should().BeNull();
            s.Dados.Documento.Should().BeEmpty();
            s.Dados.Valor.Should().Be(NfeVeiculoBuilderTests.Carro().VeiPrecoCompra);
            s.Dados.Endereco.Uf.Should().Be(NfeVeiculoBuilderTests.Config().Uf);
        }

        [Theory]
        [InlineData("Rua A, 10, Centro, Salto, SP, 13320-000", "Rua A", "10", "Centro", "Salto", "SP", "13320000")]
        [InlineData("Rua A, Centro, Salto, SP, 13320000", "Rua A", null, "Centro", "Salto", "SP", "13320000")]
        [InlineData("Rua A, S/N, Salto, MG", "Rua A", "S/N", null, "Salto", "MG", null)]
        [InlineData("Av. Brasil, 500, Jardim Paulista, Bloco B, São Paulo, SP", "Av. Brasil", "500", "Jardim Paulista, Bloco B", "São Paulo", "SP", null)]
        public void Le_O_Endereco_Gravado_Pela_Tela_De_Vendas(string texto, string rua, string? numero, string? bairro,
            string cidade, string uf, string? cep)
        {
            var e = NfeService.LerEnderecoDaVenda(texto, "RJ");
            e.Logradouro.Should().Be(rua);
            e.Numero.Should().Be(numero);
            e.Bairro.Should().Be(bairro);
            e.Municipio.Should().Be(cidade);
            e.Uf.Should().Be(uf);
            e.Cep.Should().Be(cep);
        }

        // ---------------- Carta de correcao ----------------

        [Fact]
        public async Task Carta_De_Correcao_Numera_Em_Sequencia_E_Fica_A_Ultima()
        {
            await ConfigurarAsync();
            var nota = await _service.EmitirAsync(42, NfeVeiculoBuilderTests.Venda());
            nota.PodeCorrigir.Should().BeTrue();

            await _service.CorrigirAsync(nota.Id, "Cor do veiculo: onde se le Prata leia-se Cinza.");
            var r = await _service.CorrigirAsync(nota.Id, "  Cor do veículo:   Cinza. Complemento do endereço: casa 2.  ");

            _sefaz.Correcoes.Select(c => c.Sequencia).Should().Equal(1, 2);
            _sefaz.Correcoes[1].Texto.Should().Be("Cor do veiculo: Cinza. Complemento do endereco: casa 2.");
            r.CartaCorrecaoSequencia.Should().Be(2);
            r.CartaCorrecaoTexto.Should().StartWith("Cor do veiculo: Cinza");
            (await _service.ObterXmlCartaCorrecaoAsync(nota.Id)).NomeArquivo.Should().EndWith("-cce-2.xml");
        }

        [Fact]
        public async Task Carta_De_Correcao_Recusa_Texto_Curto_Nota_Cancelada_E_Passou_De_20()
        {
            await ConfigurarAsync();
            var nota = await _service.EmitirAsync(42, NfeVeiculoBuilderTests.Venda());

            await FluentActions.Awaiting(() => _service.CorrigirAsync(nota.Id, "curto"))
                .Should().ThrowAsync<NfeException>().WithMessage("*15 e 1000*");

            var entidade = _notas.Itens.Single(n => n.NfId == nota.Id);
            entidade.RegistrarCartaCorrecao(20, "x", "p", null, "<x/>");
            await FluentActions.Awaiting(() => _service.CorrigirAsync(nota.Id, "Texto de correcao suficiente."))
                .Should().ThrowAsync<NfeException>().WithMessage("*20*");

            await _service.CancelarAsync(nota.Id, "Venda desfeita a pedido do cliente");
            await FluentActions.Awaiting(() => _service.CorrigirAsync(nota.Id, "Texto de correcao suficiente."))
                .Should().ThrowAsync<NfeException>().WithMessage("*autorizada*");
            _sefaz.Correcoes.Should().BeEmpty();
        }

        [Fact]
        public async Task Carta_Recusada_Pela_Sefaz_Nao_Muda_A_Nota()
        {
            await ConfigurarAsync();
            var nota = await _service.EmitirAsync(42, NfeVeiculoBuilderTests.Venda());
            _sefaz.RespostaCorrecao = new NfeRetornoEvento { Registrado = false, CStat = 573, Motivo = "Duplicidade de evento" };

            await FluentActions.Awaiting(() => _service.CorrigirAsync(nota.Id, "Texto de correcao suficiente."))
                .Should().ThrowAsync<NfeException>().WithMessage("*573*Duplicidade*");
            _notas.Itens.Single().NfCceSequencia.Should().Be(0);
        }

        // ---------------- Inutilizacao ----------------

        [Fact]
        public async Task Inutilizar_Marca_A_Rejeitada_E_A_Numeracao_Pula_A_Faixa()
        {
            await ConfigurarAsync(proximoNumero: 10);
            _sefaz.Respostas.Enqueue(new NfeRetornoAutorizacao { Autorizada = false, CStat = 539, Motivo = "Duplicidade" });
            await FluentActions.Awaiting(() => _service.EmitirAsync(42, NfeVeiculoBuilderTests.Venda())).Should().ThrowAsync<NfeException>();
            var rejeitada = _notas.Itens.Single();
            rejeitada.NfNumero.Should().Be(10);

            var r = await _service.InutilizarAsync(new NfeInutilizacaoInput
            {
                Serie = rejeitada.NfSerie, NumeroInicial = 10, NumeroFinal = 12, Justificativa = "Numeração pulada por falha no envio"
            });

            r.Protocolo.Should().Be("135260000009");
            _sefaz.Inutilizadas.Should().ContainSingle().Which.Should().Be((rejeitada.NfSerie, 10, 12));
            rejeitada.NfStatus.Should().Be(NotaFiscal.StatusInutilizada);
            rejeitada.PodeReenviar.Should().BeFalse();
            (await _service.ListarInutilizacoesAsync()).Should().ContainSingle();

            // A proxima nota (outro veiculo) nao pode cair em 11 nem 12.
            (await _notas.NumeroEmUsoAsync("homologacao", rejeitada.NfSerie, 12)).Should().BeTrue();
            (await _notas.NumeroEmUsoAsync("homologacao", rejeitada.NfSerie, 13)).Should().BeFalse();
        }

        [Fact]
        public async Task Inutilizar_Recusa_Nota_Autorizada_Na_Faixa_E_Faixa_Repetida()
        {
            await ConfigurarAsync(proximoNumero: 10);
            var nota = await _service.EmitirAsync(42, NfeVeiculoBuilderTests.Venda());
            var serie = _notas.Itens.Single().NfSerie;

            await FluentActions.Awaiting(() => _service.InutilizarAsync(new NfeInutilizacaoInput
            {
                Serie = serie, NumeroInicial = 9, NumeroFinal = 11, Justificativa = "Numeração pulada por falha no envio"
            })).Should().ThrowAsync<NfeException>().WithMessage("*10*autorizada*cancela*");

            await _service.InutilizarAsync(new NfeInutilizacaoInput { Serie = serie, NumeroInicial = 20, NumeroFinal = 25, Justificativa = "Numeração pulada por falha no envio" });
            await FluentActions.Awaiting(() => _service.InutilizarAsync(new NfeInutilizacaoInput
            {
                Serie = serie, NumeroInicial = 25, NumeroFinal = 30, Justificativa = "Numeração pulada por falha no envio"
            })).Should().ThrowAsync<NfeException>().WithMessage("*já foi inutilizada*20 a 25*");

            await FluentActions.Awaiting(() => _service.InutilizarAsync(new NfeInutilizacaoInput
            {
                Serie = serie, NumeroInicial = 40, NumeroFinal = 40, Justificativa = "curta"
            })).Should().ThrowAsync<NfeException>().WithMessage("*15 e 255*");
            _sefaz.Inutilizadas.Should().ContainSingle();
        }

        [Fact]
        public void Xml_Da_Carta_De_Correcao_E_Da_Inutilizacao_Passam_No_Schema()
        {
            using var cert = NfeVeiculoBuilderTests.CertificadoDeTeste();
            var config = new Unimake.Business.DFe.Servicos.Configuracao
            {
                TipoDFe = Unimake.Business.DFe.Servicos.TipoDFe.NFe, CertificadoDigital = cert
            };
            var chave = "35261111222333000181550010000000101234567890";

            FluentActions.Invoking(() => new Unimake.Business.DFe.Servicos.NFe.RecepcaoEvento(
                NfeSefazUnimake.MontarCartaCorrecao(chave, 1, "Cor do veiculo: onde se le Prata leia-se Cinza.", "11222333000181", UFBrasil.SP, true), config))
                .Should().NotThrow();
            FluentActions.Invoking(() => new Unimake.Business.DFe.Servicos.NFe.Inutilizacao(
                NfeSefazUnimake.MontarInutilizacao(1, 10, 12, "Numeracao pulada por falha no envio", "11222333000181", UFBrasil.SP, true), config))
                .Should().NotThrow();
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
