using ConnectVeiculos.Application.UseCases.Relatorios;
using ConnectVeiculos.Core.Entities.Lojas;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Entities.Vendas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases.Relatorios
{
    public class ConsultarRelatorioFinanceiroUseCaseTests
    {
        private readonly Mock<IVendaRepository> _vendaRepositoryMock;
        private readonly Mock<IVeiculoRepository> _veiculoRepositoryMock;
        private readonly Mock<ILojaRepository> _lojaRepositoryMock;
        private readonly ConsultarRelatorioFinanceiroUseCase _useCase;

        public ConsultarRelatorioFinanceiroUseCaseTests()
        {
            _vendaRepositoryMock = new Mock<IVendaRepository>();
            _veiculoRepositoryMock = new Mock<IVeiculoRepository>();
            _lojaRepositoryMock = new Mock<ILojaRepository>();
            _useCase = new ConsultarRelatorioFinanceiroUseCase(
                _vendaRepositoryMock.Object,
                _veiculoRepositoryMock.Object,
                _lojaRepositoryMock.Object);
        }

        private static Venda CriarVenda(int id, int veiculoId, int vendedorId, DateTime dataVenda, decimal valor, decimal comissaoPorc, decimal comissaoValor)
        {
            return new Venda(id, veiculoId, vendedorId, dataVenda, "Toyota", "Corolla", 2023, "CHASSI", valor, comissaoPorc, comissaoValor, "Comprador", "12345678901", "11999999999");
        }

        [Fact]
        public async Task Execute_DeveCalcularReceitaBruta()
        {
            // Arrange
            var vendas = new List<Venda>
            {
                CriarVenda(1, 1, 1, DateTime.Now, 100000m, 5, 5000m),
                CriarVenda(2, 2, 1, DateTime.Now, 80000m, 5, 4000m)
            };

            var veiculos = new List<Veiculo>
            {
                new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023, "ABC1234", "CHASSI1", "Prata", 10000, 100000m, DateTime.Now, "V", "A", 70000m),
                new Veiculo(2, 1, 1, "Honda", "Civic", 2022, "DEF5678", "CHASSI2", "Preto", 20000, 80000m, DateTime.Now, "V", "A", 60000m)
            };

            _vendaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(vendas);
            _veiculoRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(veiculos);
            _lojaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Loja>());

            // Act
            var result = await _useCase.Execute();

            // Assert
            result.ReceitaBruta.Should().Be(180000m);
        }

        [Fact]
        public async Task Execute_DeveCalcularLucroBruto()
        {
            // Arrange
            var vendas = new List<Venda>
            {
                CriarVenda(1, 1, 1, DateTime.Now, 100000m, 5, 5000m)
            };

            var veiculos = new List<Veiculo>
            {
                new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023, "ABC1234", "CHASSI1", "Prata", 10000, 100000m, DateTime.Now, "V", "A", 70000m)
            };

            _vendaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(vendas);
            _veiculoRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(veiculos);
            _lojaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Loja>());

            // Act
            var result = await _useCase.Execute();

            // Assert
            result.LucroBruto.Should().Be(30000m);
        }

        [Fact]
        public async Task Execute_DeveCalcularLucroLiquido()
        {
            // Arrange
            var vendas = new List<Venda>
            {
                CriarVenda(1, 1, 1, DateTime.Now, 100000m, 5, 5000m)
            };

            var veiculos = new List<Veiculo>
            {
                new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023, "ABC1234", "CHASSI1", "Prata", 10000, 100000m, DateTime.Now, "V", "A", 70000m)
            };

            _vendaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(vendas);
            _veiculoRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(veiculos);
            _lojaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Loja>());

            // Act
            var result = await _useCase.Execute();

            // Assert
            result.LucroLiquido.Should().Be(25000m);
        }

        [Fact]
        public async Task Execute_ComFiltroData_DeveConsiderarApenasVendasNoPeriodo()
        {
            // Arrange
            var vendas = new List<Venda>
            {
                CriarVenda(1, 1, 1, new DateTime(2024, 1, 15), 100000m, 5, 5000m),
                CriarVenda(2, 2, 1, new DateTime(2024, 2, 20), 80000m, 5, 4000m)
            };

            var veiculos = new List<Veiculo>
            {
                new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023, "ABC1234", "CHASSI1", "Prata", 10000, 100000m, DateTime.Now, "V", "A", 70000m),
                new Veiculo(2, 1, 1, "Honda", "Civic", 2022, "DEF5678", "CHASSI2", "Preto", 20000, 80000m, DateTime.Now, "V", "A", 60000m)
            };

            _vendaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(vendas);
            _veiculoRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(veiculos);
            _lojaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Loja>());

            // Act
            var result = await _useCase.Execute(dataInicio: new DateTime(2024, 1, 1), dataFim: new DateTime(2024, 1, 31));

            // Assert
            result.ReceitaBruta.Should().Be(100000m);
        }

        [Fact]
        public async Task Execute_DeveCalcularTicketMedio()
        {
            // Arrange
            var vendas = new List<Venda>
            {
                CriarVenda(1, 1, 1, DateTime.Now, 100000m, 5, 5000m),
                CriarVenda(2, 2, 1, DateTime.Now, 80000m, 5, 4000m)
            };

            var veiculos = new List<Veiculo>
            {
                new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023, "ABC1234", "CHASSI1", "Prata", 10000, 100000m, DateTime.Now, "V", "A", 70000m),
                new Veiculo(2, 1, 1, "Honda", "Civic", 2022, "DEF5678", "CHASSI2", "Preto", 20000, 80000m, DateTime.Now, "V", "A", 60000m)
            };

            _vendaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(vendas);
            _veiculoRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(veiculos);
            _lojaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Loja>());

            // Act
            var result = await _useCase.Execute();

            // Assert
            result.TicketMedio.Should().Be(90000m);
        }
    }
}
