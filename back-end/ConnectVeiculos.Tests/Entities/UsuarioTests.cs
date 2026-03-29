using ConnectVeiculos.Core.Entities.Usuarios;
using ConnectVeiculos.Core.Exceptions;
using FluentAssertions;
using Xunit;

namespace ConnectVeiculos.Tests.Entities
{
    public class UsuarioTests
    {
        [Fact]
        public void Criar_Usuario_Com_Dados_Validos_Deve_Funcionar()
        {
            // Arrange & Act
            var usuario = new Usuario(
                1, "Joao Silva", "11999999999", "12345678901",
                "joao@email.com", "senha123", "Vendedor", true);

            // Assert
            usuario.UsuId.Should().Be(1);
            usuario.UsuNome.Should().Be("Joao Silva");
            usuario.UsuEmail.Should().Be("joao@email.com");
            usuario.UsuSts.Should().BeTrue();
        }

        [Fact]
        public void Criar_Usuario_Sem_Nome_Deve_Lancar_Excecao()
        {
            // Arrange & Act
            Action act = () => new Usuario(
                1, "", "11999999999", "12345678901",
                "joao@email.com", "senha123", "Vendedor", true);

            // Assert
            act.Should().Throw<UsuarioException>()
                .WithMessage("*nome*");
        }

        [Fact]
        public void Criar_Usuario_Sem_Email_Deve_Lancar_Excecao()
        {
            // Arrange & Act
            Action act = () => new Usuario(
                1, "Joao Silva", "11999999999", "12345678901",
                "", "senha123", "Vendedor", true);

            // Assert
            act.Should().Throw<UsuarioException>()
                .WithMessage("*e-mail*");
        }

        [Fact]
        public void Criar_Usuario_Sem_Senha_Deve_Lancar_Excecao()
        {
            // Arrange & Act
            Action act = () => new Usuario(
                1, "Joao Silva", "11999999999", "12345678901",
                "joao@email.com", "", "Vendedor", true);

            // Assert
            act.Should().Throw<UsuarioException>()
                .WithMessage("*senha*");
        }

        [Fact]
        public void Inativar_Usuario_Deve_Funcionar()
        {
            // Arrange
            var usuario = new Usuario(
                1, "Joao Silva", "11999999999", "12345678901",
                "joao@email.com", "senha123", "Vendedor", true);

            // Act
            usuario.AlterarStatus(false);

            // Assert
            usuario.UsuSts.Should().BeFalse();
        }

        [Fact]
        public void Ativar_Usuario_Deve_Funcionar()
        {
            // Arrange
            var usuario = new Usuario(
                1, "Joao Silva", "11999999999", "12345678901",
                "joao@email.com", "senha123", "Vendedor", false);

            // Act
            usuario.AlterarStatus(true);

            // Assert
            usuario.UsuSts.Should().BeTrue();
        }

        [Fact]
        public void Alterar_Senha_Deve_Funcionar()
        {
            // Arrange
            var usuario = new Usuario(
                1, "Joao Silva", "11999999999", "12345678901",
                "joao@email.com", "senha123", "Vendedor", true);

            // Act
            usuario.AlterarSenha("novaSenha456");

            // Assert
            usuario.UsuSenha.Should().Be("novaSenha456");
        }

        [Fact]
        public void Alterar_Senha_Vazia_Deve_Lancar_Excecao()
        {
            // Arrange
            var usuario = new Usuario(
                1, "Joao Silva", "11999999999", "12345678901",
                "joao@email.com", "senha123", "Vendedor", true);

            // Act
            Action act = () => usuario.AlterarSenha("");

            // Assert
            act.Should().Throw<UsuarioException>()
                .WithMessage("*senha*");
        }
    }
}
