using ConnectVeiculos.Application.Exceptions;
using ConnectVeiculos.Application.InputModels.Auth;
using ConnectVeiculos.Application.UseCases.Auth;
using ConnectVeiculos.Core.Entities.Usuarios;
using ConnectVeiculos.Core.Interfaces.Database.Operations.Usuarios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases
{
    public class TrocarSenhaUseCaseTests
    {
        private readonly Mock<IUsuarioRepository> _repoMock = new();
        private readonly Mock<IUsuarioOperations> _opsMock = new();
        private readonly TrocarSenhaUseCase _useCase;

        private const string SenhaAtualPlain = "SenhaAtual123";
        private static readonly string SenhaAtualHash = BCrypt.Net.BCrypt.HashPassword(SenhaAtualPlain);

        public TrocarSenhaUseCaseTests()
        {
            _useCase = new TrocarSenhaUseCase(_repoMock.Object, _opsMock.Object);
        }

        private static Usuario UsuarioComSenha(int id, string senhaHash)
        {
            return new Usuario(id, "Joao", "", "", "joao@email.com", senhaHash, "Vendedor", true);
        }

        [Fact]
        public async Task Usuario_Inexistente_Lanca_InputModelException()
        {
            _repoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Usuario?)null!);

            var input = new TrocarSenhaInputModel
            {
                SenhaAtual = SenhaAtualPlain,
                NovaSenha = "NovaSenha456",
                ConfirmarSenha = "NovaSenha456"
            };

            var act = () => _useCase.ExecutarAsync(999, input);

            await act.Should().ThrowAsync<InputModelException>().WithMessage("*nao encontrado*");
            _opsMock.Verify(o => o.AtualizarSenhaAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Senha_Atual_Incorreta_Lanca_InputModelException()
        {
            var usuario = UsuarioComSenha(1, SenhaAtualHash);
            _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(usuario);

            var input = new TrocarSenhaInputModel
            {
                SenhaAtual = "SenhaErrada",
                NovaSenha = "NovaSenha456",
                ConfirmarSenha = "NovaSenha456"
            };

            var act = () => _useCase.ExecutarAsync(1, input);

            await act.Should().ThrowAsync<InputModelException>().WithMessage("*atual incorreta*");
            _opsMock.Verify(o => o.AtualizarSenhaAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Nova_Senha_Igual_A_Atual_Lanca_InputModelException()
        {
            var usuario = UsuarioComSenha(1, SenhaAtualHash);
            _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(usuario);

            var input = new TrocarSenhaInputModel
            {
                SenhaAtual = SenhaAtualPlain,
                NovaSenha = SenhaAtualPlain,
                ConfirmarSenha = SenhaAtualPlain
            };

            var act = () => _useCase.ExecutarAsync(1, input);

            await act.Should().ThrowAsync<InputModelException>().WithMessage("*diferente*");
            _opsMock.Verify(o => o.AtualizarSenhaAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Fluxo_Feliz_Atualiza_Senha_Com_Hash_BCrypt()
        {
            var usuario = UsuarioComSenha(1, SenhaAtualHash);
            _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(usuario);

            string? hashCapturado = null;
            _opsMock.Setup(o => o.AtualizarSenhaAsync(1, It.IsAny<string>()))
                .Callback<int, string>((_, hash) => hashCapturado = hash)
                .Returns(Task.CompletedTask);

            var input = new TrocarSenhaInputModel
            {
                SenhaAtual = SenhaAtualPlain,
                NovaSenha = "NovaSenhaSegura789",
                ConfirmarSenha = "NovaSenhaSegura789"
            };

            await _useCase.ExecutarAsync(1, input);

            _opsMock.Verify(o => o.AtualizarSenhaAsync(1, It.IsAny<string>()), Times.Once);
            hashCapturado.Should().NotBeNullOrEmpty();
            BCrypt.Net.BCrypt.Verify("NovaSenhaSegura789", hashCapturado).Should().BeTrue();
        }
    }
}
