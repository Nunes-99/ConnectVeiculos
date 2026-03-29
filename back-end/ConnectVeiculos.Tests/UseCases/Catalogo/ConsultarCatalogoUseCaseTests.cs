using ConnectVeiculos.Application.UseCases.Catalogo;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Entities.VeiculosImagens;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.VeiculosImagens;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases.Catalogo
{
    public class ConsultarCatalogoUseCaseTests
    {
        private readonly Mock<IVeiculoRepository> _veiculoRepositoryMock;
        private readonly Mock<ILojaRepository> _lojaRepositoryMock;
        private readonly Mock<IVeiculoImagemRepository> _imagemRepositoryMock;
        private readonly ConsultarCatalogoUseCase _useCase;

        public ConsultarCatalogoUseCaseTests()
        {
            _veiculoRepositoryMock = new Mock<IVeiculoRepository>();
            _lojaRepositoryMock = new Mock<ILojaRepository>();
            _imagemRepositoryMock = new Mock<IVeiculoImagemRepository>();
            _useCase = new ConsultarCatalogoUseCase(
                _veiculoRepositoryMock.Object,
                _lojaRepositoryMock.Object,
                _imagemRepositoryMock.Object);
        }

        private static Loja CriarLoja(int id, string nome, string cidade = "São Paulo", string estado = "SP")
        {
            return new Loja(id, nome, "Rua A", "123", "Centro", cidade, estado, "12345678", "", "loja@email.com", "11999999999", "", "11999999999", "", "12345678901234", "", true);
        }

        [Fact]
        public async Task Execute_DeveRetornarApenasVeiculosDisponiveis()
        {
            // Arrange
            var veiculos = new List<Veiculo>
            {
                new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023, "ABC1234", "CHASSI1", "Prata", 10000, 80000m, DateTime.Now, "D", "A", 70000m),
                new Veiculo(2, 1, 1, "Honda", "Civic", 2022, "DEF5678", "CHASSI2", "Preto", 20000, 90000m, DateTime.Now, "V", "A", 75000m),
                new Veiculo(3, 1, 1, "Ford", "Focus", 2021, "GHI9012", "CHASSI3", "Branco", 30000, 70000m, DateTime.Now, "D", "A", 60000m)
            };

            _veiculoRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(veiculos);
            _lojaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Loja>());
            _imagemRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<VeiculoImagem>());

            // Act
            var result = await _useCase.Execute(null, null, null, null, null);

            // Assert
            result.Veiculos.Should().HaveCount(2);
            result.Veiculos.All(v => v.VeiId != 2).Should().BeTrue();
        }

        [Fact]
        public async Task Execute_ComFiltroMarca_DeveRetornarApenasVeiculosDaMarca()
        {
            // Arrange
            var veiculos = new List<Veiculo>
            {
                new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023, "ABC1234", "CHASSI1", "Prata", 10000, 80000m, DateTime.Now, "D", "A", 70000m),
                new Veiculo(2, 1, 1, "Honda", "Civic", 2022, "DEF5678", "CHASSI2", "Preto", 20000, 90000m, DateTime.Now, "D", "A", 75000m),
                new Veiculo(3, 1, 1, "Toyota", "Yaris", 2021, "GHI9012", "CHASSI3", "Branco", 30000, 70000m, DateTime.Now, "D", "A", 60000m)
            };

            _veiculoRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(veiculos);
            _lojaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Loja>());
            _imagemRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<VeiculoImagem>());

            // Act
            var result = await _useCase.Execute("Toyota", null, null, null, null);

            // Assert
            result.Veiculos.Should().HaveCount(2);
            result.Veiculos.All(v => v.VeiMarca == "Toyota").Should().BeTrue();
        }

        [Fact]
        public async Task Execute_ComFiltroAno_DeveRetornarVeiculosNaFaixa()
        {
            // Arrange
            var veiculos = new List<Veiculo>
            {
                new Veiculo(1, 1, 1, "Toyota", "Corolla", 2020, "ABC1234", "CHASSI1", "Prata", 10000, 80000m, DateTime.Now, "D", "A", 70000m),
                new Veiculo(2, 1, 1, "Honda", "Civic", 2022, "DEF5678", "CHASSI2", "Preto", 20000, 90000m, DateTime.Now, "D", "A", 75000m),
                new Veiculo(3, 1, 1, "Ford", "Focus", 2024, "GHI9012", "CHASSI3", "Branco", 30000, 70000m, DateTime.Now, "D", "A", 60000m)
            };

            _veiculoRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(veiculos);
            _lojaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Loja>());
            _imagemRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<VeiculoImagem>());

            // Act
            var result = await _useCase.Execute(null, 2021, 2023, null, null);

            // Assert
            result.Veiculos.Should().HaveCount(1);
            result.Veiculos.First().VeiAno.Should().Be(2022);
        }

        [Fact]
        public async Task Execute_ComFiltroPreco_DeveRetornarVeiculosNaFaixa()
        {
            // Arrange
            var veiculos = new List<Veiculo>
            {
                new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023, "ABC1234", "CHASSI1", "Prata", 10000, 50000m, DateTime.Now, "D", "A", 40000m),
                new Veiculo(2, 1, 1, "Honda", "Civic", 2022, "DEF5678", "CHASSI2", "Preto", 20000, 100000m, DateTime.Now, "D", "A", 80000m),
                new Veiculo(3, 1, 1, "Ford", "Focus", 2021, "GHI9012", "CHASSI3", "Branco", 30000, 200000m, DateTime.Now, "D", "A", 150000m)
            };

            _veiculoRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(veiculos);
            _lojaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Loja>());
            _imagemRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<VeiculoImagem>());

            // Act
            var result = await _useCase.Execute(null, null, null, 60000m, 150000m);

            // Assert
            result.Veiculos.Should().HaveCount(1);
            result.Veiculos.First().VeiPreco.Should().Be(100000m);
        }

        [Fact]
        public async Task Execute_DeveRetornarFiltrosDisponiveis()
        {
            // Arrange
            var veiculos = new List<Veiculo>
            {
                new Veiculo(1, 1, 1, "Toyota", "Corolla", 2020, "ABC1234", "CHASSI1", "Prata", 10000, 50000m, DateTime.Now, "D", "A", 40000m),
                new Veiculo(2, 1, 1, "Honda", "Civic", 2022, "DEF5678", "CHASSI2", "Preto", 20000, 100000m, DateTime.Now, "D", "A", 80000m),
                new Veiculo(3, 1, 1, "Ford", "Focus", 2024, "GHI9012", "CHASSI3", "Branco", 30000, 200000m, DateTime.Now, "D", "A", 150000m)
            };

            _veiculoRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(veiculos);
            _lojaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Loja>());
            _imagemRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<VeiculoImagem>());

            // Act
            var result = await _useCase.Execute(null, null, null, null, null);

            // Assert
            result.Filtros.Marcas.Should().Contain("Toyota");
            result.Filtros.Marcas.Should().Contain("Honda");
            result.Filtros.Marcas.Should().Contain("Ford");
            result.Filtros.AnoMin.Should().Be(2020);
            result.Filtros.AnoMax.Should().Be(2024);
            result.Filtros.PrecoMin.Should().Be(50000m);
            result.Filtros.PrecoMax.Should().Be(200000m);
        }

        [Fact]
        public async Task Execute_DeveIncluirInformacoesLoja()
        {
            // Arrange
            var veiculos = new List<Veiculo>
            {
                new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023, "ABC1234", "CHASSI1", "Prata", 10000, 80000m, DateTime.Now, "D", "A", 70000m)
            };

            var lojas = new List<Loja> { CriarLoja(1, "Loja Centro") };

            _veiculoRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(veiculos);
            _lojaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(lojas);
            _imagemRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<VeiculoImagem>());

            // Act
            var result = await _useCase.Execute(null, null, null, null, null);

            // Assert
            var veiculo = result.Veiculos.First();
            veiculo.LojaNome.Should().Be("Loja Centro");
            veiculo.LojaCidade.Should().Be("São Paulo");
            veiculo.LojaEstado.Should().Be("SP");
        }
    }
}
