using ConnectVeiculos.Application.UseCases.Relatorios;
using ConnectVeiculos.Core.Entities.Usuarios;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Entities.Vendas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases
{
    public class ConsultarRelatorioVendasUseCaseTests
    {
        private readonly Mock<IVendaRepository> _vendaRepositoryMock;
        private readonly Mock<IUsuarioRepository> _usuarioRepositoryMock;
        private readonly Mock<IVeiculoRepository> _veiculoRepositoryMock;
        private readonly ConsultarRelatorioVendasUseCase _useCase;

        public ConsultarRelatorioVendasUseCaseTests()
        {
            _vendaRepositoryMock = new Mock<IVendaRepository>();
            _usuarioRepositoryMock = new Mock<IUsuarioRepository>();
            _veiculoRepositoryMock = new Mock<IVeiculoRepository>();
            _useCase = new ConsultarRelatorioVendasUseCase(
                _vendaRepositoryMock.Object,
                _usuarioRepositoryMock.Object,
                _veiculoRepositoryMock.Object);
        }

        [Fact]
        public async Task Consultar_Relatorio_Sem_Vendas_Deve_Retornar_Valores_Zerados()
        {
            // Arrange
            _vendaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Venda>());
            _usuarioRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Usuario>());
            _veiculoRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Veiculo>());

            // Act
            var result = await _useCase.Execute();

            // Assert
            result.Should().NotBeNull();
            result.TotalVendas.Should().Be(0);
            result.ValorTotalVendas.Should().Be(0);
            result.TotalComissoes.Should().Be(0);
        }

        [Fact]
        public async Task Consultar_Relatorio_Com_Vendas_Deve_Calcular_Totais()
        {
            // Arrange
            var vendas = new List<Venda>
            {
                new Venda(1, 1, 1, DateTime.Now, "Toyota", "Corolla", 2023, "CHASSI1", 85000m, 5m, 4250m, "Comprador 1"),
                new Venda(2, 2, 1, DateTime.Now, "Honda", "Civic", 2022, "CHASSI2", 75000m, 5m, 3750m, "Comprador 2"),
                new Venda(3, 3, 2, DateTime.Now.AddMonths(-1), "VW", "Golf", 2021, "CHASSI3", 65000m, 5m, 3250m, "Comprador 3")
            };

            var usuarios = new List<Usuario>
            {
                new Usuario(1, "Vendedor 1", "11999999999", "12345678901", "v1@email.com", "hash", "Vendedor", true),
                new Usuario(2, "Vendedor 2", "11888888888", "98765432101", "v2@email.com", "hash", "Vendedor", true)
            };

            var veiculos = new List<Veiculo>
            {
                new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023, "ABC1234", "CHASSI1", "Prata", 15000, 85000m, DateTime.Now, "V", "A", 75000m),
                new Veiculo(2, 1, 1, "Honda", "Civic", 2022, "DEF5678", "CHASSI2", "Preto", 20000, 75000m, DateTime.Now, "V", "A", 65000m),
                new Veiculo(3, 2, 1, "VW", "Golf", 2021, "GHI9012", "CHASSI3", "Branco", 25000, 65000m, DateTime.Now, "V", "A", 55000m)
            };

            _vendaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(vendas);
            _usuarioRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(usuarios);
            _veiculoRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(veiculos);

            // Act
            var result = await _useCase.Execute();

            // Assert
            result.Should().NotBeNull();
            result.TotalVendas.Should().Be(3);
            result.ValorTotalVendas.Should().Be(225000m); // 85000 + 75000 + 65000
            result.TotalComissoes.Should().Be(11250m); // 4250 + 3750 + 3250
            result.VendasAtivas.Should().Be(3);
            result.VendasEstornadas.Should().Be(0);
        }

        [Fact]
        public async Task Consultar_Relatorio_Com_Filtro_Data_Deve_Filtrar_Corretamente()
        {
            // Arrange
            var dataBase = DateTime.Now;
            var vendas = new List<Venda>
            {
                new Venda(1, 1, 1, dataBase, "Toyota", "Corolla", 2023, "CHASSI1", 85000m, 5m, 4250m, "Comprador 1"),
                new Venda(2, 2, 1, dataBase.AddMonths(-2), "Honda", "Civic", 2022, "CHASSI2", 75000m, 5m, 3750m, "Comprador 2")
            };

            var veiculos = new List<Veiculo>
            {
                new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023, "ABC1234", "CHASSI1", "Prata", 15000, 85000m, DateTime.Now, "V", "A", 75000m),
                new Veiculo(2, 1, 1, "Honda", "Civic", 2022, "DEF5678", "CHASSI2", "Preto", 20000, 75000m, DateTime.Now, "V", "A", 65000m)
            };

            _vendaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(vendas);
            _usuarioRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Usuario>());
            _veiculoRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(veiculos);

            // Act - Filtra apenas vendas do mes atual
            var result = await _useCase.Execute(dataBase.AddDays(-15), dataBase.AddDays(15));

            // Assert
            result.TotalVendas.Should().Be(1);
            result.ValorTotalVendas.Should().Be(85000m);
        }

        [Fact]
        public async Task Consultar_Relatorio_Deve_Agrupar_Por_Vendedor()
        {
            // Arrange
            var vendas = new List<Venda>
            {
                new Venda(1, 1, 1, DateTime.Now, "Toyota", "Corolla", 2023, "CHASSI1", 85000m, 5m, 4250m, "Comprador 1"),
                new Venda(2, 2, 1, DateTime.Now, "Honda", "Civic", 2022, "CHASSI2", 75000m, 5m, 3750m, "Comprador 2"),
                new Venda(3, 3, 2, DateTime.Now, "VW", "Golf", 2021, "CHASSI3", 65000m, 5m, 3250m, "Comprador 3")
            };

            var usuarios = new List<Usuario>
            {
                new Usuario(1, "Vendedor A", "11999999999", "12345678901", "va@email.com", "hash", "Vendedor", true),
                new Usuario(2, "Vendedor B", "11888888888", "98765432101", "vb@email.com", "hash", "Vendedor", true)
            };

            var veiculos = new List<Veiculo>
            {
                new Veiculo(1, 1, 1, "Toyota", "Corolla", 2023, "ABC1234", "CHASSI1", "Prata", 15000, 85000m, DateTime.Now, "V", "A", 75000m),
                new Veiculo(2, 1, 1, "Honda", "Civic", 2022, "DEF5678", "CHASSI2", "Preto", 20000, 75000m, DateTime.Now, "V", "A", 65000m),
                new Veiculo(3, 1, 1, "VW", "Golf", 2021, "GHI9012", "CHASSI3", "Branco", 25000, 65000m, DateTime.Now, "V", "A", 55000m)
            };

            _vendaRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(vendas);
            _usuarioRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(usuarios);
            _veiculoRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(veiculos);

            // Act
            var result = await _useCase.Execute();

            // Assert
            result.VendasPorVendedor.Should().HaveCount(2);
            result.VendasPorVendedor.First(v => v.VendedorId == 1).Quantidade.Should().Be(2);
            result.VendasPorVendedor.First(v => v.VendedorId == 2).Quantidade.Should().Be(1);
        }
    }
}
