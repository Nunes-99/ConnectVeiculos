using ConnectVeiculos.Application.InputModels.Usuarios;
using ConnectVeiculos.Application.UseCases.Usuarios;
using ConnectVeiculos.Core.Entities.Usuarios;
using ConnectVeiculos.Core.Interfaces.Database.Common;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.LojasUsuarios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Permissoes;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;
using ConnectVeiculos.Core.Interfaces.Email;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Core.Interfaces.Tenancy;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases.Usuarios
{
    public class CadastrarUsuarioUseCaseTests
    {
        private readonly Mock<IUsuarioRepository> _usuarioRepositoryMock;
        private readonly Mock<ILojaUsuarioRepository> _lojaUsuarioRepositoryMock;
        private readonly Mock<IPermissaoRepository> _permissaoRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<ITenantBackgroundRunner> _backgroundRunnerMock;
        private readonly CadastrarUsuarioUseCase _useCase;

        public CadastrarUsuarioUseCaseTests()
        {
            _usuarioRepositoryMock = new Mock<IUsuarioRepository>();
            _lojaUsuarioRepositoryMock = new Mock<ILojaUsuarioRepository>();
            _permissaoRepositoryMock = new Mock<IPermissaoRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _backgroundRunnerMock = new Mock<ITenantBackgroundRunner>();
            _useCase = new CadastrarUsuarioUseCase(
                _usuarioRepositoryMock.Object,
                _lojaUsuarioRepositoryMock.Object,
                _permissaoRepositoryMock.Object,
                 _unitOfWorkMock.Object,
                 new Mock<ILimiteService>().Object,
                _backgroundRunnerMock.Object);
        }

        /// <summary>
        /// A senha nao vem mais do formulario: quem cadastra nao pode conhecer a
        /// senha de outra pessoa. O sistema gera, manda por e-mail e exige a
        /// troca no primeiro acesso.
        /// </summary>
        [Fact]
        public async Task Execute_DeveIgnorarASenhaDoFormularioEExigirTroca()
        {
            Usuario? salvo = null;
            _usuarioRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Usuario>()))
                                  .Callback<Usuario>(u => salvo = u)
                                  .ReturnsAsync(1);

            var input = new UsuarioInputModel
            {
                UsuNome = "João Silva",
                UsuEmail = "joao@email.com",
                UsuSenha = "senha-digitada-pelo-admin",
                UsuSts = true
            };

            await _useCase.Execute(input);

            salvo.Should().NotBeNull();
            salvo!.UsuTrocarSenha.Should().BeTrue();
            BCrypt.Net.BCrypt.Verify("senha-digitada-pelo-admin", salvo.UsuSenha).Should().BeFalse();
        }

        [Fact]
        public async Task Execute_DeveEnviarASenhaPorEmail()
        {
            _usuarioRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Usuario>())).ReturnsAsync(1);

            await _useCase.Execute(new UsuarioInputModel
            {
                UsuNome = "João Silva",
                UsuEmail = "joao@email.com",
                UsuSenha = "x",
                UsuSts = true
            });

            _backgroundRunnerMock.Verify(
                x => x.Enqueue(It.IsAny<Func<IEmailService, Task>>(), It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task Execute_QuandoOEmailJaExiste_NaoDeveEnviarNada()
        {
            _usuarioRepositoryMock.Setup(x => x.GetByEmailAsync("joao@email.com"))
                                  .ReturnsAsync(new Usuario(1, "Outro", "", "", "joao@email.com", "hash", "Vendedor", true));

            var acao = async () => await _useCase.Execute(new UsuarioInputModel
            {
                UsuNome = "João Silva",
                UsuEmail = "joao@email.com",
                UsuSenha = "x",
                UsuSts = true
            });

            await acao.Should().ThrowAsync<ConnectVeiculos.Core.Exceptions.DomainException>();
            _backgroundRunnerMock.Verify(
                x => x.Enqueue(It.IsAny<Func<IEmailService, Task>>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task Execute_ComDadosValidos_DeveCadastrarUsuario()
        {
            // Arrange
            var input = new UsuarioInputModel
            {
                UsuNome = "João Silva",
                UsuEmail = "joao@email.com",
                UsuSenha = "Senha@123",
                UsuCPF = "12345678901",
                UsuRG = "123456789",
                UsuFuncao = "Vendedor",
                UsuSts = true
            };

            _usuarioRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Usuario>()))
                .ReturnsAsync(1);

            // Act
            var result = await _useCase.Execute(input);

            // Assert
            result.Should().Be(1);
            _usuarioRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<Usuario>()), Times.Once);
            _unitOfWorkMock.Verify(x => x.BeginTransaction(), Times.Once);
            _unitOfWorkMock.Verify(x => x.Commit(), Times.Once);
        }

        [Fact]
        public async Task Execute_ComErroNoRepositorio_DeveRollbackELancarExcecao()
        {
            // Arrange
            var input = new UsuarioInputModel
            {
                UsuNome = "João Silva",
                UsuEmail = "joao@email.com",
                UsuSenha = "Senha@123",
                UsuCPF = "12345678901",
                UsuRG = "123456789",
                UsuFuncao = "Vendedor",
                UsuSts = true
            };

            _usuarioRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Usuario>()))
                .ThrowsAsync(new Exception("Erro no banco de dados"));

            // Act
            Func<Task> act = async () => await _useCase.Execute(input);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("Erro no banco de dados");
            _unitOfWorkMock.Verify(x => x.Rollback(), Times.Once);
            _unitOfWorkMock.Verify(x => x.Commit(), Times.Never);
        }

        [Fact]
        public async Task Execute_DeveFazerHashDaSenha()
        {
            // Arrange
            var input = new UsuarioInputModel
            {
                UsuNome = "João Silva",
                UsuEmail = "joao@email.com",
                UsuSenha = "Senha@123",
                UsuCPF = "12345678901",
                UsuRG = "123456789",
                UsuFuncao = "Vendedor",
                UsuSts = true
            };

            Usuario usuarioCriado = null;
            _usuarioRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Usuario>()))
                .Callback<Usuario>(u => usuarioCriado = u)
                .ReturnsAsync(1);

            // Act
            await _useCase.Execute(input);

            // Assert
            usuarioCriado.Should().NotBeNull();
            // Nunca em texto puro, e nunca a que veio do formulario: a senha
            // gravada e a gerada pelo sistema, que so o proprio usuario recebe.
            usuarioCriado.UsuSenha.Should().NotBe("Senha@123");
            usuarioCriado.UsuSenha.Should().StartWith("$2");
            BCrypt.Net.BCrypt.Verify("Senha@123", usuarioCriado.UsuSenha).Should().BeFalse();
        }
    }
}
