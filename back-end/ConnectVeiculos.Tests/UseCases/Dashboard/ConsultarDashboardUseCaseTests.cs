using ConnectVeiculos.Application.UseCases.Dashboard;
using ConnectVeiculos.Core.Entities.Categorias;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.Usuarios;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Categorias;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases.Dashboard
{
    public class ConsultarDashboardUseCaseTests
    {
        private readonly Mock<IVeiculoRepository> _veiculoRepositoryMock;
        private readonly Mock<ILojaRepository> _lojaRepositoryMock;
        private readonly Mock<ICategoriaRepository> _categoriaRepositoryMock;
        private readonly Mock<IUsuarioRepository> _usuarioRepositoryMock;
        private readonly ConsultarDashboardUseCase _useCase;

        public ConsultarDashboardUseCaseTests()
        {
            _veiculoRepositoryMock = new Mock<IVeiculoRepository>();
            _lojaRepositoryMock = new Mock<ILojaRepository>();
            _categoriaRepositoryMock = new Mock<ICategoriaRepository>();
            _usuarioRepositoryMock = new Mock<IUsuarioRepository>();
            _useCase = new ConsultarDashboardUseCase(
                _veiculoRepositoryMock.Object,
                _lojaRepositoryMock.Object,
                _categoriaRepositoryMock.Object,
                _usuarioRepositoryMock.Object);
        }

        private static Loja CriarLoja(int id, string nome, string cidade = "SP", string estado = "SP")
        {
            return new Loja(id, nome, "Rua A", "123", "Centro", cidade, estado, "12345678", "", "loja@email.com", "11999999999", "", "11999999999", "", "12345678901234", "", true);
        }

        [Fact]
        public async Task Execute_DeveRetornarTotaisCorretos()
        {
            // Arrange
            var veiculos = new List<Veiculo>
            {
                new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023, "ABC1234", "CHASSI1", "Prata", 10000, 80000m, DateTime.Now, "D", "A", 70000m),
                new Veiculo(2, 1, 1, "Honda", "Civic", 2022, "DEF5678", "CHASSI2", "Preto", 20000, 90000m, DateTime.Now, "D", "A", 75000m),
                new Veiculo(3, 1, 2, "Ford", "Focus", 2021, "GHI9012", "CHASSI3", "Branco", 30000, 70000m, DateTime.Now, "V", "A", 60000m)
            };

            var lojas = new List<Loja> { CriarLoja(1, "Loja Centro") };

            var categorias = new List<Categoria>
            {
                new Categoria(1, "Sedan", "Veículos Sedan", true),
                new Categoria(2, "Hatch", "Veículos Hatch", true)
            };

            var usuarios = new List<Usuario>
            {
                new Usuario(1, "João", "12345678901", "11999999999", "joao@email.com", "hash", "Vendedor", true)
            };

            _veiculoRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(veiculos);
            _lojaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(lojas);
            _categoriaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(categorias);
            _usuarioRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(usuarios);

            // Act
            var result = await _useCase.Execute();

            // Assert
            result.TotalVeiculos.Should().Be(3);
            result.VeiculosDisponiveis.Should().Be(2);
            result.VeiculosVendidos.Should().Be(1);
            result.TotalLojas.Should().Be(1);
            result.TotalCategorias.Should().Be(2);
            result.TotalUsuarios.Should().Be(1);
        }

        [Fact]
        public async Task Execute_DeveCalcularValorEstoqueCorreto()
        {
            // Arrange
            var veiculos = new List<Veiculo>
            {
                new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023, "ABC1234", "CHASSI1", "Prata", 10000, 100000m, DateTime.Now, "D", "A", 70000m),
                new Veiculo(2, 1, 1, "Honda", "Civic", 2022, "DEF5678", "CHASSI2", "Preto", 20000, 50000m, DateTime.Now, "D", "A", 40000m),
                new Veiculo(3, 1, 1, "Ford", "Focus", 2021, "GHI9012", "CHASSI3", "Branco", 30000, 70000m, DateTime.Now, "V", "A", 60000m)
            };

            _veiculoRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(veiculos);
            _lojaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Loja>());
            _categoriaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Categoria>());
            _usuarioRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Usuario>());

            // Act
            var result = await _useCase.Execute();

            // Assert
            result.ValorTotalEstoque.Should().Be(150000m);
            result.ValorMedioVeiculo.Should().Be(75000m);
        }

        [Fact]
        public async Task Execute_DeveRetornarVeiculosPorCategoria()
        {
            // Arrange
            var veiculos = new List<Veiculo>
            {
                new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023, "ABC1234", "CHASSI1", "Prata", 10000, 80000m, DateTime.Now, "D", "A", 70000m),
                new Veiculo(2, 1, 1, "Honda", "Civic", 2022, "DEF5678", "CHASSI2", "Preto", 20000, 90000m, DateTime.Now, "D", "A", 75000m),
                new Veiculo(3, 1, 2, "Ford", "Ka", 2021, "GHI9012", "CHASSI3", "Branco", 30000, 50000m, DateTime.Now, "D", "A", 40000m)
            };

            var categorias = new List<Categoria>
            {
                new Categoria(1, "Sedan", "Veículos Sedan", true),
                new Categoria(2, "Hatch", "Veículos Hatch", true)
            };

            _veiculoRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(veiculos);
            _lojaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Loja>());
            _categoriaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(categorias);
            _usuarioRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Usuario>());

            // Act
            var result = await _useCase.Execute();

            // Assert
            result.VeiculosPorCategoria.Should().HaveCount(2);
            result.VeiculosPorCategoria.First().Quantidade.Should().Be(2);
        }

        [Fact]
        public async Task Execute_ComListasVazias_DeveRetornarZeros()
        {
            // Arrange
            _veiculoRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Veiculo>());
            _lojaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Loja>());
            _categoriaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Categoria>());
            _usuarioRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Usuario>());

            // Act
            var result = await _useCase.Execute();

            // Assert
            result.TotalVeiculos.Should().Be(0);
            result.VeiculosDisponiveis.Should().Be(0);
            result.ValorTotalEstoque.Should().Be(0);
            result.ValorMedioVeiculo.Should().Be(0);
        }
    }
}
