using ConnectVeiculos.Application.UseCases.Imagens;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases.Imagens
{
    public class ExcluirImagemVeiculoUseCaseTests
    {
        private readonly Mock<IVeiculoImagemRepository> _imagemRepositoryMock;
        private readonly ExcluirImagemVeiculoUseCase _useCase;

        public ExcluirImagemVeiculoUseCaseTests()
        {
            _imagemRepositoryMock = new Mock<IVeiculoImagemRepository>();
            _useCase = new ExcluirImagemVeiculoUseCase(_imagemRepositoryMock.Object);
        }

        [Fact]
        public async Task Execute_ComIdValido_DeveChamarDelete()
        {
            // Arrange
            _imagemRepositoryMock.Setup(x => x.DeleteAsync(1))
                .Returns(Task.CompletedTask);

            // Act
            await _useCase.Execute(1);

            // Assert
            _imagemRepositoryMock.Verify(x => x.DeleteAsync(1), Times.Once);
        }

        [Fact]
        public async Task Execute_DeveChamarDeleteComIdCorreto()
        {
            // Arrange
            int idCapturado = 0;
            _imagemRepositoryMock.Setup(x => x.DeleteAsync(It.IsAny<int>()))
                .Callback<int>(id => idCapturado = id)
                .Returns(Task.CompletedTask);

            // Act
            await _useCase.Execute(123);

            // Assert
            idCapturado.Should().Be(123);
        }

        [Fact]
        public async Task Execute_ComErroNoRepositorio_DevePropagarExcecao()
        {
            // Arrange
            _imagemRepositoryMock.Setup(x => x.DeleteAsync(999))
                .ThrowsAsync(new Exception("Imagem não encontrada"));

            // Act
            Func<Task> act = async () => await _useCase.Execute(999);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("Imagem não encontrada");
        }
    }
}
