using ConnectVeiculos.Application.UseCases.Dashboard;
using ConnectVeiculos.Core.Entities.Despesas;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Entities.Vendas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Despesas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases.Dashboard
{
    public class ConsultarLucroDashboardUseCaseTests
    {
        private readonly Mock<IVendaRepository> _vendaRepoMock = new();
        private readonly Mock<IVeiculoRepository> _veiculoRepoMock = new();
        private readonly Mock<IVeiculoDespesaRepository> _despesaRepoMock = new();
        private readonly ConsultarLucroDashboardUseCase _useCase;

        public ConsultarLucroDashboardUseCaseTests()
        {
            _useCase = new ConsultarLucroDashboardUseCase(
                _vendaRepoMock.Object,
                _veiculoRepoMock.Object,
                _despesaRepoMock.Object);
        }

        private static Veiculo VeiculoFake(int id, string marca = "Toyota", string modelo = "Corolla", decimal precoCompra = 80_000m)
        {
            var v = new Veiculo(id, 1, 1, marca, modelo, 2023, "ABC1D23",
                "9BWZZZ377VT004251", "Prata", 15000, 100_000m,
                DateTime.Now, "V", "D", precoCompra);
            return v;
        }

        private static Venda VendaFake(int veiculoId, decimal valor, DateTime? data = null, decimal comissao = 0)
        {
            // Venda(int venId, int rVeiId, int rUsuId, string venMarca, string venModelo, string venChassi,
            //       decimal venValor, decimal venComissaoPorc, decimal venComissaoValor, ... )
            // Construtor pode variar; usar reflexao seria fragil. Criar via construtor mais provavel:
            var venda = (Venda)Activator.CreateInstance(typeof(Venda), nonPublic: true)!;
            // Usar reflection para setar private setters
            typeof(Venda).GetProperty("VenId")!.SetValue(venda, 1, null);
            typeof(Venda).GetProperty("R_VeiId")!.SetValue(venda, veiculoId, null);
            typeof(Venda).GetProperty("VenValor")!.SetValue(venda, valor, null);
            typeof(Venda).GetProperty("VenComissaoValor")!.SetValue(venda, comissao, null);
            typeof(Venda).GetProperty("VenStatus")!.SetValue(venda, "A", null);
            typeof(Venda).GetProperty("VenDtVenda")!.SetValue(venda, data ?? DateTime.Today, null);
            return venda;
        }

        [Fact]
        public async Task Execute_SemVendas_RetornaZeros()
        {
            _vendaRepoMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Venda>());
            _veiculoRepoMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Veiculo>());
            _despesaRepoMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<VeiculoDespesa>());

            var result = await _useCase.Execute();

            result.Receita.Should().Be(0);
            result.LucroLiquido.Should().Be(0);
            result.MargemMedia.Should().Be(0);
            result.TopVeiculosRentaveis.Should().BeEmpty();
            result.TotalVendas.Should().Be(0);
        }

        [Fact]
        public async Task Execute_ComVendaSimples_CalculaLucroCorretamente()
        {
            var veiculo = VeiculoFake(1, precoCompra: 80_000m);
            var venda = VendaFake(1, 100_000m);

            _veiculoRepoMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new[] { veiculo });
            _vendaRepoMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new[] { venda });
            _despesaRepoMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<VeiculoDespesa>());

            var result = await _useCase.Execute();

            result.Receita.Should().Be(100_000m);
            result.CustoCompra.Should().Be(80_000m);
            result.Despesas.Should().Be(0);
            result.Comissoes.Should().Be(0);
            result.LucroLiquido.Should().Be(20_000m);
            result.MargemMedia.Should().Be(20m);
            result.TotalVendas.Should().Be(1);
            result.TopVeiculosRentaveis.Should().HaveCount(1);
            result.TopVeiculosRentaveis[0].Lucro.Should().Be(20_000m);
        }

        [Fact]
        public async Task Execute_ComDespesasEComissoes_DescontaTudo()
        {
            var veiculo = VeiculoFake(1, precoCompra: 80_000m);
            var venda = VendaFake(1, 100_000m, comissao: 5_000m);
            var despesa = new VeiculoDespesa(0, 1, "MANUTENCAO", "troca de oleo", 2_000m, DateTime.Today);

            _veiculoRepoMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new[] { veiculo });
            _vendaRepoMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new[] { venda });
            _despesaRepoMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new[] { despesa });

            var result = await _useCase.Execute();

            // 100k - 80k(compra) - 2k(despesas) - 5k(comissao) = 13k
            result.LucroLiquido.Should().Be(13_000m);
            result.Despesas.Should().Be(2_000m);
            result.Comissoes.Should().Be(5_000m);
        }

        [Fact]
        public async Task Execute_TopVeiculos_OrdenadosPorLucroDesc()
        {
            var v1 = VeiculoFake(1, marca: "Honda", precoCompra: 50_000m);
            var v2 = VeiculoFake(2, marca: "Toyota", precoCompra: 80_000m);
            var v3 = VeiculoFake(3, marca: "Ford", precoCompra: 60_000m);

            var vendas = new[] {
                VendaFake(1, 70_000m),  // lucro 20k
                VendaFake(2, 120_000m), // lucro 40k
                VendaFake(3, 65_000m)   // lucro 5k
            };

            _veiculoRepoMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new[] { v1, v2, v3 });
            _vendaRepoMock.Setup(x => x.GetAllAsync()).ReturnsAsync(vendas);
            _despesaRepoMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<VeiculoDespesa>());

            var result = await _useCase.Execute();

            result.TopVeiculosRentaveis.Should().HaveCount(3);
            result.TopVeiculosRentaveis[0].Marca.Should().Be("Toyota");
            result.TopVeiculosRentaveis[0].Lucro.Should().Be(40_000m);
            result.TopVeiculosRentaveis[1].Lucro.Should().Be(20_000m);
            result.TopVeiculosRentaveis[2].Lucro.Should().Be(5_000m);
        }
    }
}
