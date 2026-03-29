using ConnectVeiculos.Application.UseCases.Imagens;
using ConnectVeiculos.Core.Entities.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases.Imagens
{
    public class UploadImagemVeiculoUseCaseTests
    {
        private readonly Mock<IVeiculoImagemRepository> _imagemRepositoryMock;
        private readonly UploadImagemVeiculoUseCase _useCase;

        public UploadImagemVeiculoUseCaseTests()
        {
            _imagemRepositoryMock = new Mock<IVeiculoImagemRepository>();
            _useCase = new UploadImagemVeiculoUseCase(_imagemRepositoryMock.Object);
        }

        [Fact]
        public async Task Execute_ComPrimeiraImagem_DeveDefinirOrdem1()
        {
            // Arrange
            _imagemRepositoryMock.Setup(x => x.GetByVeiculoIdAsync(1))
                .ReturnsAsync(new List<VeiculoImagem>());

            _imagemRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<VeiculoImagem>()))
                .ReturnsAsync(1);

            // Act
            var result = await _useCase.Execute(1, "/images/veiculo1.jpg");

            // Assert
            result.ImgOrdem.Should().Be(1);
            result.R_VeiId.Should().Be(1);
            result.ImgCaminho.Should().Be("/images/veiculo1.jpg");
        }

        [Fact]
        public async Task Execute_ComImagensExistentes_DeveIncrementarOrdem()
        {
            // Arrange
            var imagensExistentes = new List<VeiculoImagem>
            {
                new VeiculoImagem(1, 1, "/images/img1.jpg", 1, true),
                new VeiculoImagem(2, 1, "/images/img2.jpg", 2, true)
            };

            _imagemRepositoryMock.Setup(x => x.GetByVeiculoIdAsync(1))
                .ReturnsAsync(imagensExistentes);

            _imagemRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<VeiculoImagem>()))
                .ReturnsAsync(3);

            // Act
            var result = await _useCase.Execute(1, "/images/img3.jpg");

            // Assert
            result.ImgOrdem.Should().Be(3);
            result.ImgId.Should().Be(3);
        }

        [Fact]
        public async Task Execute_DeveChamarRepositorioComDadosCorretos()
        {
            // Arrange
            _imagemRepositoryMock.Setup(x => x.GetByVeiculoIdAsync(1))
                .ReturnsAsync(new List<VeiculoImagem>());

            VeiculoImagem imagemCriada = null;
            _imagemRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<VeiculoImagem>()))
                .Callback<VeiculoImagem>(i => imagemCriada = i)
                .ReturnsAsync(1);

            // Act
            await _useCase.Execute(1, "/uploads/foto.png");

            // Assert
            imagemCriada.Should().NotBeNull();
            imagemCriada.R_VeiId.Should().Be(1);
            imagemCriada.ImgCaminho.Should().Be("/uploads/foto.png");
            imagemCriada.ImgOrdem.Should().Be(1);
            imagemCriada.ImgSts.Should().BeTrue();
        }

        [Fact]
        public async Task Execute_DeveRetornarViewModelComIdCorreto()
        {
            // Arrange
            _imagemRepositoryMock.Setup(x => x.GetByVeiculoIdAsync(5))
                .ReturnsAsync(new List<VeiculoImagem>());

            _imagemRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<VeiculoImagem>()))
                .ReturnsAsync(10);

            // Act
            var result = await _useCase.Execute(5, "/path/to/image.jpg");

            // Assert
            result.ImgId.Should().Be(10);
            result.R_VeiId.Should().Be(5);
        }
    }
}
