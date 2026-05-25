using ConnectVeiculos.Core.Entities.Vendas;
using ConnectVeiculos.Core.Exceptions;
using FluentAssertions;
using Xunit;

namespace ConnectVeiculos.Tests.Entities
{
    public class VendaTests
    {
        [Fact]
        public void Criar_Venda_Com_Dados_Validos_Deve_Funcionar()
        {
            // Arrange & Act
            var venda = new Venda(
                1, 1, 1, DateTime.Now, "Toyota", "Corolla", 2023, "9BWZZZ377VT004251",
                85000m, 5m, 4250m, "Joao Silva", "12345678901", "11999999999",
                "joao@email.com", "Rua A, 123", "PIX", "Venda a vista");

            // Assert
            venda.VenId.Should().Be(1);
            venda.VenMarca.Should().Be("Toyota");
            venda.VenModelo.Should().Be("Corolla");
            venda.VenValor.Should().Be(85000m);
            venda.VenStatus.Should().Be("A");
            venda.VenCompradorNome.Should().Be("Joao Silva");
        }

        [Fact]
        public void Criar_Venda_Sem_Veiculo_Deve_Lancar_Excecao()
        {
            // Arrange & Act
            Action act = () => new Venda(
                1, 0, 1, DateTime.Now, "Toyota", "Corolla", 2023, "9BWZZZ377VT004251",
                85000m, 5m, 4250m, "Joao Silva");

            // Assert
            act.Should().Throw<DomainException>()
                .WithMessage("*veículo*obrigatório*");
        }

        [Fact]
        public void Criar_Venda_Sem_Vendedor_Deve_Lancar_Excecao()
        {
            // Arrange & Act
            Action act = () => new Venda(
                1, 1, 0, DateTime.Now, "Toyota", "Corolla", 2023, "9BWZZZ377VT004251",
                85000m, 5m, 4250m, "Joao Silva");

            // Assert
            act.Should().Throw<DomainException>()
                .WithMessage("*vendedor*obrigatório*");
        }

        [Fact]
        public void Criar_Venda_Sem_Comprador_Deve_Lancar_Excecao()
        {
            // Arrange & Act
            Action act = () => new Venda(
                1, 1, 1, DateTime.Now, "Toyota", "Corolla", 2023, "9BWZZZ377VT004251",
                85000m, 5m, 4250m, "");

            // Assert
            act.Should().Throw<DomainException>()
                .WithMessage("*comprador*obrigatório*");
        }

        [Fact]
        public void Criar_Venda_Com_Valor_Negativo_Deve_Lancar_Excecao()
        {
            // Arrange & Act
            Action act = () => new Venda(
                1, 1, 1, DateTime.Now, "Toyota", "Corolla", 2023, "9BWZZZ377VT004251",
                -1000m, 5m, 4250m, "Joao Silva");

            // Assert
            act.Should().Throw<DomainException>()
                .WithMessage("*valor*negativo*");
        }

        [Fact]
        public void Estornar_Venda_Ativa_Deve_Funcionar()
        {
            // Arrange
            var venda = new Venda(
                1, 1, 1, DateTime.Now, "Toyota", "Corolla", 2023, "9BWZZZ377VT004251",
                85000m, 5m, 4250m, "Joao Silva");

            // Act
            venda.Estornar();

            // Assert
            venda.VenStatus.Should().Be("E");
            venda.VenDtEstorno.Should().NotBeNull();
        }

        [Fact]
        public void Estornar_Venda_Ja_Estornada_Deve_Lancar_Excecao()
        {
            // Arrange
            var venda = new Venda(
                1, 1, 1, DateTime.Now, "Toyota", "Corolla", 2023, "9BWZZZ377VT004251",
                85000m, 5m, 4250m, "Joao Silva");
            venda.Estornar();

            // Act
            Action act = () => venda.Estornar();

            // Assert
            act.Should().Throw<DomainException>()
                .WithMessage("*ja foi estornada*");
        }
    }
}
