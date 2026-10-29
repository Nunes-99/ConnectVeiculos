using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.Publicacoes;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Entities.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Configuracoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Publicacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Infrastructure.Services.Meta;
using ConnectVeiculos.Infrastructure.Services.Stories;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace ConnectVeiculos.Tests.Publicacoes
{
    /// <summary>
    /// Story publicado pela API nao tem legenda nem figurinha de link: preco,
    /// carro e loja vao desenhados numa arte 9:16 gerada no servidor.
    /// </summary>
    public class StoryServiceTests : IDisposable
    {
        private const int VeiculoId = 7;

        private readonly string _raiz;
        private readonly Dictionary<string, string> _config = new();
        private readonly Mock<IConfiguracaoSistemaRepository> _configRepo = new();
        private readonly Mock<IVeiculoRepository> _veiculos = new();
        private readonly Mock<IVeiculoImagemRepository> _imagens = new();
        private readonly Mock<ILojaRepository> _lojas = new();
        private readonly Mock<IVeiculoPublicacaoRepository> _publicacoes = new();
        private readonly Mock<IInstagramPostService> _instagram = new();
        private readonly Mock<IFacebookPagePostService> _facebook = new();
        private readonly List<VeiculoPublicacao> _historico = new();

        public StoryServiceTests()
        {
            _raiz = Path.Combine(Path.GetTempPath(), "cv-story-" + Guid.NewGuid().ToString("N")[..8]);
            var pastaFoto = Path.Combine(_raiz, "uploads", "veiculos", VeiculoId.ToString());
            Directory.CreateDirectory(pastaFoto);
            using (var foto = new Image<Rgba32>(1600, 1200, Color.SteelBlue))
                foto.SaveAsJpeg(Path.Combine(pastaFoto, "foto.jpg"));

            _configRepo.Setup(x => x.GetValorAsync(It.IsAny<string>()))
                       .ReturnsAsync((string k) => _config.TryGetValue(k, out var v) ? v : null);
            _configRepo.Setup(x => x.SetValorAsync(It.IsAny<string>(), It.IsAny<string>()))
                       .Callback((string k, string v) => _config[k] = v)
                       .Returns(Task.CompletedTask);

            _veiculos.Setup(x => x.GetByIdAsync(VeiculoId)).ReturnsAsync(Veiculo("D", 89900m));
            _imagens.Setup(x => x.GetByVeiculoIdAsync(VeiculoId)).ReturnsAsync(new[]
            {
                new VeiculoImagem(1, VeiculoId, $"/uploads/veiculos/{VeiculoId}/foto.jpg", 0, true)
            });
            _lojas.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(Loja("https://diamanteveiculos.com.br"));

            _publicacoes.Setup(x => x.GetByVeiculoIdAsync(VeiculoId)).ReturnsAsync(() => _historico.ToList());
            _publicacoes.Setup(x => x.CreateAsync(It.IsAny<VeiculoPublicacao>()))
                        .Callback((VeiculoPublicacao p) => _historico.Add(p))
                        .ReturnsAsync(1);

            _instagram.Setup(x => x.IsConfiguredAsync()).ReturnsAsync(true);
            _facebook.Setup(x => x.IsConfiguredAsync()).ReturnsAsync(true);
            _instagram.Setup(x => x.PublicarStoryAsync(It.IsAny<int>(), It.IsAny<string>()))
                      .ReturnsAsync(new PublicacaoResult { ExternoId = "ig-s1", Url = "https://instagram.com/stories/loja/1" });
            _facebook.Setup(x => x.PublicarStoryAsync(It.IsAny<int>(), It.IsAny<string>()))
                     .ReturnsAsync(new PublicacaoResult { ExternoId = "fb-s1", Url = "https://facebook.com/123" });
        }

        public void Dispose()
        {
            try { Directory.Delete(_raiz, recursive: true); } catch { }
        }

        [Theory]
        [InlineData(1600, 1200)]  // horizontal, o caso comum
        [InlineData(900, 1600)]   // vertical de celular
        [InlineData(1000, 1000)]  // quadrada
        [InlineData(4000, 1000)]  // panoramica
        public void ArteDeveSairEm9por16QualquerQueSejaAFoto(int largura, int altura)
        {
            using var foto = new Image<Rgba32>(largura, altura, Color.DarkRed);
            var dados = StoryService.MontarDados(Veiculo("D", 89900m), Loja(null), EventoStory.PrecoBaixou, 95000m);

            var jpeg = StoryArte.Gerar(foto, dados);

            using var arte = Image.Load(jpeg);
            arte.Width.Should().Be(1080);
            arte.Height.Should().Be(1920);
        }

        [Fact]
        public void ArteAguentaTituloLongoECorDaLojaInvalida()
        {
            using var foto = new Image<Rgba32>(1600, 1200, Color.Gray);
            var dados = new StoryArteDados
            {
                Selo = "Acabou de chegar",
                Titulo = "Volkswagen Nivus Highline 200 TSI Automático Teto Solar Edição Especial",
                Detalhes = "2024  •  12.000 km  •  Cinza",
                Preco = "R$ 139.900",
                LojaNome = "Diamante Veículos",
                Contato = "WhatsApp (11) 99999-0000",
                CorDestaque = "nao-e-cor"
            };

            var acao = () => StoryArte.Gerar(foto, dados);

            acao.Should().NotThrow();
        }

        [Fact]
        public void PrecoQueBaixouMostraOValorAntigo()
        {
            var dados = StoryService.MontarDados(Veiculo("D", 89900m), Loja(null), EventoStory.PrecoBaixou, 95000m);

            dados.Selo.Should().Be("Baixou o preço");
            dados.Preco.Should().Be("R$ 89.900");
            dados.PrecoAnterior.Should().Be("De R$ 95.000");
        }

        [Fact]
        public void VendidoNaoMostraPreco()
        {
            var dados = StoryService.MontarDados(Veiculo("V", 89900m), Loja(null), EventoStory.Vendido, null);

            dados.Selo.Should().Be("Vendido");
            dados.Preco.Should().BeNull();
            dados.Mensagem.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void DadosDaLojaEntramNaArte()
        {
            var dados = StoryService.MontarDados(Veiculo("D", 89900.5m), Loja(null), EventoStory.Novo, null);

            dados.Titulo.Should().Be("Toyota Corolla");
            dados.Detalhes.Should().Be("2022  •  35.000 km  •  Prata");
            dados.Preco.Should().Be("R$ 89.900,50");
            dados.LojaNome.Should().Be("Diamante Veículos");
            dados.Contato.Should().Be("WhatsApp (11) 98888-7777");
            dados.CorDestaque.Should().Be("#0D47A1");
        }

        [Fact]
        public async Task EventoComInterruptorDesligadoNaoPublica()
        {
            Ligar(StoryService.KEY_NO_INSTAGRAM);

            await Criar().PublicarAutomaticoAsync(VeiculoId, EventoStory.Novo);

            _instagram.Verify(x => x.PublicarStoryAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task EventoLigadoPublicaSoOndeALojaEscolheu()
        {
            Ligar(StoryService.KEY_NO_INSTAGRAM, StoryService.KEY_AO_CHEGAR);

            await Criar().PublicarAutomaticoAsync(VeiculoId, EventoStory.Novo);

            _instagram.Verify(x => x.PublicarStoryAsync(VeiculoId,
                It.Is<string>(u => u.StartsWith("https://diamanteveiculos.com.br/api/imagens/file?path=%2Fuploads%2F_stories%2F"))),
                Times.Once);
            _facebook.Verify(x => x.PublicarStoryAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
            _historico.Should().ContainSingle(p => p.PubPlataforma == InstagramPostService.PLATAFORMA_STORY);
        }

        [Fact]
        public async Task ArteFicaGravadaNoCaminhoQueAUrlAponta()
        {
            Ligar(StoryService.KEY_NO_INSTAGRAM);
            string? url = null;
            _instagram.Setup(x => x.PublicarStoryAsync(It.IsAny<int>(), It.IsAny<string>()))
                      .Callback((int _, string u) => url = u)
                      .ReturnsAsync(new PublicacaoResult { ExternoId = "x", Url = "y" });

            await Criar().PublicarManualAsync(VeiculoId);

            var caminho = Uri.UnescapeDataString(url!.Split("path=")[1]);
            File.Exists(Path.Combine(_raiz, caminho.TrimStart('/'))).Should().BeTrue();
        }

        [Fact]
        public async Task PrecoQueBaixaDuasVezesNoDiaGeraUmStorySo()
        {
            Ligar(StoryService.KEY_NO_INSTAGRAM, StoryService.KEY_AO_BAIXAR_PRECO);
            var servico = Criar();

            await servico.PublicarAutomaticoAsync(VeiculoId, EventoStory.PrecoBaixou, 95000m);
            await servico.PublicarAutomaticoAsync(VeiculoId, EventoStory.PrecoBaixou, 92000m);

            _instagram.Verify(x => x.PublicarStoryAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Once);
        }

        [Theory]
        [InlineData(null, 3)]   // loja que nunca mexeu: padrao 3
        [InlineData("1", 1)]
        [InlineData("2", 2)]
        [InlineData("9", 3)]    // fora da faixa vira o maximo
        public async Task ManualPublicaAQuantidadeDeImagensConfigurada(string? configurado, int esperado)
        {
            Ligar(StoryService.KEY_NO_INSTAGRAM);
            ComFotos(5);
            if (configurado != null) _config[StoryService.KEY_QTD_IMAGENS] = configurado;

            await Criar().PublicarManualAsync(VeiculoId);

            _instagram.Verify(x => x.PublicarStoryAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Exactly(esperado));
        }

        [Fact]
        public async Task PrepararReservaOCarroAteAPublicacaoTerminar()
        {
            // O botao agenda e responde na hora; um segundo clique antes de a
            // publicacao em segundo plano terminar nao pode agendar outra.
            Ligar(StoryService.KEY_NO_INSTAGRAM);
            var servico = Criar();

            var primeiro = await servico.PrepararManualAsync(VeiculoId);
            var segundo = await servico.PrepararManualAsync(VeiculoId);
            await servico.PublicarManualAsync(VeiculoId);
            var depois = await servico.PrepararManualAsync(VeiculoId);

            primeiro.Sucesso.Should().BeTrue();
            primeiro.Mensagem.Should().Contain("Publicando");
            segundo.Sucesso.Should().BeFalse();
            segundo.Mensagem.Should().Contain("já está sendo publicado");
            depois.Sucesso.Should().BeFalse();
            depois.Mensagem.Should().Contain("espere 10 minutos");
            _instagram.Verify(x => x.PublicarStoryAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task PrepararSemFotoAvisaNaHoraENaoReserva()
        {
            Ligar(StoryService.KEY_NO_INSTAGRAM);
            _imagens.Setup(x => x.GetByVeiculoIdAsync(VeiculoId)).ReturnsAsync(Array.Empty<VeiculoImagem>());

            var r = await Criar().PrepararManualAsync(VeiculoId);

            r.Sucesso.Should().BeFalse();
            r.Mensagem.Should().Contain("não tem foto");
        }

        [Fact]
        public async Task ManualRepetidoLogoEmSeguidaNaoPublicaDeNovo()
        {
            // Foi o que aconteceu em producao: o nginx deu timeout, o painel
            // mostrou erro, o usuario clicou de novo e saiu tudo em dobro.
            Ligar(StoryService.KEY_NO_INSTAGRAM);
            var servico = Criar();

            var primeiro = await servico.PublicarManualAsync(VeiculoId);
            var segundo = await servico.PublicarManualAsync(VeiculoId);

            primeiro.Sucesso.Should().BeTrue();
            segundo.Sucesso.Should().BeFalse();
            segundo.Mensagem.Should().Contain("espere 10 minutos");
            _instagram.Verify(x => x.PublicarStoryAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task ManualSemDestinoEscolhidoExplicaOQueFalta()
        {
            var r = await Criar().PublicarManualAsync(VeiculoId);

            r.Sucesso.Should().BeFalse();
            r.Mensagem.Should().Contain("Integrações");
        }

        [Fact]
        public async Task ManualSemHttpsNaoChamaAMeta()
        {
            Ligar(StoryService.KEY_NO_INSTAGRAM);
            _lojas.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(Loja("http://localhost:4200"));

            var r = await Criar().PublicarManualAsync(VeiculoId);

            r.Sucesso.Should().BeFalse();
            _instagram.Verify(x => x.PublicarStoryAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ManualQueFalhaNumaRedeAvisaQualFalhou()
        {
            Ligar(StoryService.KEY_NO_INSTAGRAM, StoryService.KEY_NO_FACEBOOK);
            _facebook.Setup(x => x.PublicarStoryAsync(It.IsAny<int>(), It.IsAny<string>()))
                     .ReturnsAsync((PublicacaoResult?)null);

            var r = await Criar().PublicarManualAsync(VeiculoId);

            r.Sucesso.Should().BeTrue();
            r.Mensagem.Should().Be("Story publicado no Instagram, mas falhou no Facebook.");
        }

        [Fact]
        public async Task ConfigGravaEVoltaIgual()
        {
            var servico = Criar();
            await servico.SalvarConfigAsync(new StoryConfigInput { NoFacebook = true, AoVender = true });

            var c = await servico.GetConfigAsync();

            c.NoFacebook.Should().BeTrue();
            c.AoVender.Should().BeTrue();
            c.NoInstagram.Should().BeFalse();
            c.AoChegar.Should().BeFalse();
        }

        [Fact]
        public async Task ManualComVariasFotosViraSequenciaDeTres()
        {
            Ligar(StoryService.KEY_NO_INSTAGRAM);
            ComFotos(5);
            var urls = new List<string>();
            _instagram.Setup(x => x.PublicarStoryAsync(It.IsAny<int>(), It.IsAny<string>()))
                      .Callback((int _, string u) => urls.Add(u))
                      .ReturnsAsync(new PublicacaoResult { ExternoId = "ig", Url = "https://ig/s" });

            var r = await Criar().PublicarManualAsync(VeiculoId);

            urls.Should().HaveCount(3).And.OnlyHaveUniqueItems();
            _historico.Should().HaveCount(3, "cada story gasta uma publicacao do limite diario");
            r.Mensagem.Should().Be("Story publicado no Instagram (3 stories em sequência)!");
        }

        [Fact]
        public async Task CarroNovoTambemViraSequencia()
        {
            Ligar(StoryService.KEY_NO_FACEBOOK, StoryService.KEY_AO_CHEGAR);
            ComFotos(2);

            await Criar().PublicarAutomaticoAsync(VeiculoId, EventoStory.Novo);

            _facebook.Verify(x => x.PublicarStoryAsync(VeiculoId, It.IsAny<string>()), Times.Exactly(2));
        }

        [Fact]
        public async Task PrecoEVendaFicamNumaArteSo()
        {
            Ligar(StoryService.KEY_NO_INSTAGRAM, StoryService.KEY_AO_BAIXAR_PRECO, StoryService.KEY_AO_VENDER);
            ComFotos(4);
            var servico = Criar();

            await servico.PublicarAutomaticoAsync(VeiculoId, EventoStory.PrecoBaixou, 95000m);
            _historico.Clear();
            await servico.PublicarAutomaticoAsync(VeiculoId, EventoStory.Vendido);

            _instagram.Verify(x => x.PublicarStoryAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Exactly(2));
        }

        [Fact]
        public async Task SeAArteCompletaFalhaAsFotosNaoSaemSoltas()
        {
            Ligar(StoryService.KEY_NO_INSTAGRAM);
            ComFotos(3);
            _instagram.Setup(x => x.PublicarStoryAsync(It.IsAny<int>(), It.IsAny<string>()))
                      .ReturnsAsync((PublicacaoResult?)null);

            var r = await Criar().PublicarManualAsync(VeiculoId);

            r.Sucesso.Should().BeFalse();
            _instagram.Verify(x => x.PublicarStoryAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task FotoExtraSemArquivoEPulada()
        {
            Ligar(StoryService.KEY_NO_INSTAGRAM);
            ComFotos(3);
            File.Delete(Path.Combine(_raiz, "uploads", "veiculos", VeiculoId.ToString(), "foto2.jpg"));

            await Criar().PublicarManualAsync(VeiculoId);

            _instagram.Verify(x => x.PublicarStoryAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Exactly(2));
        }

        [Theory]
        [InlineData(1600, 1200)]
        [InlineData(900, 1600)]
        public void ArteDeContinuacaoSaiEm9por16(int largura, int altura)
        {
            using var foto = new Image<Rgba32>(largura, altura, Color.Teal);
            var dados = StoryService.MontarDados(Veiculo("D", 89900m), Loja(null), EventoStory.Manual, null);

            using var arte = Image.Load(StoryArte.GerarFoto(foto, dados));

            arte.Width.Should().Be(1080);
            arte.Height.Should().Be(1920);
        }

        /// <summary>A foto principal do construtor mais (quantidade - 1) fotos.</summary>
        private void ComFotos(int quantidade)
        {
            var pasta = Path.Combine(_raiz, "uploads", "veiculos", VeiculoId.ToString());
            var lista = new List<VeiculoImagem>
            {
                new(1, VeiculoId, $"/uploads/veiculos/{VeiculoId}/foto.jpg", 0, true)
            };
            for (var i = 2; i <= quantidade; i++)
            {
                using (var foto = new Image<Rgba32>(1200, 900, Color.Olive))
                    foto.SaveAsJpeg(Path.Combine(pasta, $"foto{i}.jpg"));
                lista.Add(new VeiculoImagem(i, VeiculoId, $"/uploads/veiculos/{VeiculoId}/foto{i}.jpg", i, true));
            }
            _imagens.Setup(x => x.GetByVeiculoIdAsync(VeiculoId)).ReturnsAsync(lista);
        }

        private void Ligar(params string[] chaves)
        {
            foreach (var k in chaves) _config[k] = "true";
        }

        private StoryService Criar()
        {
            var env = new Mock<IHostEnvironment>();
            env.SetupGet(e => e.ContentRootPath).Returns(_raiz);
            return new StoryService(
                _configRepo.Object, _veiculos.Object, _imagens.Object, _lojas.Object, _publicacoes.Object,
                _instagram.Object, _facebook.Object,
                Options.Create(new MetaSettings { PublicSiteUrl = "https://connectveiculos.dev.br" }),
                env.Object, NullLogger<StoryService>.Instance);
        }

        private static Veiculo Veiculo(string status, decimal preco) => new(
            VeiculoId, 1, 1, "Toyota", "Corolla", 2022, "ABC1D23", "9BWZZZ377VT004251",
            "Prata", 35000, preco, DateTime.Now, status, "D", 70000m);

        private static Loja Loja(string? urlCatalogo) => new(
            1, "Diamante Veículos", "Rua A", "10", "Centro", "São Paulo", "SP", "01000-000",
            null, "contato@diamante.com.br", null, null, "(11) 98888-7777", null, null, null, true,
            lojCorPrimaria: "#0D47A1", lojUrlCatalogo: urlCatalogo);
    }
}
