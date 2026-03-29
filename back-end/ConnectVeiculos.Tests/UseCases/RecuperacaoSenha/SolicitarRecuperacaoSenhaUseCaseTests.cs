using ConnectVeiculos.Application.Exceptions;
using ConnectVeiculos.Application.InputModels.RecuperacaoSenha;
using ConnectVeiculos.Application.UseCases.RecuperacaoSenha;
using ConnectVeiculos.Core.Entities.Usuarios;
using ConnectVeiculos.Core.Interfaces.Database.Operations.RecuperacaoSenha;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases.RecuperacaoSenha
{
    public class SolicitarRecuperacaoSenhaUseCaseTests
    {
        private readonly Mock<IUsuarioRepository> _usuarioRepositoryMock;
        private readonly Mock<IRecuperacaoSenhaOperations> _recuperacaoOperationsMock;
        private readonly SolicitarRecuperacaoSenhaUseCase _useCase;

        public SolicitarRecuperacaoSenhaUseCaseTests()
        {
            _usuarioRepositoryMock = new Mock<IUsuarioRepository>();
            _recuperacaoOperationsMock = new Mock<IRecuperacaoSenhaOperations>();
            _useCase = new SolicitarRecuperacaoSenhaUseCase(
                _usuarioRepositoryMock.Object,
                _recuperacaoOperationsMock.Object);
        }

        [Fact]
        public async Task ExecutarAsync_ComEmailValido_DeveRetornarToken()
        {
            // Arrange
            var usuario = new Usuario(1, "João Silva", "12345678901", "123456789",
                "joao@email.com", "senhaHash", "Vendedor", true);

            _usuarioRepositoryMock.Setup(x => x.GetByEmailAsync("joao@email.com"))
                .ReturnsAsync(usuario);

            var input = new SolicitarRecuperacaoInputModel { Email = "joao@email.com" };

            // Act
            var result = await _useCase.ExecutarAsync(input);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Length.Should().Be(64); // 2 GUIDs sem hífens = 64 caracteres
            _recuperacaoOperationsMock.Verify(x => x.InvalidarTokensAnterioresAsync(1), Times.Once);
            _recuperacaoOperationsMock.Verify(x => x.InserirAsync(It.IsAny<Core.Entities.RecuperacaoSenha.RecuperacaoSenha>()), Times.Once);
        }

        [Fact]
        public async Task ExecutarAsync_ComEmailInexistente_DeveLancarExcecao()
        {
            // Arrange
            _usuarioRepositoryMock.Setup(x => x.GetByEmailAsync("naoexiste@email.com"))
                .ReturnsAsync((Usuario)null);

            var input = new SolicitarRecuperacaoInputModel { Email = "naoexiste@email.com" };

            // Act
            Func<Task> act = async () => await _useCase.ExecutarAsync(input);

            // Assert
            await act.Should().ThrowAsync<InputModelException>()
                .WithMessage("Se o e-mail estiver cadastrado, voce recebera as instrucoes para recuperacao.");
        }

        [Fact]
        public async Task ExecutarAsync_ComUsuarioInativo_DeveLancarExcecao()
        {
            // Arrange
            var usuarioInativo = new Usuario(1, "João Silva", "12345678901", "123456789",
                "joao@email.com", "senhaHash", "Vendedor", false); // Inativo

            _usuarioRepositoryMock.Setup(x => x.GetByEmailAsync("joao@email.com"))
                .ReturnsAsync(usuarioInativo);

            var input = new SolicitarRecuperacaoInputModel { Email = "joao@email.com" };

            // Act
            Func<Task> act = async () => await _useCase.ExecutarAsync(input);

            // Assert
            await act.Should().ThrowAsync<InputModelException>()
                .WithMessage("Usuario inativo. Entre em contato com o administrador.");
        }

        [Fact]
        public async Task ExecutarAsync_DeveInvalidarTokensAnteriores()
        {
            // Arrange
            var usuario = new Usuario(1, "João Silva", "12345678901", "123456789",
                "joao@email.com", "senhaHash", "Vendedor", true);

            _usuarioRepositoryMock.Setup(x => x.GetByEmailAsync("joao@email.com"))
                .ReturnsAsync(usuario);

            var input = new SolicitarRecuperacaoInputModel { Email = "joao@email.com" };

            // Act
            await _useCase.ExecutarAsync(input);

            // Assert
            _recuperacaoOperationsMock.Verify(x => x.InvalidarTokensAnterioresAsync(1), Times.Once);
        }
    }
}
