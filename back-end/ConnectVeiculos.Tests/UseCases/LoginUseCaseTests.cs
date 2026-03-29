using ConnectVeiculos.Application.InputModels.Auth;
using ConnectVeiculos.Application.UseCases.Auth;
using ConnectVeiculos.Core.Entities.Usuarios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases
{
    public class LoginUseCaseTests
    {
        private readonly Mock<IUsuarioRepository> _usuarioRepositoryMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly LoginUseCase _loginUseCase;

        public LoginUseCaseTests()
        {
            _usuarioRepositoryMock = new Mock<IUsuarioRepository>();
            _configurationMock = new Mock<IConfiguration>();

            // Setup JWT configuration
            var jwtSectionMock = new Mock<IConfigurationSection>();
            jwtSectionMock.Setup(x => x["SecretKey"]).Returns("ChaveSecretaConnectVeiculos2024!@#$%MuitoLongaParaSerSegura");
            jwtSectionMock.Setup(x => x["Issuer"]).Returns("ConnectVeiculos");
            jwtSectionMock.Setup(x => x["Audience"]).Returns("ConnectVeiculosApp");
            jwtSectionMock.Setup(x => x["ExpirationInHours"]).Returns("8");
            _configurationMock.Setup(x => x.GetSection("JwtSettings")).Returns(jwtSectionMock.Object);

            _loginUseCase = new LoginUseCase(_usuarioRepositoryMock.Object, _configurationMock.Object);
        }

        [Fact]
        public async Task Login_Com_Credenciais_Validas_Deve_Retornar_Token()
        {
            // Arrange
            var senhaHash = BCrypt.Net.BCrypt.HashPassword("senha123");
            var usuario = new Usuario(1, "Joao Silva", "11999999999", "12345678901",
                "joao@email.com", senhaHash, "Vendedor", true);

            _usuarioRepositoryMock.Setup(x => x.GetByEmailAsync("joao@email.com"))
                .ReturnsAsync(usuario);

            var input = new LoginInputModel { Email = "joao@email.com", Senha = "senha123" };

            // Act
            var result = await _loginUseCase.Execute(input);

            // Assert
            result.Should().NotBeNull();
            result!.Token.Should().NotBeNullOrEmpty();
            result.UsuNome.Should().Be("Joao Silva");
        }

        [Fact]
        public async Task Login_Com_Email_Inexistente_Deve_Retornar_Null()
        {
            // Arrange
            _usuarioRepositoryMock.Setup(x => x.GetByEmailAsync("naoexiste@email.com"))
                .ReturnsAsync((Usuario)null);

            var input = new LoginInputModel { Email = "naoexiste@email.com", Senha = "senha123" };

            // Act
            var result = await _loginUseCase.Execute(input);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task Login_Com_Senha_Incorreta_Deve_Retornar_Null()
        {
            // Arrange
            var senhaHash = BCrypt.Net.BCrypt.HashPassword("senha123");
            var usuario = new Usuario(1, "Joao Silva", "11999999999", "12345678901",
                "joao@email.com", senhaHash, "Vendedor", true);

            _usuarioRepositoryMock.Setup(x => x.GetByEmailAsync("joao@email.com"))
                .ReturnsAsync(usuario);

            var input = new LoginInputModel { Email = "joao@email.com", Senha = "senhaErrada" };

            // Act
            var result = await _loginUseCase.Execute(input);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task Login_Com_Usuario_Inativo_Deve_Retornar_Null()
        {
            // Arrange
            var senhaHash = BCrypt.Net.BCrypt.HashPassword("senha123");
            var usuario = new Usuario(1, "Joao Silva", "11999999999", "12345678901",
                "joao@email.com", senhaHash, "Vendedor", false); // Usuario inativo

            _usuarioRepositoryMock.Setup(x => x.GetByEmailAsync("joao@email.com"))
                .ReturnsAsync(usuario);

            var input = new LoginInputModel { Email = "joao@email.com", Senha = "senha123" };

            // Act
            var result = await _loginUseCase.Execute(input);

            // Assert
            result.Should().BeNull();
        }
    }
}
