using ConnectVeiculos.Application.InputModels.Veiculos;
using ConnectVeiculos.Application.UseCases.Veiculos;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases.Veiculos
{
    public class BuscaAvancadaVeiculosUseCaseTests
    {
        private readonly Mock<IVeiculoRepository> _veiculoRepositoryMock;
        private readonly BuscaAvancadaVeiculosUseCase _useCase;

        public BuscaAvancadaVeiculosUseCaseTests()
        {
            _veiculoRepositoryMock = new Mock<IVeiculoRepository>();
            _useCase = new BuscaAvancadaVeiculosUseCase(_veiculoRepositoryMock.Object);
        }

        [Fact]
        public async Task Execute_DeveRetornarResultadoPaginado()
        {
            // Arrange
            var veiculos = new List<Veiculo>
            {
                new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023, "ABC1234", "CHASSI1", "Prata", 10000, 80000m, DateTime.Now, "D", "A", 70000m),
                new Veiculo(2, 1, 1, "Honda", "Civic", 2022, "DEF5678", "CHASSI2", "Preto", 20000, 90000m, DateTime.Now, "D", "A", 75000m)
            };

            _veiculoRepositoryMock.Setup(x => x.BuscaAvancadaAsync(It.IsAny<BuscaAvancadaParams>()))
                .ReturnsAsync((veiculos, 2));

            var input = new BuscaAvancadaVeiculoInputModel
            {
                Pagina = 1,
                TamanhoPagina = 10
            };

            // Act
            var result = await _useCase.Execute(input);

            // Assert
            result.Items.Should().HaveCount(2);
            result.TotalItems.Should().Be(2);
            result.Page.Should().Be(1);
            result.PageSize.Should().Be(10);
        }

        [Fact]
        public async Task Execute_ComFiltroMarca_DevePassarParametroCorreto()
        {
            // Arrange
            BuscaAvancadaParams parametrosCapturados = null;
            _veiculoRepositoryMock.Setup(x => x.BuscaAvancadaAsync(It.IsAny<BuscaAvancadaParams>()))
                .Callback<BuscaAvancadaParams>(p => parametrosCapturados = p)
                .ReturnsAsync((new List<Veiculo>(), 0));

            var input = new BuscaAvancadaVeiculoInputModel
            {
                Marca = "Toyota",
                Pagina = 1,
                TamanhoPagina = 10
            };

            // Act
            await _useCase.Execute(input);

            // Assert
            parametrosCapturados.Marca.Should().Be("Toyota");
        }

        [Fact]
        public async Task Execute_ComFiltroPreco_DevePassarParametrosCorretos()
        {
            // Arrange
            BuscaAvancadaParams parametrosCapturados = null;
            _veiculoRepositoryMock.Setup(x => x.BuscaAvancadaAsync(It.IsAny<BuscaAvancadaParams>()))
                .Callback<BuscaAvancadaParams>(p => parametrosCapturados = p)
                .ReturnsAsync((new List<Veiculo>(), 0));

            var input = new BuscaAvancadaVeiculoInputModel
            {
                PrecoMinimo = 50000m,
                PrecoMaximo = 100000m,
                Pagina = 1,
                TamanhoPagina = 10
            };

            // Act
            await _useCase.Execute(input);

            // Assert
            parametrosCapturados.PrecoMinimo.Should().Be(50000m);
            parametrosCapturados.PrecoMaximo.Should().Be(100000m);
        }

        [Fact]
        public async Task Execute_ComFiltroAno_DevePassarParametrosCorretos()
        {
            // Arrange
            BuscaAvancadaParams parametrosCapturados = null;
            _veiculoRepositoryMock.Setup(x => x.BuscaAvancadaAsync(It.IsAny<BuscaAvancadaParams>()))
                .Callback<BuscaAvancadaParams>(p => parametrosCapturados = p)
                .ReturnsAsync((new List<Veiculo>(), 0));

            var input = new BuscaAvancadaVeiculoInputModel
            {
                AnoMinimo = 2020,
                AnoMaximo = 2024,
                Pagina = 1,
                TamanhoPagina = 10
            };

            // Act
            await _useCase.Execute(input);

            // Assert
            parametrosCapturados.AnoMinimo.Should().Be(2020);
            parametrosCapturados.AnoMaximo.Should().Be(2024);
        }

        [Fact]
        public async Task Execute_ComOrdenacao_DevePassarParametrosCorretos()
        {
            // Arrange
            BuscaAvancadaParams parametrosCapturados = null;
            _veiculoRepositoryMock.Setup(x => x.BuscaAvancadaAsync(It.IsAny<BuscaAvancadaParams>()))
                .Callback<BuscaAvancadaParams>(p => parametrosCapturados = p)
                .ReturnsAsync((new List<Veiculo>(), 0));

            var input = new BuscaAvancadaVeiculoInputModel
            {
                OrdenarPor = "preco",
                Direcao = "asc",
                Pagina = 1,
                TamanhoPagina = 10
            };

            // Act
            await _useCase.Execute(input);

            // Assert
            parametrosCapturados.OrdenarPor.Should().Be("preco");
            parametrosCapturados.Direcao.Should().Be("asc");
        }

        [Fact]
        public async Task Execute_SemDirecao_DeveUsarDescPadrao()
        {
            // Arrange
            BuscaAvancadaParams parametrosCapturados = null;
            _veiculoRepositoryMock.Setup(x => x.BuscaAvancadaAsync(It.IsAny<BuscaAvancadaParams>()))
                .Callback<BuscaAvancadaParams>(p => parametrosCapturados = p)
                .ReturnsAsync((new List<Veiculo>(), 0));

            var input = new BuscaAvancadaVeiculoInputModel
            {
                Pagina = 1,
                TamanhoPagina = 10
            };

            // Act
            await _useCase.Execute(input);

            // Assert
            parametrosCapturados.Direcao.Should().Be("desc");
        }

        [Fact]
        public async Task Execute_DeveMapearViewModelCorretamente()
        {
            // Arrange
            var veiculo = new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023, "ABC1234", "CHASSI1", "Prata", 10000, 80000m, DateTime.Now, "D", "A", 70000m);

            _veiculoRepositoryMock.Setup(x => x.BuscaAvancadaAsync(It.IsAny<BuscaAvancadaParams>()))
                .ReturnsAsync((new List<Veiculo> { veiculo }, 1));

            var input = new BuscaAvancadaVeiculoInputModel
            {
                Pagina = 1,
                TamanhoPagina = 10
            };

            // Act
            var result = await _useCase.Execute(input);

            // Assert
            var vm = result.Items.First();
            vm.VeiId.Should().Be(1);
            vm.VeiMarca.Should().Be("Toyota");
            vm.VeiModelo.Should().Be("Corolla");
            vm.VeiAno.Should().Be(2023);
            vm.VeiPreco.Should().Be(80000m);
        }
    }
}
