using ConnectVeiculos.Core.Entities.Publicacoes;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Entities.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Publicacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Infrastructure.Services.Publicacoes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.Publicacoes
{
    /// <summary>
    /// O cadastro de veiculo sao duas requisicoes: primeiro o veiculo, depois as
    /// fotos (o upload precisa do id). A publicacao acontecia junto com a
    /// primeira, quando o veiculo ainda nao tinha imagem nenhuma — o Instagram
    /// abortava em "sem imagens" e a Facebook Page postava so o texto.
    /// Este servico e' o que espera as fotos antes de publicar.
    /// </summary>
    public class PublicacaoAutomaticaServiceTests
    {
        private const int VeiculoId = 42;

        private readonly Mock<IVeiculoRepository> _veiculos = new();
        private readonly Mock<IVeiculoImagemRepository> _imagens = new();
        private readonly Mock<IVeiculoPublicacaoRepository> _publicacoes = new();
        private readonly Mock<IMercadoLivreService> _ml = new();
        private readonly Mock<IFacebookCatalogService> _fbCatalogo = new();
        private readonly Mock<IFacebookPagePostService> _fbPage = new();
        private readonly Mock<IInstagramPostService> _instagram = new();
        private readonly Mock<IGoogleMerchantService> _google = new();

        public PublicacaoAutomaticaServiceTests()
        {
            _veiculos.Setup(x => x.GetByIdAsync(VeiculoId)).ReturnsAsync(VeiculoDisponivel());
            _ml.Setup(x => x.IsConnectedAsync()).ReturnsAsync(false);
            _fbPage.Setup(x => x.PublicarVeiculoAsync(It.IsAny<int>()))
                   .ReturnsAsync(new PublicacaoResult { ExternoId = "fb1", Url = "https://fb/1" });
            _instagram.Setup(x => x.PublicarVeiculoAsync(It.IsAny<int>()))
                      .ReturnsAsync(new PublicacaoResult { ExternoId = "ig1", Url = "https://ig/1" });
        }

        [Fact]
        public async Task DeveEsperarAsFotosChegaremAntesDePublicar()
        {
            // As duas primeiras leituras nao acham foto — o upload ainda esta
            // acontecendo. So depois as imagens aparecem.
            FotosNasLeituras(0, 0, 3, 3);

            await Criar().PublicarNovoVeiculoAsync(VeiculoId);

            _instagram.Verify(x => x.PublicarVeiculoAsync(VeiculoId), Times.Once);
            _fbPage.Verify(x => x.PublicarVeiculoAsync(VeiculoId), Times.Once);
        }

        [Fact]
        public async Task NaoDevePublicarQuandoNenhumaFotoChega()
        {
            // Sem foto, publicar so repete o bug antigo: Instagram nao posta e o
            // Facebook posta so texto. Melhor nao publicar e deixar pro manual.
            FotosNasLeituras(0, 0, 0, 0, 0, 0);

            await Criar().PublicarNovoVeiculoAsync(VeiculoId);

            _instagram.Verify(x => x.PublicarVeiculoAsync(It.IsAny<int>()), Times.Never);
            _fbPage.Verify(x => x.PublicarVeiculoAsync(It.IsAny<int>()), Times.Never);
            _google.Verify(x => x.PublicarVeiculoAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task DeveEsperarOUploadTerminarEmVezDePublicarComAPrimeiraFoto()
        {
            // Dez fotos nao chegam juntas. Publicar na primeira leitura positiva
            // postaria um carrossel com uma foto so.
            var leiturasAteEstabilizar = 0;
            var contagens = new[] { 1, 4, 9, 10, 10 };
            _imagens.Setup(x => x.GetByVeiculoIdAsync(VeiculoId))
                    .ReturnsAsync(() => Fotos(contagens[Math.Min(leiturasAteEstabilizar++, contagens.Length - 1)]));

            await Criar().PublicarNovoVeiculoAsync(VeiculoId);

            leiturasAteEstabilizar.Should().BeGreaterThanOrEqualTo(5,
                "so a 5a leitura repete a contagem anterior, indicando upload concluido");
            _instagram.Verify(x => x.PublicarVeiculoAsync(VeiculoId), Times.Once);
        }

        [Fact]
        public async Task NaoDevePublicarSeOVeiculoForVendidoDuranteAEspera()
        {
            FotosNasLeituras(2, 2);
            _veiculos.Setup(x => x.GetByIdAsync(VeiculoId)).ReturnsAsync(VeiculoComStatus("V"));

            await Criar().PublicarNovoVeiculoAsync(VeiculoId);

            _instagram.Verify(x => x.PublicarVeiculoAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task NaoDevePublicarSeOVeiculoForApagadoDuranteAEspera()
        {
            FotosNasLeituras(2, 2);
            _veiculos.Setup(x => x.GetByIdAsync(VeiculoId)).ReturnsAsync((Veiculo?)null);

            await Criar().PublicarNovoVeiculoAsync(VeiculoId);

            _instagram.Verify(x => x.PublicarVeiculoAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task NaoDeveDuplicarPostQuandoJaExistePublicacaoAtiva()
        {
            FotosNasLeituras(2, 2);
            _publicacoes.Setup(x => x.GetAtivaByVeiculoEPlataformaAsync(VeiculoId, "Instagram"))
                        .ReturnsAsync(new VeiculoPublicacao(VeiculoId, "Instagram", "ja-existe", "https://ig/x"));

            await Criar().PublicarNovoVeiculoAsync(VeiculoId);

            _instagram.Verify(x => x.PublicarVeiculoAsync(It.IsAny<int>()), Times.Never);
            _fbPage.Verify(x => x.PublicarVeiculoAsync(VeiculoId), Times.Once,
                "uma plataforma ja publicada nao bloqueia as outras");
        }

        [Fact]
        public async Task FalhaEmUmaPlataformaNaoPodeImpedirAsOutras()
        {
            FotosNasLeituras(2, 2);
            _fbPage.Setup(x => x.PublicarVeiculoAsync(It.IsAny<int>()))
                   .ThrowsAsync(new Exception("token da Page expirou"));

            await Criar().PublicarNovoVeiculoAsync(VeiculoId);

            _instagram.Verify(x => x.PublicarVeiculoAsync(VeiculoId), Times.Once);
            _google.Verify(x => x.PublicarVeiculoAsync(VeiculoId), Times.Once);
        }

        [Fact]
        public async Task DeveRegistrarAPublicacaoParaAparecerNaTelaDeIntegracoes()
        {
            FotosNasLeituras(2, 2);
            var salvas = new List<VeiculoPublicacao>();
            _publicacoes.Setup(x => x.CreateAsync(It.IsAny<VeiculoPublicacao>()))
                        .Callback<VeiculoPublicacao>(salvas.Add)
                        .ReturnsAsync(1);

            await Criar().PublicarNovoVeiculoAsync(VeiculoId);

            salvas.Select(p => p.PubPlataforma).Should().Contain(new[] { "FacebookPage", "Instagram" });
        }

        [Fact]
        public async Task FalhaAoLerAsFotosNaoPodeDerrubarAPublicacao()
        {
            // Lock do SQLite durante o upload nao pode custar o post do veiculo.
            var leitura = 0;
            _imagens.Setup(x => x.GetByVeiculoIdAsync(VeiculoId))
                    .ReturnsAsync(() =>
                    {
                        if (leitura++ == 0) throw new Exception("database is locked");
                        return Fotos(3);
                    });

            await Criar().PublicarNovoVeiculoAsync(VeiculoId);

            _instagram.Verify(x => x.PublicarVeiculoAsync(VeiculoId), Times.Once);
        }

        private PublicacaoAutomaticaService Criar() => new(
            _veiculos.Object, _imagens.Object, _publicacoes.Object, _ml.Object,
            _fbCatalogo.Object, _fbPage.Object, _instagram.Object, _google.Object,
            NullLogger<PublicacaoAutomaticaService>.Instance,
            tentativas: 6, intervaloMs: 1, leiturasEstaveis: 2);

        /// <summary>Uma contagem de fotos por leitura; a ultima se repete.</summary>
        private void FotosNasLeituras(params int[] contagens)
        {
            var leitura = 0;
            _imagens.Setup(x => x.GetByVeiculoIdAsync(VeiculoId))
                    .ReturnsAsync(() => Fotos(contagens[Math.Min(leitura++, contagens.Length - 1)]));
        }

        private static IEnumerable<VeiculoImagem> Fotos(int quantidade) =>
            Enumerable.Range(1, quantidade)
                      .Select(i => new VeiculoImagem(i, VeiculoId, $"/uploads/veiculos/{VeiculoId}/{i}.jpg", i, true))
                      .ToList();

        private static Veiculo VeiculoDisponivel() => VeiculoComStatus("D");

        private static Veiculo VeiculoComStatus(string status) => new(
            VeiculoId, 1, 1, "Toyota", "Corolla", 2024, "ABC1D23", "9BWZZZ377VT004251",
            "Branco", 10000, 145000m, DateTime.Now, status, "D", 130000m, "", "", "", "", 140000m);
    }
}
