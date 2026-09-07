using ConnectVeiculos.Application.UseCases.Dashboard;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Entities.Vendas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConnectVeiculos.Tests.UseCases.Dashboard
{
    /// <summary>
    /// O comparativo media o mes corrente (parcial) contra o mes anterior
    /// inteiro. Resultado: todo comeco de mes o painel mostrava -100% em tudo,
    /// como se o faturamento tivesse desabado. Agora os dois lados cobrem o
    /// mesmo intervalo de dias.
    /// </summary>
    public class ConsultarComparativoMensalUseCaseTests
    {
        private readonly Mock<IVendaRepository> _vendaRepositoryMock = new();
        private readonly Mock<IVeiculoRepository> _veiculoRepositoryMock = new();

        private ConsultarComparativoMensalUseCase CriarUseCase(params Venda[] vendas)
        {
            _vendaRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(vendas);
            _veiculoRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Veiculo>());
            return new ConsultarComparativoMensalUseCase(
                _vendaRepositoryMock.Object, _veiculoRepositoryMock.Object);
        }

        private static Venda Vender(DateTime data, decimal valor) =>
            new Venda(0, 1, 1, data, "Fiat", "Argo", 2021, "9BWZZZ377VT004251", valor, 0, 0, "Comprador Teste");

        [Fact]
        public async Task Execute_NaoDeveContarVendasDoMesAnteriorAlemDoDiaDeCorte()
        {
            var hoje = DateTime.Today;
            var mesAnterior = new DateTime(hoje.Year, hoje.Month, 1).AddMonths(-1);
            var diasNoMesAnterior = DateTime.DaysInMonth(mesAnterior.Year, mesAnterior.Month);
            var diaDeCorte = Math.Min(hoje.Day, diasNoMesAnterior);

            var vendas = new List<Venda> { Vender(mesAnterior, 10_000m) };

            // Venda no ultimo dia do mes anterior: so entra se ja passamos desse
            // dia no mes atual. Caso contrario esta fora do intervalo comparado.
            var ultimoDia = mesAnterior.AddDays(diasNoMesAnterior - 1);
            if (diaDeCorte < diasNoMesAnterior)
                vendas.Add(Vender(ultimoDia, 90_000m));

            var resultado = await CriarUseCase(vendas.ToArray()).Execute();

            if (diaDeCorte < diasNoMesAnterior)
            {
                resultado.MesAnterior.Faturamento.Should().Be(10_000m,
                    "a venda do fim do mes anterior esta fora do intervalo comparado");
                resultado.MesAnterior.QuantidadeVendas.Should().Be(1);
            }
            else
            {
                resultado.MesAnterior.Faturamento.Should().Be(10_000m);
            }
        }

        [Fact]
        public async Task Execute_ComMesmoFaturamentoNoIntervalo_DeveDarVariacaoZero()
        {
            var hoje = DateTime.Today;
            var inicioMesAtual = new DateTime(hoje.Year, hoje.Month, 1);
            var inicioMesAnterior = inicioMesAtual.AddMonths(-1);

            // Uma venda no dia 1 de cada mes: mesmo valor, mesmo ponto do periodo.
            var resultado = await CriarUseCase(
                Vender(inicioMesAtual, 50_000m),
                Vender(inicioMesAnterior, 50_000m)).Execute();

            resultado.MesAtual.Faturamento.Should().Be(50_000m);
            resultado.MesAnterior.Faturamento.Should().Be(50_000m);
            resultado.VariacaoFaturamento.Should().Be(0m);
            resultado.VariacaoQuantidade.Should().Be(0m);
        }

        [Fact]
        public async Task Execute_DeveSinalizarComparacaoParcialEDiaDeCorte()
        {
            var hoje = DateTime.Today;
            var resultado = await CriarUseCase().Execute();

            var ehUltimoDia = hoje.Day == DateTime.DaysInMonth(hoje.Year, hoje.Month);
            resultado.ComparacaoParcial.Should().Be(!ehUltimoDia);

            var diasNoMesAnterior = DateTime.DaysInMonth(
                hoje.AddMonths(-1).Year, hoje.AddMonths(-1).Month);
            resultado.DiaDeCorte.Should().Be(Math.Min(hoje.Day, diasNoMesAnterior));
        }

        [Fact]
        public async Task Execute_DeveIgnorarVendasEstornadas()
        {
            var inicioMesAtual = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var estornada = Vender(inicioMesAtual, 80_000m);
            estornada.Estornar();

            var resultado = await CriarUseCase(estornada).Execute();

            resultado.MesAtual.Faturamento.Should().Be(0m);
            resultado.MesAtual.QuantidadeVendas.Should().Be(0);
        }

        [Fact]
        public async Task Execute_DiaDeCorteNuncaExcedeODoMesAnterior()
        {
            // Protege o caso 31/03 vs fevereiro: sem o clamp, o intervalo do mes
            // anterior terminaria depois do fim do proprio mes.
            var resultado = await CriarUseCase().Execute();
            var mesAnterior = DateTime.Today.AddMonths(-1);

            resultado.DiaDeCorte.Should()
                .BeLessThanOrEqualTo(DateTime.DaysInMonth(mesAnterior.Year, mesAnterior.Month));
        }
    }
}
