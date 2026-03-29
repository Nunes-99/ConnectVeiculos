using ConnectVeiculos.Application.UseCases.Imagens;
using ConnectVeiculos.Core.Entities.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases.Imagens
{
    public class ConsultarImagensVeiculoUseCaseTests
    {
        private readonly Mock<IVeiculoImagemRepository> _imagemRepositoryMock;
        private readonly ConsultarImagensVeiculoUseCase _useCase;

        public ConsultarImagensVeiculoUseCaseTests()
        {
            _imagemRepositoryMock = new Mock<IVeiculoImagemRepository>();
            _useCase = new ConsultarImagensVeiculoUseCase(_imagemRepositoryMock.Object);
        }

        [Fact]
        public async Task Execute_ComImagensExistentes_DeveRetornarLista()
        {
            // Arrange
            var imagens = new List<VeiculoImagem>
            {
                new VeiculoImagem(1, 1, "/images/img1.jpg", 1, true),
                new VeiculoImagem(2, 1, "/images/img2.jpg", 2, true),
                new VeiculoImagem(3, 1, "/images/img3.jpg", 3, true)
            };

            _imagemRepositoryMock.Setup(x => x.GetByVeiculoIdAsync(1))
                .ReturnsAsync(imagens);

            // Act
            var result = await _useCase.Execute(1);

            // Assert
            result.Should().HaveCount(3);
        }

        [Fact]
        public async Task Execute_DeveFiltrarImagensInativas()
        {
            // Arrange
            var imagens = new List<VeiculoImagem>
            {
                new VeiculoImagem(1, 1, "/images/img1.jpg", 1, true),
                new VeiculoImagem(2, 1, "/images/img2.jpg", 2, false), // Inativa
                new VeiculoImagem(3, 1, "/images/img3.jpg", 3, true)
            };

            _imagemRepositoryMock.Setup(x => x.GetByVeiculoIdAsync(1))
                .ReturnsAsync(imagens);

            // Act
            var result = await _useCase.Execute(1);

            // Assert
            result.Should().HaveCount(2);
            result.All(i => i.ImgCaminho != "/images/img2.jpg").Should().BeTrue();
        }

        [Fact]
        public async Task Execute_DeveOrdenarPorOrdem()
        {
            // Arrange
            var imagens = new List<VeiculoImagem>
            {
                new VeiculoImagem(3, 1, "/images/img3.jpg", 3, true),
                new VeiculoImagem(1, 1, "/images/img1.jpg", 1, true),
                new VeiculoImagem(2, 1, "/images/img2.jpg", 2, true)
            };

            _imagemRepositoryMock.Setup(x => x.GetByVeiculoIdAsync(1))
                .ReturnsAsync(imagens);

            // Act
            var result = (await _useCase.Execute(1)).ToList();

            // Assert
            result[0].ImgOrdem.Should().Be(1);
            result[1].ImgOrdem.Should().Be(2);
            result[2].ImgOrdem.Should().Be(3);
        }

        [Fact]
        public async Task Execute_SemImagens_DeveRetornarListaVazia()
        {
            // Arrange
            _imagemRepositoryMock.Setup(x => x.GetByVeiculoIdAsync(999))
                .ReturnsAsync(new List<VeiculoImagem>());

            // Act
            var result = await _useCase.Execute(999);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task Execute_DeveMapearPropriedadesCorretamente()
        {
            // Arrange
            var imagens = new List<VeiculoImagem>
            {
                new VeiculoImagem(5, 10, "/uploads/foto.png", 1, true)
            };

            _imagemRepositoryMock.Setup(x => x.GetByVeiculoIdAsync(10))
                .ReturnsAsync(imagens);

            // Act
            var result = (await _useCase.Execute(10)).First();

            // Assert
            result.ImgId.Should().Be(5);
            result.R_VeiId.Should().Be(10);
            result.ImgCaminho.Should().Be("/uploads/foto.png");
            result.ImgOrdem.Should().Be(1);
        }
    }
}
