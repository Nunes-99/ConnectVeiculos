using ConnectVeiculos.Application.Interfaces.Relatorios;
using ConnectVeiculos.Application.ViewModels.Relatorios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Lojas;

namespace ConnectVeiculos.Application.UseCases.Relatorios
{
    public class ConsultarRelatorioFinanceiroUseCase : IConsultarRelatorioFinanceiroUseCase
    {
        private readonly IVendaRepository _vendaRepository;
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly ILojaRepository _lojaRepository;

        public ConsultarRelatorioFinanceiroUseCase(
            IVendaRepository vendaRepository,
            IVeiculoRepository veiculoRepository,
            ILojaRepository lojaRepository)
        {
            _vendaRepository = vendaRepository;
            _veiculoRepository = veiculoRepository;
            _lojaRepository = lojaRepository;
        }

        public async Task<RelatorioFinanceiroViewModel> Execute(DateTime? dataInicio = null, DateTime? dataFim = null, int? lojaId = null)
        {
            var todasVendas = await _vendaRepository.GetAllAsync();
            var todosVeiculos = await _veiculoRepository.GetAllAsync();
            var lojas = (await _lojaRepository.GetAllAsync()).ToList();

            var vendas = todasVendas.ToList();
            var veiculos = todosVeiculos.ToDictionary(v => v.VeiId);

            // Aplicar filtros de data
            if (dataInicio.HasValue)
                vendas = vendas.Where(v => v.VenDtVenda >= dataInicio.Value).ToList();

            if (dataFim.HasValue)
                vendas = vendas.Where(v => v.VenDtVenda <= dataFim.Value.Date.AddDays(1).AddSeconds(-1)).ToList();

            // Filtrar por loja atraves do veiculo
            if (lojaId.HasValue)
            {
                vendas = vendas.Where(v =>
                {
                    if (veiculos.TryGetValue(v.R_VeiId, out var veiculo))
                        return veiculo.R_LojId == lojaId.Value;
                    return false;
                }).ToList();
            }

            // Apenas vendas ativas para calculos financeiros
            var vendasAtivas = vendas.Where(v => v.VenStatus == "A").ToList();

            // Calcular receita e custo
            decimal receitaBruta = vendasAtivas.Sum(v => v.VenValor);
            decimal custoTotal = vendasAtivas.Sum(v =>
            {
                if (veiculos.TryGetValue(v.R_VeiId, out var veiculo))
                    return veiculo.VeiPrecoCompra;
                return 0;
            });
            decimal totalComissoes = vendasAtivas.Sum(v => v.VenComissaoValor);
            decimal lucroBruto = receitaBruta - custoTotal;
            decimal lucroLiquido = lucroBruto - totalComissoes;

            var resultado = new RelatorioFinanceiroViewModel
            {
                ReceitaBruta = receitaBruta,
                CustoTotal = custoTotal,
                LucroBruto = lucroBruto,
                TotalComissoes = totalComissoes,
                LucroLiquido = lucroLiquido,
                MargemLucro = receitaBruta > 0 ? (lucroLiquido / receitaBruta) * 100 : 0,
                TicketMedio = vendasAtivas.Count > 0 ? receitaBruta / vendasAtivas.Count : 0,

                // Financeiro por mes (ultimos 12 meses)
                FinanceiroPorMes = vendasAtivas
                    .GroupBy(v => new { v.VenDtVenda.Year, v.VenDtVenda.Month })
                    .OrderByDescending(g => g.Key.Year)
                    .ThenByDescending(g => g.Key.Month)
                    .Take(12)
                    .Select(g =>
                    {
                        var receita = g.Sum(v => v.VenValor);
                        var custo = g.Sum(v =>
                        {
                            if (veiculos.TryGetValue(v.R_VeiId, out var veiculo))
                                return veiculo.VeiPrecoCompra;
                            return 0;
                        });
                        return new FinanceiroPorMesViewModel
                        {
                            Periodo = $"{g.Key.Month:D2}/{g.Key.Year}",
                            Receita = receita,
                            Custo = custo,
                            Lucro = receita - custo - g.Sum(v => v.VenComissaoValor)
                        };
                    })
                    .Reverse()
                    .ToList(),

                // Financeiro por loja
                FinanceiroPorLoja = vendasAtivas
                    .GroupBy(v =>
                    {
                        if (veiculos.TryGetValue(v.R_VeiId, out var veiculo))
                            return veiculo.R_LojId;
                        return 0;
                    })
                    .Where(g => g.Key > 0)
                    .Select(g =>
                    {
                        var loja = lojas.FirstOrDefault(l => l.LojId == g.Key);
                        var receita = g.Sum(v => v.VenValor);
                        var custo = g.Sum(v =>
                        {
                            if (veiculos.TryGetValue(v.R_VeiId, out var veiculo))
                                return veiculo.VeiPrecoCompra;
                            return 0;
                        });
                        return new FinanceiroPorLojaViewModel
                        {
                            LojaId = g.Key,
                            LojaNome = loja?.LojNome ?? "Desconhecida",
                            Receita = receita,
                            Custo = custo,
                            Lucro = receita - custo - g.Sum(v => v.VenComissaoValor)
                        };
                    })
                    .OrderByDescending(f => f.Lucro)
                    .ToList()
            };

            return resultado;
        }
    }
}
