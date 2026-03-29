using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Exceptions;
using FluentAssertions;
using Xunit;

namespace ConnectVeiculos.Tests.Entities
{
    public class VeiculoTests
    {
        [Fact]
        public void Criar_Veiculo_Com_Dados_Validos_Deve_Funcionar()
        {
            // Arrange & Act
            var veiculo = new Veiculo(
                1, 1, 1, "Toyota", "Corolla", 2023, "ABC1234", "9BWZZZ377VT004251",
                "Prata", 15000, 85000m, DateTime.Now, "D", "A", 75000m);

            // Assert
            veiculo.VeiId.Should().Be(1);
            veiculo.VeiMarca.Should().Be("Toyota");
            veiculo.VeiModelo.Should().Be("Corolla");
            veiculo.VeiAno.Should().Be(2023);
            veiculo.VeiSts.Should().Be("D");
        }

        [Fact]
        public void Criar_Veiculo_Sem_Loja_Deve_Lancar_Excecao()
        {
            // Arrange & Act
            Action act = () => new Veiculo(
                1, 0, 1, "Toyota", "Corolla", 2023, "ABC1234", "9BWZZZ377VT004251",
                "Prata", 15000, 85000m, DateTime.Now, "D", "A", 75000m);

            // Assert
            act.Should().Throw<VeiculoException>()
                .WithMessage("*loja*obrigatória*");
        }

        [Fact]
        public void Criar_Veiculo_Sem_Categoria_Deve_Lancar_Excecao()
        {
            // Arrange & Act
            Action act = () => new Veiculo(
                1, 1, 0, "Toyota", "Corolla", 2023, "ABC1234", "9BWZZZ377VT004251",
                "Prata", 15000, 85000m, DateTime.Now, "D", "A", 75000m);

            // Assert
            act.Should().Throw<VeiculoException>()
                .WithMessage("*categoria*obrigatória*");
        }

        [Fact]
        public void Alterar_Status_Veiculo_Deve_Funcionar()
        {
            // Arrange
            var veiculo = new Veiculo(
                1, 1, 1, "Toyota", "Corolla", 2023, "ABC1234", "9BWZZZ377VT004251",
                "Prata", 15000, 85000m, DateTime.Now, "D", "A", 75000m);

            // Act
            veiculo.AlterarStatus("V");

            // Assert
            veiculo.VeiSts.Should().Be("V");
        }

        [Fact]
        public void Criar_Veiculo_Com_Marca_Muito_Longa_Deve_Lancar_Excecao()
        {
            // Arrange
            var marcaLonga = new string('A', 101);

            // Act
            Action act = () => new Veiculo(
                1, 1, 1, marcaLonga, "Corolla", 2023, "ABC1234", "9BWZZZ377VT004251",
                "Prata", 15000, 85000m, DateTime.Now, "D", "A", 75000m);

            // Assert
            act.Should().Throw<VeiculoException>()
                .WithMessage("*marca*100 caracteres*");
        }
    }
}
