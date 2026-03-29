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
    public class CadastrarCategoriaUseCaseTests
    {
        private readonly Mock<ICategoriaRepository> _categoriaRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly CadastrarCategoriaUseCase _useCase;

        public CadastrarCategoriaUseCaseTests()
        {
            _categoriaRepositoryMock = new Mock<ICategoriaRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _useCase = new CadastrarCategoriaUseCase(_categoriaRepositoryMock.Object, _unitOfWorkMock.Object);
        }

        [Fact]
        public async Task Execute_ComDadosValidos_DeveCadastrarCategoria()
        {
            // Arrange
            var input = new CategoriaInputModel
            {
                CatNome = "SUV",
                CatDesc = "Sport Utility Vehicle",
                CatSts = true
            };

            _categoriaRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Categoria>()))
                .ReturnsAsync(1);

            // Act
            var result = await _useCase.Execute(input);

            // Assert
            result.Should().Be(1);
            _categoriaRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<Categoria>()), Times.Once);
            _unitOfWorkMock.Verify(x => x.Commit(), Times.Once);
        }

        [Fact]
        public async Task Execute_ComErroNoRepositorio_DeveRollback()
        {
            // Arrange
            var input = new CategoriaInputModel
            {
                CatNome = "Sedan",
                CatDesc = "Veículos Sedan"
            };

            _categoriaRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Categoria>()))
                .ThrowsAsync(new Exception("Erro no banco"));

            // Act
            Func<Task> act = async () => await _useCase.Execute(input);

            // Assert
            await act.Should().ThrowAsync<Exception>();
            _unitOfWorkMock.Verify(x => x.Rollback(), Times.Once);
        }

        [Fact]
        public async Task Execute_DeveCriarCategoriaComDadosCorretos()
        {
            // Arrange
            var input = new CategoriaInputModel
            {
                CatNome = "Hatch",
                CatDesc = "Veículos compactos",
                CatSts = true
            };

            Categoria categoriaCriada = null;
            _categoriaRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Categoria>()))
                .Callback<Categoria>(c => categoriaCriada = c)
                .ReturnsAsync(1);

            // Act
            await _useCase.Execute(input);

            // Assert
            categoriaCriada.Should().NotBeNull();
            categoriaCriada.CatNome.Should().Be("Hatch");
            categoriaCriada.CatDesc.Should().Be("Veículos compactos");
        }
    }
}
