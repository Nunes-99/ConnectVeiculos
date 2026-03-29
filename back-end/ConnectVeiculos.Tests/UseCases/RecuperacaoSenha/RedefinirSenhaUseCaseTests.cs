using ConnectVeiculos.Application.Exceptions;
using ConnectVeiculos.Application.InputModels.RecuperacaoSenha;
using ConnectVeiculos.Application.UseCases.RecuperacaoSenha;
using ConnectVeiculos.Core.Interfaces.Database.Operations.RecuperacaoSenha;
using ConnectVeiculos.Core.Interfaces.Database.Operations.Usuarios;
using FluentAssertions;
using Moq;
using Xunit;
using RecuperacaoSenhaEntity = ConnectVeiculos.Core.Entities.RecuperacaoSenha.RecuperacaoSenha;

namespace ConnectVeiculos.Tests.UseCases.RecuperacaoSenha
{
    public class RedefinirSenhaUseCaseTests
    {
        private readonly Mock<IRecuperacaoSenhaOperations> _recuperacaoOperationsMock;
        private readonly Mock<IUsuarioOperations> _usuarioOperationsMock;
        private readonly RedefinirSenhaUseCase _useCase;

        public RedefinirSenhaUseCaseTests()
        {
            _recuperacaoOperationsMock = new Mock<IRecuperacaoSenhaOperations>();
            _usuarioOperationsMock = new Mock<IUsuarioOperations>();
            _useCase = new RedefinirSenhaUseCase(
                _recuperacaoOperationsMock.Object,
                _usuarioOperationsMock.Object);
        }

        [Fact]
        public async Task ExecutarAsync_ComTokenValido_DeveRedefinirSenha()
        {
            // Arrange
            var recuperacao = new RecuperacaoSenhaEntity(1, "token123", DateTime.Now.AddHours(1));

            _recuperacaoOperationsMock.Setup(x => x.ObterPorTokenAsync("token123"))
                .ReturnsAsync(recuperacao);

            var input = new RedefinirSenhaInputModel
            {
                Token = "token123",
                NovaSenha = "NovaSenha@123",
                ConfirmarSenha = "NovaSenha@123"
            };

            // Act
            await _useCase.ExecutarAsync(input);

            // Assert
            _usuarioOperationsMock.Verify(x => x.AtualizarSenhaAsync(1, It.IsAny<string>()), Times.Once);
            _recuperacaoOperationsMock.Verify(x => x.AtualizarAsync(It.IsAny<RecuperacaoSenhaEntity>()), Times.Once);
        }

        [Fact]
        public async Task ExecutarAsync_ComTokenInexistente_DeveLancarExcecao()
        {
            // Arrange
            _recuperacaoOperationsMock.Setup(x => x.ObterPorTokenAsync("tokenInvalido"))
                .ReturnsAsync((RecuperacaoSenhaEntity)null);

            var input = new RedefinirSenhaInputModel
            {
                Token = "tokenInvalido",
                NovaSenha = "NovaSenha@123"
            };

            // Act
            Func<Task> act = async () => await _useCase.ExecutarAsync(input);

            // Assert
            await act.Should().ThrowAsync<InputModelException>()
                .WithMessage("Token invalido ou expirado.");
        }

        [Fact]
        public async Task ExecutarAsync_ComTokenExpirado_DeveLancarExcecao()
        {
            // Arrange
            var recuperacaoExpirada = new RecuperacaoSenhaEntity(1, "tokenExpirado", DateTime.Now.AddHours(-1));

            _recuperacaoOperationsMock.Setup(x => x.ObterPorTokenAsync("tokenExpirado"))
                .ReturnsAsync(recuperacaoExpirada);

            var input = new RedefinirSenhaInputModel
            {
                Token = "tokenExpirado",
                NovaSenha = "NovaSenha@123"
            };

            // Act
            Func<Task> act = async () => await _useCase.ExecutarAsync(input);

            // Assert
            await act.Should().ThrowAsync<InputModelException>()
                .WithMessage("Token invalido ou expirado.");
        }

        [Fact]
        public async Task ExecutarAsync_DeveFazerHashDaNovaSenha()
        {
            // Arrange
            var recuperacao = new RecuperacaoSenhaEntity(1, "token123", DateTime.Now.AddHours(1));

            _recuperacaoOperationsMock.Setup(x => x.ObterPorTokenAsync("token123"))
                .ReturnsAsync(recuperacao);

            string senhaHashCapturada = null;
            _usuarioOperationsMock.Setup(x => x.AtualizarSenhaAsync(1, It.IsAny<string>()))
                .Callback<int, string>((id, senha) => senhaHashCapturada = senha)
                .Returns(Task.CompletedTask);

            var input = new RedefinirSenhaInputModel
            {
                Token = "token123",
                NovaSenha = "NovaSenha@123"
            };

            // Act
            await _useCase.ExecutarAsync(input);

            // Assert
            senhaHashCapturada.Should().NotBe("NovaSenha@123");
            BCrypt.Net.BCrypt.Verify("NovaSenha@123", senhaHashCapturada).Should().BeTrue();
        }
    }
}
