using ConnectVeiculos.Application.Interfaces.Dashboard;
using ConnectVeiculos.Application.ViewModels.Dashboard;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using System.Globalization;

namespace ConnectVeiculos.Application.UseCases.Dashboard
{
    public class ConsultarComparativoMensalUseCase : IConsultarComparativoMensalUseCase
    {
        private readonly IVendaRepository _vendaRepository;
        private readonly IVeiculoRepository _veiculoRepository;

        public ConsultarComparativoMensalUseCase(
            IVendaRepository vendaRepository,
            IVeiculoRepository veiculoRepository)
        {
            _vendaRepository = vendaRepository;
            _veiculoRepository = veiculoRepository;
        }

        public async Task<ComparativoMensalViewModel> Execute()
        {
            var todasVendas = await _vendaRepository.GetAllAsync();
            var todosVeiculos = await _veiculoRepository.GetAllAsync();

            var veiculosDict = todosVeiculos.ToDictionary(v => v.VeiId);

            var hoje = DateTime.Today;
            var inicioMesAtual = new DateTime(hoje.Year, hoje.Month, 1);
            var inicioMesAnterior = inicioMesAtual.AddMonths(-1);
            var fimMesAnterior = inicioMesAtual.AddDays(-1);

            var vendasMesAtual = todasVendas
                .Where(v => v.VenStatus == "A" && v.VenDtVenda >= inicioMesAtual)
                .ToList();

            var vendasMesAnterior = todasVendas
                .Where(v => v.VenStatus == "A" && v.VenDtVenda >= inicioMesAnterior && v.VenDtVenda <= fimMesAnterior)
                .ToList();

            var mesAtual = CalcularComparativo(vendasMesAtual, veiculosDict, inicioMesAtual);
            var mesAnterior = CalcularComparativo(vendasMesAnterior, veiculosDict, inicioMesAnterior);

            return new ComparativoMensalViewModel
            {
                MesAtual = mesAtual,
                MesAnterior = mesAnterior,
                VariacaoFaturamento = mesAnterior.Faturamento > 0
                    ? ((mesAtual.Faturamento - mesAnterior.Faturamento) / mesAnterior.Faturamento) * 100
                    : mesAtual.Faturamento > 0 ? 100 : 0,
                VariacaoQuantidade = mesAnterior.QuantidadeVendas > 0
                    ? ((decimal)(mesAtual.QuantidadeVendas - mesAnterior.QuantidadeVendas) / mesAnterior.QuantidadeVendas) * 100
                    : mesAtual.QuantidadeVendas > 0 ? 100 : 0,
                VariacaoTicketMedio = mesAnterior.TicketMedio > 0
                    ? ((mesAtual.TicketMedio - mesAnterior.TicketMedio) / mesAnterior.TicketMedio) * 100
                    : mesAtual.TicketMedio > 0 ? 100 : 0
            };
        }

        private ComparativoMesViewModel CalcularComparativo(
            List<Core.Entities.Vendas.Venda> vendas,
            Dictionary<int, Core.Entities.Veiculos.Veiculo> veiculosDict,
            DateTime dataReferencia)
        {
            var cultureInfo = new CultureInfo("pt-BR");
            var faturamento = vendas.Sum(v => v.VenValor);
            var custo = vendas.Sum(v =>
            {
                if (veiculosDict.TryGetValue(v.R_VeiId, out var veiculo))
                    return veiculo.VeiPrecoCompra;
                return 0;
            });
            var comissoes = vendas.Sum(v => v.VenComissaoValor);

            return new ComparativoMesViewModel
            {
                Periodo = $"{cultureInfo.DateTimeFormat.GetMonthName(dataReferencia.Month)}/{dataReferencia.Year}",
                Faturamento = faturamento,
                QuantidadeVendas = vendas.Count,
                TicketMedio = vendas.Count > 0 ? faturamento / vendas.Count : 0,
                Lucro = faturamento - custo - comissoes
            };
        }
    }
}
