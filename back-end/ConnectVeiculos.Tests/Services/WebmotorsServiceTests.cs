using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using ConnectVeiculos.Core.Entities.Integracoes;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Entities.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Integracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Core.Webmotors;
using ConnectVeiculos.Infrastructure.Services.Webmotors;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.Services
{
    public class WebmotorsSugestaoTests
    {
        private static readonly List<WebmotorsItem> Marcas = new()
        {
            new() { Codigo = 30, Nome = "VOLKSWAGEN" }, new() { Codigo = 2, Nome = "CHEVROLET" },
            new() { Codigo = 26, Nome = "MITSUBISHI" }, new() { Codigo = 40, Nome = "MERCEDES-BENZ" }, new() { Codigo = 5, Nome = "FIAT" }
        };

        [Theory]
        [InlineData("VW - VolksWagen", 30)]
        [InlineData("GM - Chevrolet", 2)]
        [InlineData("Mitsubishi", 26)]
        [InlineData("Mercedes-Benz", 40)]
        [InlineData("FIAT", 5)]
        public void Marca_Da_Fipe(string fipe, long esperado) =>
            WebmotorsSugestao.Marca(fipe, Marcas)!.Codigo.Should().Be(esperado);

        [Fact]
        public void Modelo_E_O_Maior_Inicio_Do_Nome()
        {
            var modelos = new List<WebmotorsItem> { new() { Codigo = 729, Nome = "L200" }, new() { Codigo = 3284, Nome = "L200 OUTDOOR" }, new() { Codigo = 1, Nome = "L" } };
            WebmotorsSugestao.Modelo("L200 Outdoor GLS 2.5 4x4 CD TDI Dies.", modelos)!.Codigo.Should().Be(3284);
            WebmotorsSugestao.Modelo("L200 Triton 3.2 CD", modelos)!.Codigo.Should().Be(729);
            WebmotorsSugestao.Modelo("Pajero", modelos).Should().BeNull();
        }

        [Fact]
        public void Versao_Do_Ano_Com_Mais_Palavras_Em_Comum()
        {
            var gol = new WebmotorsItem { Codigo = 1, Nome = "GOL" };
            var versoes = new List<WebmotorsVersao>
            {
                new() { Codigo = 10, Nome = "1.0 MI 8V FLEX 4P MANUAL G.V", Anos = new() { 2010, 2011, 2012 } },
                new() { Codigo = 11, Nome = "1.6 MI POWER 8V FLEX 4P MANUAL", Anos = new() { 2012 } },
                new() { Codigo = 12, Nome = "1.0 MI 8V FLEX 4P MANUAL G.IV", Anos = new() { 2005, 2006 } }
            };
            WebmotorsSugestao.Versao("GOL 1.0 Mi Total Flex 8V 4p", gol, 2012, versoes)!.Codigo.Should().Be(10);
            WebmotorsSugestao.Versao("GOL 1.6 Mi Power Total Flex 8V 4p", gol, 2012, versoes)!.Codigo.Should().Be(11);
        }

        private static readonly List<WebmotorsItem> Combustiveis = new()
        {
            new() { Codigo = 21205, Nome = "Gasolina e álcool" }, new() { Codigo = 21201, Nome = "Gasolina" },
            new() { Codigo = 21203, Nome = "Diesel" }, new() { Codigo = 21210, Nome = "Gasolina e elétrico" }
        };

        private static readonly List<WebmotorsItem> Cambios = new()
        {
            new() { Codigo = 23001, Nome = "Manual" }, new() { Codigo = 23003, Nome = "Automática" },
            new() { Codigo = 23004, Nome = "CVT" }, new() { Codigo = 2020041, Nome = "Automatizada" }
        };

        [Theory]
        [InlineData("GOL 1.0 Mi Total Flex 8V 4p", 21205, 23001L, 4)]
        [InlineData("1.6 GLX 16V GASOLINA 4P AUTOMÁTICO", 21201, 23003L, 4)]
        [InlineData("HB20 1.0 Comfort Flex 12V Aut. 5p", 21205, 23003L, 4)]
        [InlineData("L200 2.4 DIESEL CD 4X4 MANUAL", 21203, 23001L, null)]
        [InlineData("COROLLA 1.8 HYBRID CVT", 21210, 23004L, null)]
        [InlineData("UP 1.0 TSI 3P FLEX AUTOMATIZADO", 21205, 2020041L, 3)]
        public void Combustivel_Cambio_E_Portas(string texto, long combustivel, long? cambio, int? portas)
        {
            WebmotorsSugestao.Combustivel(texto.Replace("HYBRID", "HIBRIDO"), Combustiveis)!.Codigo.Should().Be(combustivel);
            WebmotorsSugestao.Cambio(texto, Cambios)?.Codigo.Should().Be(cambio);
            WebmotorsSugestao.Portas(texto).Should().Be(portas);
        }

        [Fact]
        public void Cor_E_Opcionais()
        {
            var cores = new List<WebmotorsItem> { new() { Codigo = 30409, Nome = "Prata" }, new() { Codigo = 30410, Nome = "Preto" } };
            WebmotorsSugestao.Cor("Prata Metálico", cores)!.Codigo.Should().Be(30409);
            WebmotorsSugestao.Cor("preto", cores)!.Codigo.Should().Be(30410);
            WebmotorsSugestao.Cor("Vinho", cores).Should().BeNull();

            var opcionais = new List<WebmotorsItem> { new() { Codigo = 5, Nome = "Alarme" }, new() { Codigo = 6, Nome = "Ar condicionado" } };
            WebmotorsSugestao.Opcionais("Ar Condicionado,Alarme,Teto solar", opcionais).Should().BeEquivalentTo(new[] { 5L, 6L });
        }
    }

    public class WebmotorsServiceTests
    {
        public WebmotorsServiceTests() => WebmotorsService.EsperasFotoNova = new[] { TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero };

        private sealed class Config : IConfiguracaoSistemaRepository
        {
            public readonly Dictionary<string, string> V = new();
            public Task<string> GetValorAsync(string c) => Task.FromResult(V.TryGetValue(c, out var v) ? v : null!);
            public Task SetValorAsync(string c, string v) { V[c] = v; return Task.CompletedTask; }
        }

        private sealed class Cifra : ITokenProtector
        {
            public string Protect(string s) => "cifrado:" + s;
            public string Unprotect(string s) => s.StartsWith("cifrado:") ? s[8..] : throw new InvalidOperationException();
        }

        private sealed class Anuncios : IVeiculoWebmotorsRepository
        {
            public readonly List<VeiculoWebmotors> Itens = new();
            public Task<VeiculoWebmotors?> GetByVeiculoIdAsync(int id) => Task.FromResult(Itens.FirstOrDefault(a => a.R_VeiId == id));
            public Task<IReadOnlyList<VeiculoWebmotors>> ListarPublicadosAsync() => Task.FromResult<IReadOnlyList<VeiculoWebmotors>>(Itens);
            public Task SalvarAsync(VeiculoWebmotors a) { if (!Itens.Contains(a)) Itens.Add(a); return Task.CompletedTask; }
        }

        /// <summary>Webmotors falsa: responde por operacao (SOAPAction) e grava o que recebeu.</summary>
        private sealed class Falsa : HttpMessageHandler
        {
            public readonly List<(string Operacao, string Corpo)> Chamadas = new();
            public readonly Dictionary<string, Queue<string>> Respostas = new();
            public int Logins;

            public void Responder(string operacao, params string[] resultados)
            {
                if (!Respostas.TryGetValue(operacao, out var fila)) Respostas[operacao] = fila = new();
                foreach (var r in resultados) fila.Enqueue(r);
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
            {
                if (req.Method == HttpMethod.Get)
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }) };

                var op = req.Headers.GetValues("SOAPAction").Single().Trim('"').Split('/').Last();
                var corpo = await req.Content!.ReadAsStringAsync(ct);
                Chamadas.Add((op, corpo));
                if (op == "autenticar")
                {
                    Logins++;
                    return Xml(op, $"<HashAutenticacao>hash-{Logins}-xxxxxxxx</HashAutenticacao><CodigoRetorno>500</CodigoRetorno>");
                }
                var fila = Respostas.TryGetValue(op, out var f) ? f : null;
                var resultado = fila == null || fila.Count == 0 ? "<CodigoRetorno>500</CodigoRetorno>" : fila.Count > 1 ? fila.Dequeue() : fila.Peek();
                return Xml(op, resultado);
            }

            private static HttpResponseMessage Xml(string op, string resultado) => new(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?><soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body>" +
                    $"<{op}Response xmlns=\"www.webmotors.com.br/x\"><{op}Result>{resultado}</{op}Result></{op}Response></soap:Body></soap:Envelope>",
                    Encoding.UTF8, "text/xml")
            };

            public IEnumerable<string> Operacoes => Chamadas.Select(c => c.Operacao);
        }

        private readonly Falsa _wm = new();
        private readonly Config _config = new();
        private readonly Anuncios _anuncios = new();
        private Veiculo _veiculo = NovoVeiculo("D");
        private List<VeiculoImagem> _fotos = new() { new(1, 7, "/uploads/veiculos/7/a.jpg", 1, true) };

        private static Veiculo NovoVeiculo(string status, decimal preco = 45900m) =>
            new(7, 1, 1, "VW - VolksWagen", "GOL 1.0 Mi Total Flex 8V 4p", 2012, "ABC1D23", "", "Prata", 98000, preco,
                DateTime.Today, status, "", 0m, "Único dono");

        private WebmotorsService Criar()
        {
            var veiculos = new Mock<IVeiculoRepository>();
            veiculos.Setup(x => x.GetByIdAsync(7)).ReturnsAsync(() => _veiculo);
            var imagens = new Mock<IVeiculoImagemRepository>();
            imagens.Setup(x => x.GetByVeiculoIdAsync(7)).ReturnsAsync(() => _fotos);
            var tenant = new Mock<ITenantContext>();
            tenant.SetupGet(x => x.TenantSlug).Returns("loja-" + Guid.NewGuid());
            return new WebmotorsService(new HttpClient(_wm), Options.Create(new WebmotorsSettings()), _config, new Cifra(),
                tenant.Object, veiculos.Object, imagens.Object, new Mock<ILojaRepository>().Object, _anuncios, NullLogger<WebmotorsService>.Instance);
        }

        private const string Modalidade =
            "<ModalidadeWM><CodigoModalidade>2943</CodigoModalidade><Descricao>Anúncio Básico</Descricao><TipoAnuncio>U</TipoAnuncio>" +
            "<QuantidadeAnunciosTotal>100</QuantidadeAnunciosTotal><QuantidadeAnuncios>3</QuantidadeAnuncios><CodigoRetorno>500</CodigoRetorno></ModalidadeWM>";

        private async Task<WebmotorsService> Conectado()
        {
            _wm.Responder("ObterModalidade", Modalidade);
            var s = Criar();
            await s.ConectarAsync(new WebmotorsConectarInput { Cnpj = "11.222.333/0001-81", Email = "loja@x.com", Senha = "segredo" });
            return s;
        }

        private static WebmotorsAnuncioDados Dados() => new()
        {
            MarcaId = 30, ModeloId = 1, VersaoId = 10, AnoModelo = 2012, AnoFabricacao = 2011,
            CambioId = 23001, CombustivelId = 21205, CorId = 30409, Portas = 4, Opcionais = new() { 5, 6 }, UnicoDono = true
        };

        [Fact]
        public async Task Conectar_Guarda_Senha_Cifrada_E_Escolhe_Modalidade_De_Usado()
        {
            var s = await Conectado();
            var status = await s.StatusAsync();

            status.Conectado.Should().BeTrue();
            status.Cnpj.Should().Be("11222333000181");
            status.ModalidadeId.Should().Be(2943);
            status.Homologacao.Should().BeTrue();
            _config.V[WebmotorsService.ChaveConta].Should().Contain("cifrado:segredo").And.NotContain("\"segredo\"");
        }

        [Fact]
        public async Task Login_Recusado_Nao_Grava_Conta()
        {
            var tenant = new Mock<ITenantContext>();
            tenant.SetupGet(x => x.TenantSlug).Returns("recusa");
            var s = new WebmotorsService(new HttpClient(new Recusa()), Options.Create(new WebmotorsSettings()), _config, new Cifra(), tenant.Object,
                new Mock<IVeiculoRepository>().Object, new Mock<IVeiculoImagemRepository>().Object, new Mock<ILojaRepository>().Object,
                _anuncios, NullLogger<WebmotorsService>.Instance);

            var acao = () => s.ConectarAsync(new WebmotorsConectarInput { Cnpj = "11222333000181", Email = "a@b.com", Senha = "x" });

            (await acao.Should().ThrowAsync<WebmotorsException>()).WithMessage("*CNPJ, o e-mail ou a senha*");
            _config.V.Should().NotContainKey(WebmotorsService.ChaveConta);
        }

        private sealed class Recusa : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><autenticarResponse><autenticarResult>" +
                                                "<HashAutenticacao>Erro na Autenticacao</HashAutenticacao><CodigoRetorno>400</CodigoRetorno></autenticarResult></autenticarResponse></soap:Body></soap:Envelope>")
                });
        }

        [Fact]
        public async Task Publicar_Inclui_Com_Os_Campos_Na_Ordem_Do_Wsdl_E_Envia_Foto()
        {
            var s = await Conectado();
            _wm.Responder("IncluirCarro", "<CodigoAnuncio>555</CodigoAnuncio><CodigoRetorno>500</CodigoRetorno>");
            _wm.Responder("IncluirFoto", "<CodigoAnuncio>555</CodigoAnuncio><CodigoFoto>9001</CodigoFoto><CodigoRetorno>500</CodigoRetorno>");

            var info = await s.PublicarAsync(7, Dados());

            info.Publicado.Should().BeTrue();
            info.CodigoAnuncio.Should().Be(555);
            info.Fotos.Should().Be(1);
            info.Erro.Should().BeNull();

            var corpo = _wm.Chamadas.Single(c => c.Operacao == "IncluirCarro").Corpo;
            var ordem = Regex.Matches(corpo, @"<(\w+)>").Select(m => m.Groups[1].Value)
                .SkipWhile(n => n != "pAnuncio").Skip(1).Where(n => n != "OpcionalWM" && n != "CodigoOpcional").ToList();
            ordem.Should().ContainInOrder("CodigoAnuncio", "CodigoModalidade", "TipoAnuncio", "CodigoMarca", "CodigoModelo", "CodigoVersao",
                "AnoDoModelo", "AnoFabricacao", "Km", "Placa", "CodigoCambio", "NrPortas", "CodigoCor", "CodigoCombustivel",
                "UnicoDono", "PrecoReal", "PrecoVenda", "Observacao", "Opcional");
            corpo.Should().Contain("<CodigoAnuncio>0</CodigoAnuncio>").And.Contain("<PrecoVenda>45900.00</PrecoVenda>")
                .And.Contain("<Placa>ABC1D23</Placa>").And.Contain("<UnicoDono>S</UnicoDono>").And.Contain("<Blindado>N</Blindado>")
                .And.Contain("<CodigoOpcional>5</CodigoOpcional>").And.Contain("<pHashAutenticacao>hash-1-");
            _wm.Chamadas.Single(c => c.Operacao == "IncluirFoto").Corpo.Should().Contain("<pByteImage>/9j/4A==</pByteImage>");
        }

        [Fact]
        public async Task Foto_Em_Anuncio_Recem_Criado_Espera_E_Tenta_De_Novo()
        {
            var s = await Conectado();
            _wm.Responder("IncluirCarro", "<CodigoAnuncio>555</CodigoAnuncio><CodigoRetorno>500</CodigoRetorno>");
            _wm.Responder("IncluirFoto",
                "<CodigoFoto>0</CodigoFoto><CodigoRetorno>42|4</CodigoRetorno>",
                "<CodigoFoto>9001</CodigoFoto><CodigoRetorno>500</CodigoRetorno>");

            var info = await s.PublicarAsync(7, Dados());

            info.Fotos.Should().Be(1);
            info.Erro.Should().BeNull();
            _wm.Operacoes.Count(o => o == "IncluirFoto").Should().Be(2);
        }

        [Fact]
        public async Task Sessao_Vencida_Faz_Login_De_Novo_Uma_Vez()
        {
            var s = await Conectado();
            _wm.Responder("ObterMarca",
                "<MarcaWM><CodigoMarca>0</CodigoMarca><CodigoRetorno>401</CodigoRetorno></MarcaWM>",
                "<MarcaWM><CodigoMarca>30</CodigoMarca><NomeMarca>VOLKSWAGEN</NomeMarca><CodigoRetorno>500</CodigoRetorno></MarcaWM>");

            var marcas = await s.TabelaAsync("marcas");

            marcas.Should().ContainSingle(m => m.Codigo == 30);
            _wm.Logins.Should().Be(2);
        }

        [Fact]
        public async Task Editar_Altera_E_So_Reenvia_Fotos_Quando_Mudam()
        {
            var s = await Conectado();
            _wm.Responder("IncluirCarro", "<CodigoAnuncio>555</CodigoAnuncio><CodigoRetorno>500</CodigoRetorno>");
            _wm.Responder("IncluirFoto", "<CodigoFoto>9001</CodigoFoto><CodigoRetorno>500</CodigoRetorno>");
            _wm.Responder("AlterarCarro", "<CodigoAnuncio>555</CodigoAnuncio><CodigoRetorno>500</CodigoRetorno>");
            await s.PublicarAsync(7, Dados());

            _veiculo = NovoVeiculo("D", 43900m);
            await s.SincronizarVeiculoAsync(7);

            var alterar = _wm.Chamadas.Single(c => c.Operacao == "AlterarCarro").Corpo;
            alterar.Should().Contain("<CodigoAnuncio>555</CodigoAnuncio>").And.Contain("<PrecoVenda>43900.00</PrecoVenda>");
            _wm.Operacoes.Count(o => o == "IncluirFoto").Should().Be(1, "as fotos nao mudaram");

            _fotos = new() { new(2, 7, "/uploads/veiculos/7/b.jpg", 1, true) };
            await s.SincronizarVeiculoAsync(7);

            _wm.Chamadas.Single(c => c.Operacao == "ExcluirFoto").Corpo.Should().Contain("<pCodigoFoto>9001</pCodigoFoto>");
            _wm.Operacoes.Count(o => o == "IncluirFoto").Should().Be(2);
        }

        [Fact]
        public async Task Vender_Exclui_Com_Motivo_Vendi_Na_Loja_E_Estornar_Inclui_De_Novo()
        {
            var s = await Conectado();
            _wm.Responder("IncluirCarro",
                "<CodigoAnuncio>555</CodigoAnuncio><CodigoRetorno>500</CodigoRetorno>",
                "<CodigoAnuncio>777</CodigoAnuncio><CodigoRetorno>500</CodigoRetorno>");
            _wm.Responder("IncluirFoto", "<CodigoFoto>9001</CodigoFoto><CodigoRetorno>500</CodigoRetorno>");
            await s.PublicarAsync(7, Dados());

            _veiculo = NovoVeiculo("V");
            await s.SincronizarVeiculoAsync(7);

            _wm.Chamadas.Single(c => c.Operacao == "ExcluirCarro").Corpo.Should()
                .Contain("<pCodigoAnuncio>555</pCodigoAnuncio>").And.Contain("<pMotivoExclusao>2</pMotivoExclusao>");
            _anuncios.Itens.Single().VwmStatus.Should().Be(VeiculoWebmotors.StatusRemovido);

            _veiculo = NovoVeiculo("D");
            await s.SincronizarVeiculoAsync(7);

            var anuncio = _anuncios.Itens.Single();
            anuncio.EstaPublicado.Should().BeTrue();
            anuncio.VwmCodigoAnuncio.Should().Be(777);
            _wm.Operacoes.Count(o => o == "IncluirFoto").Should().Be(2, "anuncio novo precisa das fotos de novo");
        }

        [Fact]
        public async Task Tirar_Do_Ar_Pela_Tela_Nao_Volta_Sozinho_Ao_Editar()
        {
            var s = await Conectado();
            _wm.Responder("IncluirCarro", "<CodigoAnuncio>555</CodigoAnuncio><CodigoRetorno>500</CodigoRetorno>");
            _wm.Responder("IncluirFoto", "<CodigoFoto>9001</CodigoFoto><CodigoRetorno>500</CodigoRetorno>");
            await s.PublicarAsync(7, Dados());

            var info = await s.RemoverAsync(7);
            await s.SincronizarVeiculoAsync(7);

            info.Status.Should().Be(VeiculoWebmotors.StatusPausado);
            _wm.Chamadas.Single(c => c.Operacao == "ExcluirCarro").Corpo.Should().Contain("<pMotivoExclusao>3</pMotivoExclusao>");
            _wm.Operacoes.Count(o => o == "IncluirCarro").Should().Be(1, "so o botao Anunciar recoloca");

            info = await s.PublicarAsync(7, Dados());
            info.Publicado.Should().BeTrue();
        }

        [Fact]
        public async Task Recusa_Da_Webmotors_Fica_No_Anuncio_E_Nao_Marca_Publicado()
        {
            var s = await Conectado();
            _wm.Responder("IncluirCarro", "<CodigoAnuncio>0</CodigoAnuncio><CodigoRetorno>13|2</CodigoRetorno>");

            var acao = () => s.PublicarAsync(7, Dados());

            (await acao.Should().ThrowAsync<WebmotorsException>()).WithMessage("*código 13|2*");
            var anuncio = _anuncios.Itens.Single();
            anuncio.EstaPublicado.Should().BeFalse();
            anuncio.VwmErro.Should().Contain("13|2");
        }

        [Fact]
        public async Task Sincronizar_Ignora_Veiculo_Nunca_Anunciado()
        {
            var s = await Conectado();
            var antes = _wm.Chamadas.Count;

            await s.SincronizarVeiculoAsync(7);

            _wm.Chamadas.Count.Should().Be(antes);
        }

        [Fact]
        public async Task Valida_Campos_Obrigatorios_E_Anos()
        {
            var s = await Conectado();

            var semVersao = Dados();
            semVersao.VersaoId = null;
            semVersao.CorId = null;
            (await ((Func<Task>)(() => s.PublicarAsync(7, semVersao))).Should().ThrowAsync<WebmotorsException>())
                .WithMessage("Preencha: versão, cor.");

            var anos = Dados();
            anos.AnoFabricacao = 2009;
            (await ((Func<Task>)(() => s.PublicarAsync(7, anos))).Should().ThrowAsync<WebmotorsException>())
                .WithMessage("*fabricação*");

            _veiculo = NovoVeiculo("R");
            (await ((Func<Task>)(() => s.PublicarAsync(7, Dados()))).Should().ThrowAsync<WebmotorsException>())
                .WithMessage("*disponível*");
            _wm.Operacoes.Should().NotContain("IncluirCarro");
        }

        [Fact]
        public void Observacao_Respeita_Limite_De_500()
        {
            WebmotorsSoap.Observacao(new string('a', 600))!.Length.Should().Be(500);
            WebmotorsSoap.Observacao("  ok \r\n").Should().Be("ok");
            WebmotorsSoap.Observacao(" ").Should().BeNull();
        }

        [Fact]
        public void Soap_Fault_Vira_Mensagem()
        {
            const string fault = "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><soap:Fault>" +
                                 "<faultcode>soap:Server</faultcode><faultstring>Server was unable to process request. ---> Object reference not set</faultstring>" +
                                 "</soap:Fault></soap:Body></soap:Envelope>";
            var acao = () => WebmotorsSoap.Resultado(fault, "IncluirCarro");
            acao.Should().Throw<WebmotorsException>().WithMessage("*Server was unable to process request.");
        }
    }
}
