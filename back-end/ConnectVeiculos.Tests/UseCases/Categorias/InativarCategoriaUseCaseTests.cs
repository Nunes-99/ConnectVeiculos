using ConnectVeiculos.Application.UseCases.Categorias;
using ConnectVeiculos.Core.Entities.Categorias;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Categorias;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases.Categorias
{
    public class InativarCategoriaUseCaseTests
    {
        private readonly Mock<ICategoriaRepository> _categoriaRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly InativarCategoriaUseCase _useCase;

        public InativarCategoriaUseCaseTests()
        {
            _categoriaRepositoryMock = new Mock<ICategoriaRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _useCase = new InativarCategoriaUseCase(_categoriaRepositoryMock.Object, _unitOfWorkMock.Object);
        }

        [Fact]
        public async Task Execute_ComIdValido_DeveInativarCategoria()
        {
            // Arrange
            var categoria = new Categoria(1, "SUV", "Sport Utility Vehicle", true);

            _categoriaRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(categoria);

            Categoria categoriaAtualizada = null;
            _categoriaRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Categoria>()))
                .Callback<Categoria>(c => categoriaAtualizada = c)
                .Returns(Task.CompletedTask);

            // Act
            await _useCase.Execute(1);

            // Assert
            categoriaAtualizada.Should().NotBeNull();
            categoriaAtualizada.CatSts.Should().BeFalse();
            _unitOfWorkMock.Verify(x => x.Commit(), Times.Once);
        }

        [Fact]
        public async Task Execute_ComIdInexistente_DeveLancarExcecao()
        {
            // Arrange
            _categoriaRepositoryMock.Setup(x => x.GetByIdAsync(999))
                .ReturnsAsync((Categoria)null);

            // Act
            Func<Task> act = async () => await _useCase.Execute(999);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("Categoria nao encontrada.");
        }

        [Fact]
        public async Task Execute_ComErroNoRepositorio_DeveRollback()
        {
            // Arrange
            var categoria = new Categoria(1, "SUV", "Sport Utility Vehicle", true);

            _categoriaRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(categoria);

            _categoriaRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Categoria>()))
                .ThrowsAsync(new Exception("Erro no banco"));

            // Act
            Func<Task> act = async () => await _useCase.Execute(1);

            // Assert
            await act.Should().ThrowAsync<Exception>();
            _unitOfWorkMock.Verify(x => x.Rollback(), Times.Once);
        }
    }
}
