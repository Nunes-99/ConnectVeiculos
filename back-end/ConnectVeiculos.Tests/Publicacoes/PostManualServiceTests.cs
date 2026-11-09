using ConnectVeiculos.Core.Entities.Publicacoes;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Entities.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Publicacoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Infrastructure.Services.Meta;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.Publicacoes
{
    /// <summary>
    /// Post manual em segundo plano: o clique confere e reserva, a publicacao
    /// roda depois e o painel acompanha pela situacao.
    /// </summary>
    public class PostManualServiceTests
    {
        // A situacao fica num dicionario estatico: cada teste usa um carro proprio.
        private static int _proximoId = 9100;

        private readonly Mock<IInstagramPostService> _instagram = new();
        private readonly Mock<IFacebookPagePostService> _facebook = new();
        private readonly Mock<IVeiculoRepository> _veiculos = new();
        private readonly Mock<IVeiculoImagemRepository> _imagens = new();
        private readonly Mock<IVeiculoPublicacaoRepository> _publicacoes = new();
        private readonly int _id = Interlocked.Increment(ref _proximoId);

        public PostManualServiceTests()
        {
            _instagram.Setup(x => x.IsConfiguredAsync()).ReturnsAsync(true);
            _facebook.Setup(x => x.IsConfiguredAsync()).ReturnsAsync(true);
            _veiculos.Setup(x => x.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((int id) =>
                new Veiculo(id, 1, 1, "Honda", "Civic EXL 2.0 16V 4p Aut.", 2023, "ABC1D23", "", "Branco", 15000, 115000m, DateTime.Today, "D", "", 0m, ""));
            _imagens.Setup(x => x.GetByVeiculoIdAsync(It.IsAny<int>())).ReturnsAsync((int id) =>
                new[] { new VeiculoImagem(1, id, "/uploads/1.jpg", 1, true) });
        }

        private PostManualService Servico() => new(
            _instagram.Object, _facebook.Object, _veiculos.Object, _imagens.Object, _publicacoes.Object,
            NullLogger<PostManualService>.Instance);

        [Fact]
        public async Task CliqueReservaEPublicacaoTerminaComOLink()
        {
            _instagram.Setup(x => x.PublicarManualAsync(_id, "minha legenda"))
                .ReturnsAsync(new PublicacaoResult { ExternoId = "m1", Url = "https://instagram.com/p/x" });
            var s = Servico();

            var reserva = await s.PrepararAsync(_id, "instagram");
            reserva.Estado.Should().Be(PostManualSituacao.Publicando);
            s.Situacao(_id, "instagram")!.Estado.Should().Be(PostManualSituacao.Publicando);

            await s.PublicarAsync(_id, "instagram", "minha legenda");

            var fim = s.Situacao(_id, "instagram")!;
            fim.Estado.Should().Be(PostManualSituacao.Publicado);
            fim.Url.Should().Be("https://instagram.com/p/x");
            _publicacoes.Verify(x => x.CreateAsync(It.Is<VeiculoPublicacao>(p =>
                p.PubPlataforma == "Instagram" && p.PubExternoId == "m1")), Times.Once);
        }

        [Fact]
        public async Task SegundoCliqueEnquantoPublicaERecusado()
        {
            var s = Servico();
            (await s.PrepararAsync(_id, "facebook")).Estado.Should().Be(PostManualSituacao.Publicando);

            var segundo = await s.PrepararAsync(_id, "facebook");

            segundo.Estado.Should().Be(PostManualSituacao.Falhou);
            segundo.Mensagem.Should().Contain("já está sendo publicado");
        }

        [Fact]
        public async Task RedesSaoIndependentes()
        {
            var s = Servico();
            await s.PrepararAsync(_id, "instagram");

            (await s.PrepararAsync(_id, "facebook")).Estado.Should().Be(PostManualSituacao.Publicando);
        }

        [Fact]
        public async Task FalhaDaMetaFicaComOMotivoDaConexao()
        {
            _facebook.Setup(x => x.PublicarManualAsync(_id, null)).ReturnsAsync((PublicacaoResult?)null);
            _facebook.Setup(x => x.TestarAsync()).ReturnsAsync(new TestIntegracaoResult { Sucesso = false, Mensagem = "Token expirado. Reconecte a conta Meta." });
            var s = Servico();
            await s.PrepararAsync(_id, "facebook");

            await s.PublicarAsync(_id, "facebook", null);

            var fim = s.Situacao(_id, "facebook")!;
            fim.Estado.Should().Be(PostManualSituacao.Falhou);
            fim.Mensagem.Should().Be("Token expirado. Reconecte a conta Meta.");
            _publicacoes.Verify(x => x.CreateAsync(It.IsAny<VeiculoPublicacao>()), Times.Never);
            // Depois da falha da para tentar de novo.
            (await s.PrepararAsync(_id, "facebook")).Estado.Should().Be(PostManualSituacao.Publicando);
        }

        [Fact]
        public async Task ExcecaoNaoDeixaOCarroTravado()
        {
            _instagram.Setup(x => x.PublicarManualAsync(_id, null)).ThrowsAsync(new HttpRequestException("timeout"));
            var s = Servico();
            await s.PrepararAsync(_id, "instagram");

            await s.PublicarAsync(_id, "instagram", null);

            s.Situacao(_id, "instagram")!.Estado.Should().Be(PostManualSituacao.Falhou);
            (await s.PrepararAsync(_id, "instagram")).Estado.Should().Be(PostManualSituacao.Publicando);
        }

        [Fact]
        public async Task RepublicarRemoveAPublicacaoAnterior()
        {
            var anterior = new VeiculoPublicacao(_id, "Instagram", "velho", "https://instagram.com/p/velho");
            _publicacoes.Setup(x => x.GetAtivaByVeiculoEPlataformaAsync(_id, "Instagram")).ReturnsAsync(anterior);
            _instagram.Setup(x => x.PublicarManualAsync(_id, null))
                .ReturnsAsync(new PublicacaoResult { ExternoId = "novo", Url = "https://instagram.com/p/novo" });
            var s = Servico();
            await s.PrepararAsync(_id, "instagram");

            await s.PublicarAsync(_id, "instagram", null);

            _publicacoes.Verify(x => x.UpdateAsync(anterior), Times.Once);
        }

        [Fact]
        public async Task ConfereNaHoraOQueOLojistaResolve()
        {
            _instagram.Setup(x => x.IsConfiguredAsync()).ReturnsAsync(false);
            (await Servico().PrepararAsync(_id, "instagram")).Mensagem.Should().Contain("Conecte o Instagram");

            _imagens.Setup(x => x.GetByVeiculoIdAsync(_id)).ReturnsAsync(Array.Empty<VeiculoImagem>());
            (await Servico().PrepararAsync(_id, "facebook")).Mensagem.Should().Be("O veículo não tem foto.");

            (await Servico().PrepararAsync(_id, "tiktok")).Estado.Should().Be(PostManualSituacao.Falhou);
            Servico().Situacao(_id, "facebook").Should().BeNull();
        }
    }
}
