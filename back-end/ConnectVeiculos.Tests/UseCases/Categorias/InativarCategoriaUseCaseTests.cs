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
        private readonly Mock<ICategoriaRepository> _repoMock = new();
        private readonly Mock<IUnitOfWork> _uowMock = new();
        private readonly InativarCategoriaUseCase _useCase;

        public InativarCategoriaUseCaseTests()
        {
            _useCase = new InativarCategoriaUseCase(_repoMock.Object, _uowMock.Object);
        }

        [Fact]
        public async Task Execute_ComIdValido_DeveExcluirCategoria()
        {
            var cat = new Categoria(1, "SUV", "Sport Utility Vehicle", true);
            _repoMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(cat);

            await _useCase.Execute(1);

            _repoMock.Verify(x => x.DeleteAsync(1), Times.Once);
            _uowMock.Verify(x => x.Commit(), Times.Once);
        }

        [Fact]
        public async Task Execute_ComIdInexistente_DeveLancarExcecao()
        {
            _repoMock.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((Categoria)null!);

            Func<Task> act = async () => await _useCase.Execute(999);

            await act.Should().ThrowAsync<Exception>().WithMessage("*Categoria nao encontrada*");
            _repoMock.Verify(x => x.DeleteAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Execute_ComErroNoRepositorio_DeveRollback()
        {
            var cat = new Categoria(1, "X", "Y", true);
            _repoMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(cat);
            _repoMock.Setup(x => x.DeleteAsync(1)).ThrowsAsync(new Exception("Erro"));

            Func<Task> act = async () => await _useCase.Execute(1);

            await act.Should().ThrowAsync<Exception>();
            _uowMock.Verify(x => x.Rollback(), Times.Once);
        }
    }
}
