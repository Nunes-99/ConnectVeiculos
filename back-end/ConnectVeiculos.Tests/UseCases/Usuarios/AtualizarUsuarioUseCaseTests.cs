using ConnectVeiculos.Application.InputModels.Usuarios;
using ConnectVeiculos.Application.UseCases.Usuarios;
using ConnectVeiculos.Core.Entities.Usuarios;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.LojasUsuarios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Permissoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases.Usuarios
{
    public class AtualizarUsuarioUseCaseTests
    {
        private readonly Mock<IUsuarioRepository> _usuarioRepositoryMock;
        private readonly Mock<ILojaUsuarioRepository> _lojaUsuarioRepositoryMock;
        private readonly Mock<IPermissaoRepository> _permissaoRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly AtualizarUsuarioUseCase _useCase;

        public AtualizarUsuarioUseCaseTests()
        {
            _usuarioRepositoryMock = new Mock<IUsuarioRepository>();
            _lojaUsuarioRepositoryMock = new Mock<ILojaUsuarioRepository>();
            _permissaoRepositoryMock = new Mock<IPermissaoRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _useCase = new AtualizarUsuarioUseCase(
                _usuarioRepositoryMock.Object,
                _lojaUsuarioRepositoryMock.Object,
                _permissaoRepositoryMock.Object,
                _unitOfWorkMock.Object);
        }

        [Fact]
        public async Task Execute_ComDadosValidos_DeveAtualizarUsuario()
        {
            // Arrange
            var usuarioExistente = new Usuario(1, "João Silva", "12345678901", "123456789",
                "joao@email.com", "senhaHash", "Vendedor", true);

            _usuarioRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(usuarioExistente);

            var input = new UsuarioInputModel
            {
                UsuId = 1,
                UsuNome = "João Silva Atualizado",
                UsuEmail = "joao.atualizado@email.com",
                UsuCPF = "12345678901",
                UsuRG = "123456789",
                UsuFuncao = "Gerente",
                UsuSts = true
            };

            // Act
            await _useCase.Execute(input);

            // Assert
            _usuarioRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Usuario>()), Times.Once);
            _unitOfWorkMock.Verify(x => x.Commit(), Times.Once);
        }

        [Fact]
        public async Task Execute_ComUsuarioInexistente_DeveLancarExcecao()
        {
            // Arrange
            _usuarioRepositoryMock.Setup(x => x.GetByIdAsync(999))
                .ReturnsAsync((Usuario)null);

            var input = new UsuarioInputModel
            {
                UsuId = 999,
                UsuNome = "Usuário Inexistente"
            };

            // Act
            Func<Task> act = async () => await _useCase.Execute(input);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("Usuario nao encontrado.");
        }

        [Fact]
        public async Task Execute_ComNovaSenha_DeveFazerHashDaSenha()
        {
            // Arrange
            var usuarioExistente = new Usuario(1, "João Silva", "12345678901", "123456789",
                "joao@email.com", "senhaAntigaHash", "Vendedor", true);

            _usuarioRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(usuarioExistente);

            Usuario usuarioAtualizado = null;
            _usuarioRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Usuario>()))
                .Callback<Usuario>(u => usuarioAtualizado = u)
                .Returns(Task.CompletedTask);

            var input = new UsuarioInputModel
            {
                UsuId = 1,
                UsuNome = "João Silva",
                UsuEmail = "joao@email.com",
                UsuSenha = "NovaSenha@123",
                UsuCPF = "12345678901",
                UsuRG = "123456789",
                UsuFuncao = "Vendedor",
                UsuSts = true
            };

            // Act
            await _useCase.Execute(input);

            // Assert
            usuarioAtualizado.Should().NotBeNull();
            usuarioAtualizado.UsuSenha.Should().NotBe("senhaAntigaHash");
            BCrypt.Net.BCrypt.Verify("NovaSenha@123", usuarioAtualizado.UsuSenha).Should().BeTrue();
        }

        [Fact]
        public async Task Execute_SemNovaSenha_DeveManterSenhaAnterior()
        {
            // Arrange
            var senhaAntigaHash = BCrypt.Net.BCrypt.HashPassword("SenhaAntiga");
            var usuarioExistente = new Usuario(1, "João Silva", "12345678901", "123456789",
                "joao@email.com", senhaAntigaHash, "Vendedor", true);

            _usuarioRepositoryMock.Setup(x => x.GetByIdAsync(1))
                .ReturnsAsync(usuarioExistente);

            Usuario usuarioAtualizado = null;
            _usuarioRepositoryMock.Setup(x => x.UpdateAsync(It.IsAny<Usuario>()))
                .Callback<Usuario>(u => usuarioAtualizado = u)
                .Returns(Task.CompletedTask);

            var input = new UsuarioInputModel
            {
                UsuId = 1,
                UsuNome = "João Silva Atualizado",
                UsuEmail = "joao@email.com",
                UsuSenha = "", // Sem nova senha
                UsuCPF = "12345678901",
                UsuRG = "123456789",
                UsuFuncao = "Vendedor",
                UsuSts = true
            };

            // Act
            await _useCase.Execute(input);

            // Assert
            usuarioAtualizado.Should().NotBeNull();
            usuarioAtualizado.UsuSenha.Should().Be(senhaAntigaHash);
        }
    }
}
