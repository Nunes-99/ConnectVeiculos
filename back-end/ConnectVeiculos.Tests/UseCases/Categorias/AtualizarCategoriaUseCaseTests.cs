using ConnectVeiculos.Application.InputModels.Categorias;
using ConnectVeiculos.Application.UseCases.Categorias;
using ConnectVeiculos.Core.Entities.Categorias;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Categorias;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases.Categorias
{
    public class AtualizarCategoriaUseCaseTests
    {
        private readonly Mock<ICategoriaRepository> _categoriaRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly AtualizarCategoriaUseCase _useCase;

        public AtualizarCategoriaUseCaseTests()
        {
            _categoriaRepositoryMock = new Mock<ICategoriaRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _useCase = new AtualizarCategoriaUseCase(_categoriaRepositoryMock.Object, _unitOfWorkMock.Object);
        }

        [Fact]
        public async Task Execute_ComDadosValidos_DeveAtualizarCategoria()
        {
            // Arrange
            var categoriaExistente = new Categoria(1, "SUV Antigo", "Descrição antiga", true);

            _categoriaRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(categoriaExistente);

            var input = new CategoriaInputModel
            {
                CatId = 1,
                CatNome = "SUV Atualizado",
                CatDesc = "Nova descrição",
                CatSts = true
            };

            // Act
            await _useCase.Execute(input);

            // Assert
            _categoriaRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Categoria>()), Times.Once);
            _unitOfWorkMock.Verify(x => x.Commit(), Times.Once);
        }

        [Fact]
        public async Task Execute_ComCategoriaInexistente_DeveLancarExcecao()
        {
            // Arrange
            _categoriaRepositoryMock.Setup(x => x.GetByIdAsync(999))
                .ReturnsAsync((Categoria)null);

            var input = new CategoriaInputModel { CatId = 999, CatNome = "Teste" };

            // Act
            Func<Task> act = async () => await _useCase.Execute(input);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("Categoria nao encontrada.");
        }

        [Fact]
        public async Task Execute_ComErroNoRepositorio_DeveRollback()
        {
            // Arrange
            var categoriaExistente = new Categoria(1, "SUV", "Descrição", true);

            _categoriaRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(categoriaExistente);

            _categoriaRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Categoria>()))
                .ThrowsAsync(new Exception("Erro no banco"));

            var input = new CategoriaInputModel { CatId = 1, CatNome = "SUV" };

            // Act
            Func<Task> act = async () => await _useCase.Execute(input);

            // Assert
            await act.Should().ThrowAsync<Exception>();
            _unitOfWorkMock.Verify(x => x.Rollback(), Times.Once);
        }
    }
}
