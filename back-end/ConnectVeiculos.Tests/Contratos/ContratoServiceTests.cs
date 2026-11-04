using System.Text.RegularExpressions;
using ConnectVeiculos.Core.Contratos;
using ConnectVeiculos.Core.Entities.Categorias;
using ConnectVeiculos.Core.Entities.Contratos;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Entities.Vendas;
using ConnectVeiculos.Core.Interfaces.Email;
using ConnectVeiculos.Core.Interfaces.Security;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using ConnectVeiculos.Infrastructure.Services.Contratos;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.Contratos
{
    public class ContratoTextoTests
    {
        [Theory]
        [InlineData(1, "um real")]
        [InlineData(0.5, "cinquenta centavos")]
        [InlineData(100, "cem reais")]
        [InlineData(101, "cento e um reais")]
        [InlineData(1001, "mil e um reais")]
        [InlineData(1250, "mil duzentos e cinquenta reais")]
        [InlineData(67900, "sessenta e sete mil e novecentos reais")]
        [InlineData(87543.21, "oitenta e sete mil quinhentos e quarenta e três reais e vinte e um centavos")]
        [InlineData(1000000, "um milhão de reais")]
        [InlineData(2500000, "dois milhões e quinhentos mil reais")]
        public void Valor_Por_Extenso(decimal valor, string esperado)
        {
            ContratoTexto.Extenso(valor).Should().Be(esperado);
        }

        [Fact]
        public void Preenche_Variaveis_E_Marca_O_Que_Faltou()
        {
            var faltando = new List<string>();
            var texto = ContratoTexto.Preencher("# Título\n\nComprador **{{cliente.nome}}**, CPF {{ cliente.documento }}.",
                new Dictionary<string, string?> { ["cliente.nome"] = "Maria Souza" }, faltando);

            faltando.Should().Equal("cliente.documento");
            var blocos = ContratoTexto.Blocos(texto);
            blocos.Should().HaveCount(2);
            blocos[0].Tipo.Should().Be("titulo");
            var p = blocos[1].Trechos;
            p.Should().Contain(t => t.Texto == "Maria Souza" && t.Negrito);
            p.Should().Contain(t => t.Faltando && t.Texto == ContratoTexto.Lacuna);
        }

        [Fact]
        public void Campos_Livres_Viram_Campos_Do_Formulario()
        {
            ContratoTexto.Extras("Prazo {{extra.prazo_dias}} e {{extra.comissao}}; de novo {{extra.prazo_dias}}")
                .Select(e => (e.Chave, e.Rotulo)).Should().Equal(("prazo_dias", "Prazo dias"), ("comissao", "Comissao"));
        }

        [Fact]
        public void Modelos_Prontos_So_Usam_Variaveis_Conhecidas()
        {
            var conhecidas = ContratoTexto.Variaveis.Select(v => v.Chave).ToHashSet();
            foreach (var m in ModelosPadrao.Todos)
            {
                var usadas = Regex.Matches(m.Conteudo, @"\{\{\s*([a-z_]+\.[a-z0-9_]+)\s*\}\}").Select(x => x.Groups[1].Value);
                usadas.Where(u => !u.StartsWith("extra.")).Should().OnlyContain(u => conhecidas.Contains(u), m.Nome);
            }
        }
    }

    public class ContratoServiceTests : IDisposable
    {
        private sealed class ProtetorFalso : ITokenProtector
        {
            public string Protect(string plaintext) => "p:" + plaintext;
            public string Unprotect(string ciphertext) => ciphertext[2..];
        }

        private readonly SqliteConnection _connection;
        private readonly ConnectVeiculosDbContext _db;
        private readonly Mock<IEmailService> _email = new();
        private readonly List<(string Para, string Assunto, string Corpo)> _enviados = new();
        private readonly ContratoService _service;

        public ContratoServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _db = new ConnectVeiculosDbContext(new DbContextOptionsBuilder<ConnectVeiculosDbContext>().UseSqlite(_connection).Options);
            _db.Database.EnsureCreated();
            // A venda aponta para um vendedor (Usuario) que o teste nao precisa criar.
            _db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF");

            _db.Lojas.Add(new Loja(1, "Diamante Teste Ltda", "Rua A", "100", "Centro", "Salto",
                "SP", "13320000", null, "loja@teste.com", "1133334444", null, "11999998888",
                null, "11222333000181", null, true, lojSlug: "centro"));
            _db.Categorias.Add(new Categoria(1, "Hatch", "Carros hatch", true));
            _db.Veiculos.Add(new Veiculo(1, 1, 1, "Fiat", "Argo", 2024, "SIY5F10", "9BD358A1NRYJ00001",
                "Branca", 45500, 67900m, DateTime.UtcNow, "V", "Usado", 60000m, veiDonoAtual: "João Dono", veiDonoCelular: "11988887777"));
            _db.SaveChanges();
            _db.Vendas.Add(new Venda(1, 1, 1, DateTime.Today, "Fiat", "Argo", 2024, "9BD358A1NRYJ00001", 67900m, 0, 0,
                "Maria Souza", "529.982.247-25", "11999990000", "maria@exemplo.com", "Rua das Flores, 120, Salto/SP", "PIX"));
            _db.SaveChanges();

            _email.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string, string>((p, a, c) => _enviados.Add((p, a, c)))
                .ReturnsAsync(true);
            var tenant = new Mock<ITenantContext>();
            tenant.SetupGet(t => t.TenantSlug).Returns("diamante");

            _service = new ContratoService(_db, new ProtetorFalso(), _email.Object, tenant.Object, NullLogger<ContratoService>.Instance);
        }

        private async Task<int> Modelo(string tipo)
            => (await _service.ListarModelosAsync(false)).First(m => m.Tipo == tipo).Id;

        private async Task<ContratoInfo> GerarVendaAsync()
        {
            var modelo = await Modelo(TiposContrato.Venda);
            var s = await _service.SugerirAsync(1, modelo);
            return await _service.GerarAsync(new ContratoGerarInput
            {
                ModeloId = modelo, VeiculoId = 1, Parte = s.Parte, Valor = s.Valor, FormaPagamento = s.FormaPagamento
            }, usuarioId: 7);
        }

        private static readonly string Rubrica = CriarRubrica();

        private static string CriarRubrica()
        {
            using var img = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(200, 60);
            using var ms = new MemoryStream();
            SixLabors.ImageSharp.ImageExtensions.SaveAsPng(img, ms);
            return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
        }

        private string UltimoCodigo() => Regex.Match(_enviados.Last().Corpo, @">(\d{6})<").Groups[1].Value;

        [Fact]
        public async Task Na_Primeira_Vez_A_Loja_Ganha_Os_Modelos_Prontos()
        {
            var modelos = await _service.ListarModelosAsync(false);
            modelos.Should().HaveCount(ModelosPadrao.Todos.Count);
            modelos.Select(m => m.Tipo).Should().Contain(new[] { TiposContrato.Venda, TiposContrato.Compra, TiposContrato.Consignacao });
            (await _service.ListarModelosAsync(false)).Should().HaveCount(ModelosPadrao.Todos.Count, "nao semeia de novo");
        }

        [Fact]
        public async Task Sugestao_Da_Venda_Traz_O_Comprador_E_A_Consignacao_O_Dono()
        {
            var venda = await _service.SugerirAsync(1, await Modelo(TiposContrato.Venda));
            venda.Origem.Should().Be("venda");
            venda.Parte.Nome.Should().Be("Maria Souza");
            venda.Valor.Should().Be(67900m);
            venda.PapelCliente.Should().Be("Comprador");

            var consig = await _service.SugerirAsync(1, await Modelo(TiposContrato.Consignacao));
            consig.Origem.Should().Be("veiculo");
            consig.Parte.Nome.Should().Be("João Dono");
            consig.PapelCliente.Should().Be("Consignante");
            consig.Extras.Select(e => e.Chave).Should().Contain(new[] { "comissao", "prazo_dias", "prazo_repasse_dias" });
        }

        [Fact]
        public async Task Gera_Com_O_Texto_Preenchido_E_Congelado()
        {
            var c = await GerarVendaAsync();

            c.Status.Should().Be(Contrato.StatusAguardando);
            c.Titulo.Should().Be("CONTRATO DE COMPRA E VENDA DE VEÍCULO");
            c.Signatarios.Should().HaveCount(2);
            c.Signatarios[0].PapelRotulo.Should().Be("Comprador");
            c.Signatarios[0].Documento.Should().Be("529.982.247-25");
            c.Signatarios[0].Token.Should().NotBeNullOrEmpty();
            c.Signatarios[1].Papel.Should().Be(ContratoSignatario.PapelLoja);

            var texto = string.Concat((await _service.TextoAsync(c.Id)).Blocos.SelectMany(b => b.Trechos).Select(t => t.Texto));
            texto.Should().Contain("Maria Souza").And.Contain("SIY5F10").And.Contain("sessenta e sete mil e novecentos reais")
                .And.Contain("11.222.333/0001-81");
            c.Hash.Should().Be(ContratoTexto.Hash(_db.Contratos.Single().CtrConteudo));
        }

        [Fact]
        public async Task Recusa_Cpf_Invalido_E_Cliente_Sem_Nome()
        {
            var modelo = await Modelo(TiposContrato.Venda);
            await _service.Invoking(s => s.GerarAsync(new ContratoGerarInput { ModeloId = modelo, VeiculoId = 1, Parte = new() { Nome = "Ze" } }, 1))
                .Should().ThrowAsync<ContratoException>().WithMessage("*nome do comprador*");
            await _service.Invoking(s => s.GerarAsync(new ContratoGerarInput { ModeloId = modelo, VeiculoId = 1, Parte = new() { Nome = "Maria", Documento = "111.111.111-11" } }, 1))
                .Should().ThrowAsync<ContratoException>().WithMessage("*CPF/CNPJ*");
        }

        [Fact]
        public async Task Cliente_Assina_Pelo_Link_Com_Codigo_E_A_Loja_Fecha_O_Contrato()
        {
            var c = await GerarVendaAsync();
            var token = c.Signatarios[0].Token!;

            var aberto = await _service.AbrirLinkAsync(token);
            aberto.PrecisaCodigo.Should().BeTrue();
            aberto.EmailMascarado.Should().Be("m****@exemplo.com");

            (await _service.EnviarCodigoAsync(token)).Should().Be("m****@exemplo.com");
            _enviados.Last().Para.Should().Be("maria@exemplo.com");

            var assinado = await _service.AssinarPeloLinkAsync(token, new AssinaturaInput
            {
                Nome = "Maria Souza", Documento = "52998224725", Codigo = UltimoCodigo(), Imagem = Rubrica, Aceite = true
            }, new AssinaturaOrigem { Ip = "200.1.2.3", Navegador = "Teste" });
            assinado.JaAssinou.Should().BeTrue();
            assinado.StatusContrato.Should().Be(Contrato.StatusAguardando, "falta a loja");

            var final = await _service.AssinarPelaLojaAsync(c.Id, new AssinaturaInput { Imagem = Rubrica }, 7, "Vitor", new AssinaturaOrigem());
            final.Status.Should().Be(Contrato.StatusAssinado);
            final.Signatarios.Should().OnlyContain(s => s.Status == ContratoSignatario.StatusAssinado);
            final.Signatarios.Should().OnlyContain(s => s.Token == null, "contrato fechado nao mostra mais o link");

            var s = _db.ContratosSignatarios.AsNoTracking().First(x => x.CsgPapel == ContratoSignatario.PapelCliente);
            s.CsgMetodo.Should().Be(ContratoSignatario.MetodoCodigoEmail);
            s.CsgIp.Should().Be("200.1.2.3");

            var (pdf, nome) = await _service.PdfAsync(c.Id);
            nome.Should().Be($"contrato-{c.Id}.pdf");
            System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
        }

        [Fact]
        public async Task Codigo_Errado_Conta_Tentativa_E_Cpf_Precisa_Conferir()
        {
            var c = await GerarVendaAsync();
            var token = c.Signatarios[0].Token!;
            await _service.EnviarCodigoAsync(token);
            var certo = UltimoCodigo();
            var errado = certo == "000000" ? "111111" : "000000";

            await _service.Invoking(s => s.AssinarPeloLinkAsync(token, new AssinaturaInput
            { Nome = "Maria Souza", Documento = "52998224725", Codigo = errado, Imagem = Rubrica, Aceite = true }, new AssinaturaOrigem()))
                .Should().ThrowAsync<ContratoException>().WithMessage("Código incorreto.");
            _db.ChangeTracker.Clear();
            _db.ContratosSignatarios.First(x => x.CsgPapel == ContratoSignatario.PapelCliente).CsgTentativas.Should().Be(1);

            await _service.Invoking(s => s.AssinarPeloLinkAsync(token, new AssinaturaInput
            { Nome = "Maria Souza", Documento = "12345678909", Codigo = certo, Imagem = Rubrica, Aceite = true }, new AssinaturaOrigem()))
                .Should().ThrowAsync<ContratoException>().WithMessage("*não confere*");

            await _service.Invoking(s => s.EnviarCodigoAsync(token))
                .Should().ThrowAsync<ContratoException>().WithMessage("*Aguarde um minuto*");
        }

        [Fact]
        public async Task Assinatura_Presencial_Registra_Quem_Conferiu()
        {
            var c = await GerarVendaAsync();
            var r = await _service.AssinarPresencialAsync(c.Id, new AssinaturaInput
            { Nome = "Maria Souza", Documento = "529.982.247-25", Imagem = Rubrica, Aceite = true }, 7, "Vitor", new AssinaturaOrigem());

            r.Signatarios[0].Metodo.Should().Be(ContratoSignatario.MetodoPresencial);
            _db.ContratosSignatarios.AsNoTracking().First(x => x.CsgPapel == ContratoSignatario.PapelCliente).CsgConferente.Should().Be("Vitor");
            await _service.Invoking(s => s.AssinarPresencialAsync(c.Id, new AssinaturaInput
            { Nome = "Maria Souza", Documento = "52998224725", Imagem = Rubrica, Aceite = true }, 7, "Vitor", new AssinaturaOrigem()))
                .Should().ThrowAsync<ContratoException>().WithMessage("*já assinou*");
        }

        [Fact]
        public async Task Recusa_Imagem_Que_Nao_E_Png()
        {
            var c = await GerarVendaAsync();
            await _service.Invoking(s => s.AssinarPresencialAsync(c.Id, new AssinaturaInput
            { Nome = "Maria Souza", Documento = "52998224725", Imagem = "data:image/png;base64,AAAA", Aceite = true }, 7, "Vitor", new AssinaturaOrigem()))
                .Should().ThrowAsync<ContratoException>().WithMessage("*inválida*");
        }

        [Fact]
        public async Task Contrato_Cancelado_Fecha_O_Link()
        {
            var c = await GerarVendaAsync();
            await _service.CancelarAsync(c.Id);
            await _service.Invoking(s => s.AbrirLinkAsync(c.Signatarios[0].Token!))
                .Should().ThrowAsync<ContratoException>().WithMessage("*cancelado*");
            await _service.Invoking(s => s.AbrirLinkAsync("token-que-nao-existe"))
                .Should().ThrowAsync<ContratoException>().WithMessage("Link inválido*");
        }

        [Fact]
        public async Task Envia_O_Link_Por_Email_Com_O_Endereco_Da_Loja()
        {
            var c = await GerarVendaAsync();
            await _service.EnviarLinkPorEmailAsync(c.Id, "https://connectveiculos.dev.br");
            _enviados.Last().Corpo.Should().Contain($"https://connectveiculos.dev.br/assinar/diamante/{c.Signatarios[0].Token}");
        }

        [Fact]
        public async Task Modelo_Pronto_Ou_Usado_So_E_Desativado()
        {
            var c = await GerarVendaAsync();
            var modelo = (await _service.ListarModelosAsync(false)).First(m => m.Tipo == TiposContrato.Venda);
            await _service.ExcluirModeloAsync(modelo.Id);
            (await _service.ListarModelosAsync(true)).Single(m => m.Id == modelo.Id).Ativo.Should().BeFalse();

            var proprio = await _service.SalvarModeloAsync(null, new ModeloContratoInput
            { Nome = "Termo da loja", Tipo = "outro", Conteudo = "# Termo\n\nTexto do termo com {{cliente.nome}}." });
            await _service.ExcluirModeloAsync(proprio.Id);
            (await _service.ListarModelosAsync(true)).Should().NotContain(m => m.Id == proprio.Id);
            c.Should().NotBeNull();
        }

        public void Dispose()
        {
            _db.Dispose();
            _connection.Dispose();
        }
    }
}
