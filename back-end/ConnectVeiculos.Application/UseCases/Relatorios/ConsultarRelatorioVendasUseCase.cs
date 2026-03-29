using ConnectVeiculos.Application.Interfaces.Relatorios;
using ConnectVeiculos.Application.ViewModels.Relatorios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Vendas;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Usuarios;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;

namespace ConnectVeiculos.Application.UseCases.Relatorios
{
    public class ConsultarRelatorioVendasUseCase : IConsultarRelatorioVendasUseCase
    {
        private readonly IVendaRepository _vendaRepository;
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IVeiculoRepository _veiculoRepository;

        public ConsultarRelatorioVendasUseCase(
            IVendaRepository vendaRepository,
            IUsuarioRepository usuarioRepository,
            IVeiculoRepository veiculoRepository)
        {
            _vendaRepository = vendaRepository;
            _usuarioRepository = usuarioRepository;
            _veiculoRepository = veiculoRepository;
        }

        public async Task<RelatorioVendasViewModel> Execute(DateTime? dataInicio = null, DateTime? dataFim = null, int? lojaId = null)
        {
            var todasVendas = await _vendaRepository.GetAllAsync();
            var todosVeiculos = await _veiculoRepository.GetAllAsync();
            var vendas = todasVendas.ToList();
            var veiculos = todosVeiculos.ToDictionary(v => v.VeiId);

            // Aplicar filtros
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

            var usuarios = (await _usuarioRepository.GetAllAsync()).ToList();

            var vendasAtivas = vendas.Where(v => v.VenStatus == "A").ToList();

            var resultado = new RelatorioVendasViewModel
            {
                TotalVendas = vendas.Count,
                ValorTotalVendas = vendasAtivas.Sum(v => v.VenValor),
                TotalComissoes = vendasAtivas.Sum(v => v.VenComissaoValor),
                VendasAtivas = vendasAtivas.Count,
                VendasEstornadas = vendas.Count(v => v.VenStatus == "E"),

                // Vendas por mes (ultimos 12 meses)
                VendasPorMes = vendasAtivas
                    .GroupBy(v => new { v.VenDtVenda.Year, v.VenDtVenda.Month })
                    .OrderByDescending(g => g.Key.Year)
                    .ThenByDescending(g => g.Key.Month)
                    .Take(12)
                    .Select(g => new VendaPorPeriodoViewModel
                    {
                        Periodo = $"{g.Key.Month:D2}/{g.Key.Year}",
                        Quantidade = g.Count(),
                        ValorTotal = g.Sum(v => v.VenValor)
                    })
                    .Reverse()
                    .ToList(),

                // Vendas por vendedor
                VendasPorVendedor = vendasAtivas
                    .GroupBy(v => v.R_UsuId)
                    .Select(g =>
                    {
                        var usuario = usuarios.FirstOrDefault(u => u.UsuId == g.Key);
                        return new VendaPorVendedorViewModel
                        {
                            VendedorId = g.Key,
                            VendedorNome = usuario?.UsuNome ?? "Desconhecido",
                            Quantidade = g.Count(),
                            ValorTotal = g.Sum(v => v.VenValor),
                            TotalComissoes = g.Sum(v => v.VenComissaoValor)
                        };
                    })
                    .OrderByDescending(v => v.ValorTotal)
                    .ToList(),

                // Vendas por forma de pagamento
                VendasPorFormaPagamento = vendasAtivas
                    .GroupBy(v => v.VenFormaPagamento ?? "NAO_INFORMADO")
                    .Select(g => new VendaPorFormaPagamentoViewModel
                    {
                        FormaPagamento = GetFormaPagamentoLabel(g.Key),
                        Quantidade = g.Count(),
                        ValorTotal = g.Sum(v => v.VenValor)
                    })
                    .OrderByDescending(v => v.ValorTotal)
                    .ToList()
            };

            return resultado;
        }

        private string GetFormaPagamentoLabel(string forma)
        {
            return forma switch
            {
                "DINHEIRO" => "Dinheiro",
                "PIX" => "PIX",
                "CARTAO_CREDITO" => "Cartao de Credito",
                "CARTAO_DEBITO" => "Cartao de Debito",
                "FINANCIAMENTO" => "Financiamento",
                "CONSORCIO" => "Consorcio",
                "TROCA" => "Troca",
                _ => "Nao Informado"
            };
        }
    }
}
