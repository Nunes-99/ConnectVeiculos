using ConnectVeiculos.Application.Interfaces.Dashboard;
using ConnectVeiculos.Application.ViewModels.Dashboard;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using System.Globalization;

namespace ConnectVeiculos.Application.UseCases.Dashboard
{
    public class ConsultarFaturamentoMensalUseCase : IConsultarFaturamentoMensalUseCase
    {
        private readonly IVendaRepository _vendaRepository;
        private readonly IVeiculoRepository _veiculoRepository;

        public ConsultarFaturamentoMensalUseCase(
            IVendaRepository vendaRepository,
            IVeiculoRepository veiculoRepository)
        {
            _vendaRepository = vendaRepository;
            _veiculoRepository = veiculoRepository;
        }

        public async Task<FaturamentoMensalViewModel> Execute(int ano)
        {
            var todasVendas = await _vendaRepository.GetAllAsync();
            var todosVeiculos = await _veiculoRepository.GetAllAsync();

            var veiculosDict = todosVeiculos.ToDictionary(v => v.VeiId);

            var vendasAno = todasVendas
                .Where(v => v.VenStatus == "A" && v.VenDtVenda.Year == ano)
                .ToList();

            var meses = new List<FaturamentoMesViewModel>();
            var cultureInfo = new CultureInfo("pt-BR");

            for (int mes = 1; mes <= 12; mes++)
            {
                var vendasMes = vendasAno.Where(v => v.VenDtVenda.Month == mes).ToList();

                var faturamento = vendasMes.Sum(v => v.VenValor);
                var custo = vendasMes.Sum(v =>
                {
                    if (veiculosDict.TryGetValue(v.R_VeiId, out var veiculo))
                        return veiculo.VeiPrecoCompra;
                    return 0;
                });
                var comissoes = vendasMes.Sum(v => v.VenComissaoValor);

                meses.Add(new FaturamentoMesViewModel
                {
                    Mes = cultureInfo.DateTimeFormat.GetMonthName(mes),
                    Ano = ano,
                    Faturamento = faturamento,
                    Lucro = faturamento - custo - comissoes,
                    QuantidadeVendas = vendasMes.Count
                });
            }

            var totalAnual = meses.Sum(m => m.Faturamento);
            var mesesComVenda = meses.Count(m => m.Faturamento > 0);

            return new FaturamentoMensalViewModel
            {
                Meses = meses,
                TotalAnual = totalAnual,
                MediaMensal = mesesComVenda > 0 ? totalAnual / mesesComVenda : 0
            };
        }
    }
}
