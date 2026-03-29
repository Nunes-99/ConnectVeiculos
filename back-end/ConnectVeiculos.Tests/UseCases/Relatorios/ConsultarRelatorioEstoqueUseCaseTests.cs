using ConnectVeiculos.Application.UseCases.Relatorios;
using ConnectVeiculos.Core.Entities.Categorias;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Categorias;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases.Relatorios
{
    public class ConsultarRelatorioEstoqueUseCaseTests
    {
        private readonly Mock<IVeiculoRepository> _veiculoRepositoryMock;
        private readonly Mock<ILojaRepository> _lojaRepositoryMock;
        private readonly Mock<ICategoriaRepository> _categoriaRepositoryMock;
        private readonly ConsultarRelatorioEstoqueUseCase _useCase;

        public ConsultarRelatorioEstoqueUseCaseTests()
        {
            _veiculoRepositoryMock = new Mock<IVeiculoRepository>();
            _lojaRepositoryMock = new Mock<ILojaRepository>();
            _categoriaRepositoryMock = new Mock<ICategoriaRepository>();
            _useCase = new ConsultarRelatorioEstoqueUseCase(
                _veiculoRepositoryMock.Object,
                _lojaRepositoryMock.Object,
                _categoriaRepositoryMock.Object);
        }

        private static Loja CriarLoja(int id, string nome)
        {
            return new Loja(id, nome, "Rua A", "123", "Centro", "SP", "SP", "12345678", "", "loja@email.com", "11999999999", "", "11999999999", "", "12345678901234", "", true);
        }

        [Fact]
        public async Task Execute_DeveRetornarContadoresCorretos()
        {
            // Arrange
            var veiculos = new List<Veiculo>
            {
                new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023, "ABC1234", "CHASSI1", "Prata", 10000, 80000m, DateTime.Now, "D", "A", 70000m),
                new Veiculo(2, 1, 1, "Honda", "Civic", 2022, "DEF5678", "CHASSI2", "Preto", 20000, 90000m, DateTime.Now, "D", "A", 75000m),
                new Veiculo(3, 1, 2, "Ford", "Focus", 2021, "GHI9012", "CHASSI3", "Branco", 30000, 70000m, DateTime.Now, "V", "A", 60000m),
                new Veiculo(4, 1, 1, "Fiat", "Uno", 2020, "JKL3456", "CHASSI4", "Vermelho", 40000, 40000m, DateTime.Now, "R", "A", 35000m)
            };

            _veiculoRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(veiculos);
            _lojaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Loja>());
            _categoriaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Categoria>());

            // Act
            var result = await _useCase.Execute();

            // Assert
            result.TotalVeiculos.Should().Be(4);
            result.VeiculosDisponiveis.Should().Be(2);
            result.VeiculosVendidos.Should().Be(1);
            result.VeiculosReservados.Should().Be(1);
        }

        [Fact]
        public async Task Execute_DeveCalcularValorTotalEstoque()
        {
            // Arrange
            var veiculos = new List<Veiculo>
            {
                new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023, "ABC1234", "CHASSI1", "Prata", 10000, 100000m, DateTime.Now, "D", "A", 70000m),
                new Veiculo(2, 1, 1, "Honda", "Civic", 2022, "DEF5678", "CHASSI2", "Preto", 20000, 50000m, DateTime.Now, "D", "A", 40000m)
            };

            _veiculoRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(veiculos);
            _lojaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Loja>());
            _categoriaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Categoria>());

            // Act
            var result = await _useCase.Execute();

            // Assert
            result.ValorTotalEstoque.Should().Be(150000m);
            result.ValorMedioVeiculo.Should().Be(75000m);
        }

        [Fact]
        public async Task Execute_ComFiltroLoja_DeveRetornarApenasVeiculosDaLoja()
        {
            // Arrange
            var veiculos = new List<Veiculo>
            {
                new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023, "ABC1234", "CHASSI1", "Prata", 10000, 80000m, DateTime.Now, "D", "A", 70000m),
                new Veiculo(2, 2, 1, "Honda", "Civic", 2022, "DEF5678", "CHASSI2", "Preto", 20000, 90000m, DateTime.Now, "D", "A", 75000m)
            };

            _veiculoRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(veiculos);
            _lojaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Loja>());
            _categoriaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Categoria>());

            // Act
            var result = await _useCase.Execute(lojaId: 1);

            // Assert
            result.TotalVeiculos.Should().Be(1);
        }

        [Fact]
        public async Task Execute_ComFiltroCategoria_DeveRetornarApenasVeiculosDaCategoria()
        {
            // Arrange
            var veiculos = new List<Veiculo>
            {
                new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023, "ABC1234", "CHASSI1", "Prata", 10000, 80000m, DateTime.Now, "D", "A", 70000m),
                new Veiculo(2, 1, 2, "Ford", "Ka", 2022, "DEF5678", "CHASSI2", "Preto", 20000, 50000m, DateTime.Now, "D", "A", 40000m)
            };

            _veiculoRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(veiculos);
            _lojaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Loja>());
            _categoriaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Categoria>());

            // Act
            var result = await _useCase.Execute(categoriaId: 1);

            // Assert
            result.TotalVeiculos.Should().Be(1);
        }

        [Fact]
        public async Task Execute_DeveAgruparEstoquePorLoja()
        {
            // Arrange
            var veiculos = new List<Veiculo>
            {
                new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023, "ABC1234", "CHASSI1", "Prata", 10000, 80000m, DateTime.Now, "D", "A", 70000m),
                new Veiculo(2, 1, 1, "Honda", "Civic", 2022, "DEF5678", "CHASSI2", "Preto", 20000, 90000m, DateTime.Now, "D", "A", 75000m),
                new Veiculo(3, 2, 1, "Ford", "Focus", 2021, "GHI9012", "CHASSI3", "Branco", 30000, 70000m, DateTime.Now, "D", "A", 60000m)
            };

            var lojas = new List<Loja>
            {
                CriarLoja(1, "Loja Centro"),
                CriarLoja(2, "Loja Sul")
            };

            _veiculoRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(veiculos);
            _lojaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(lojas);
            _categoriaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Categoria>());

            // Act
            var result = await _useCase.Execute();

            // Assert
            result.EstoquePorLoja.Should().HaveCount(2);
            result.EstoquePorLoja.First().Quantidade.Should().Be(2);
        }
    }
}
