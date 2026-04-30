using ConnectVeiculos.Application.InputModels.RecuperacaoSenha;
using ConnectVeiculos.Application.UseCases.RecuperacaoSenha;
using ConnectVeiculos.Core.Entities.Usuarios;
using ConnectVeiculos.Core.Interfaces.Database.Operations.RecuperacaoSenha;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;
using ConnectVeiculos.Core.Interfaces.Email;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases.RecuperacaoSenha
{
    public class SolicitarRecuperacaoSenhaUseCaseTests
    {
        private readonly Mock<IUsuarioRepository> _usuarioRepositoryMock;
        private readonly Mock<IRecuperacaoSenhaOperations> _recuperacaoOperationsMock;
        private readonly Mock<IEmailService> _emailServiceMock;
        private readonly SolicitarRecuperacaoSenhaUseCase _useCase;

        public SolicitarRecuperacaoSenhaUseCaseTests()
        {
            _usuarioRepositoryMock = new Mock<IUsuarioRepository>();
            _recuperacaoOperationsMock = new Mock<IRecuperacaoSenhaOperations>();
            _emailServiceMock = new Mock<IEmailService>();
            _useCase = new SolicitarRecuperacaoSenhaUseCase(
                _usuarioRepositoryMock.Object,
                _recuperacaoOperationsMock.Object,
                _emailServiceMock.Object);
        }

        [Fact]
        public async Task ExecutarAsync_ComEmailValido_DeveRetornarTokenEEnviarEmail()
        {
            var usuario = new Usuario(1, "Joao Silva", "12345678901", "123456789",
                "joao@email.com", "senhaHash", "Vendedor", true);

            _usuarioRepositoryMock.Setup(x => x.GetByEmailAsync("joao@email.com"))
                .ReturnsAsync(usuario);

            var input = new SolicitarRecuperacaoInputModel { Email = "joao@email.com" };

            var result = await _useCase.ExecutarAsync(input);

            result.Should().NotBeNullOrEmpty();
            result!.Length.Should().Be(64);
            _recuperacaoOperationsMock.Verify(x => x.InvalidarTokensAnterioresAsync(1), Times.Once);
            _recuperacaoOperationsMock.Verify(x => x.InserirAsync(It.IsAny<Core.Entities.RecuperacaoSenha.RecuperacaoSenha>()), Times.Once);
            _emailServiceMock.Verify(x => x.SendRecuperacaoSenhaAsync("joao@email.com", "Joao Silva", It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task ExecutarAsync_ComEmailInexistente_DeveRetornarNullSemEnviarEmail()
        {
            _usuarioRepositoryMock.Setup(x => x.GetByEmailAsync("naoexiste@email.com"))
                .ReturnsAsync((Usuario?)null);

            var input = new SolicitarRecuperacaoInputModel { Email = "naoexiste@email.com" };

            var result = await _useCase.ExecutarAsync(input);

            result.Should().BeNull();
            _recuperacaoOperationsMock.Verify(x => x.InvalidarTokensAnterioresAsync(It.IsAny<int>()), Times.Never);
            _emailServiceMock.Verify(x => x.SendRecuperacaoSenhaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ExecutarAsync_ComUsuarioInativo_DeveRetornarNullSemEnviarEmail()
        {
            var usuarioInativo = new Usuario(1, "Joao Silva", "12345678901", "123456789",
                "joao@email.com", "senhaHash", "Vendedor", false);

            _usuarioRepositoryMock.Setup(x => x.GetByEmailAsync("joao@email.com"))
                .ReturnsAsync(usuarioInativo);

            var input = new SolicitarRecuperacaoInputModel { Email = "joao@email.com" };

            var result = await _useCase.ExecutarAsync(input);

            result.Should().BeNull();
            _emailServiceMock.Verify(x => x.SendRecuperacaoSenhaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}
