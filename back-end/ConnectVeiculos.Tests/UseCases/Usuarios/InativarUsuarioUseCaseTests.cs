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
        private readonly Mock<IUsuarioRepository> _usuarioRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly InativarUsuarioUseCase _useCase;

        public InativarUsuarioUseCaseTests()
        {
            _usuarioRepositoryMock = new Mock<IUsuarioRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _useCase = new InativarUsuarioUseCase(_usuarioRepositoryMock.Object, _unitOfWorkMock.Object);
        }

        [Fact]
        public async Task Execute_ComIdValido_DeveInativarUsuario()
        {
            // Arrange
            var usuario = new Usuario(1, "João Silva", "12345678901", "123456789",
                "joao@email.com", "senhaHash", "Vendedor", true);

            _usuarioRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(usuario);

            Usuario usuarioAtualizado = null;
            _usuarioRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Usuario>()))
                .Callback<Usuario>(u => usuarioAtualizado = u)
                .Returns(Task.CompletedTask);

            // Act
            await _useCase.Execute(1);

            // Assert
            usuarioAtualizado.Should().NotBeNull();
            usuarioAtualizado.UsuSts.Should().BeFalse();
            _unitOfWorkMock.Verify(x => x.Commit(), Times.Once);
        }

        [Fact]
        public async Task Execute_ComIdInexistente_DeveLancarExcecao()
        {
            // Arrange
            _usuarioRepositoryMock.Setup(x => x.GetByIdAsync(999))
                .ReturnsAsync((Usuario)null);

            // Act
            Func<Task> act = async () => await _useCase.Execute(999);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("Usuario nao encontrado.");
        }

        [Fact]
        public async Task Execute_ComErroNoRepositorio_DeveRollback()
        {
            // Arrange
            var usuario = new Usuario(1, "João Silva", "12345678901", "123456789",
                "joao@email.com", "senhaHash", "Vendedor", true);

            _usuarioRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(usuario);

            _usuarioRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Usuario>()))
                .ThrowsAsync(new Exception("Erro no banco"));

            // Act
            Func<Task> act = async () => await _useCase.Execute(1);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("Erro no banco");
            _unitOfWorkMock.Verify(x => x.Rollback(), Times.Once);
        }
    }
}
