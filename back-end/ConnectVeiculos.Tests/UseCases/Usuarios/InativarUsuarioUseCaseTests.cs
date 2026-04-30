using ConnectVeiculos.Application.UseCases.Usuarios;
using ConnectVeiculos.Core.Entities.Usuarios;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases.Usuarios
{
    public class InativarUsuarioUseCaseTests
    {
        private readonly Mock<IUsuarioRepository> _repoMock = new();
        private readonly Mock<IUnitOfWork> _uowMock = new();
        private readonly InativarUsuarioUseCase _useCase;

        public InativarUsuarioUseCaseTests()
        {
            _useCase = new InativarUsuarioUseCase(_repoMock.Object, _uowMock.Object);
        }

        [Fact]
        public async Task Execute_ComIdValido_DeveExcluirUsuario()
        {
            var usuario = new Usuario(1, "Joao Silva", "12345678901", "123456789",
                "joao@email.com", "senhaHash", "Vendedor", true);
            _repoMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(usuario);

            await _useCase.Execute(1);

            _repoMock.Verify(x => x.DeleteAsync(1), Times.Once);
            _uowMock.Verify(x => x.Commit(), Times.Once);
        }

        [Fact]
        public async Task Execute_ComIdInexistente_DeveLancarExcecao()
        {
            _repoMock.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((Usuario)null!);

            Func<Task> act = async () => await _useCase.Execute(999);

            await act.Should().ThrowAsync<Exception>().WithMessage("*Usu*rio*");
            _repoMock.Verify(x => x.DeleteAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Execute_ComErroNoRepositorio_DeveRollback()
        {
            var usuario = new Usuario(1, "X", "12345678901", "123",
                "x@x.com", "h", "V", true);
            _repoMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(usuario);
            _repoMock.Setup(x => x.DeleteAsync(1)).ThrowsAsync(new Exception("Erro"));

            Func<Task> act = async () => await _useCase.Execute(1);

            await act.Should().ThrowAsync<Exception>();
            _uowMock.Verify(x => x.Rollback(), Times.Once);
        }
    }
}
