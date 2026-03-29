namespace ConnectVeiculos.Application.ViewModels.Dashboard
{
    /// <summary>
    /// Vendas por periodo (para grafico de linha)
    /// </summary>
    public class VendasPorPeriodoViewModel
    {
        public List<VendaDiaViewModel> Vendas { get; set; } = new();
        public decimal TotalPeriodo { get; set; }
        public int QuantidadeVendas { get; set; }
    }

    public class VendaDiaViewModel
    {
        public DateTime Data { get; set; }
        public int Quantidade { get; set; }
        public decimal Valor { get; set; }
    }

    /// <summary>
    /// Faturamento mensal (para grafico de barras)
    /// </summary>
    public class FaturamentoMensalViewModel
    {
        public List<FaturamentoMesViewModel> Meses { get; set; } = new();
        public decimal TotalAnual { get; set; }
        public decimal MediaMensal { get; set; }
    }

    public class FaturamentoMesViewModel
    {
        public string Mes { get; set; } = string.Empty;
        public int Ano { get; set; }
        public decimal Faturamento { get; set; }
        public decimal Lucro { get; set; }
        public int QuantidadeVendas { get; set; }
    }

    /// <summary>
    /// Top veiculos vendidos
    /// </summary>
    public class TopVeiculosVendidosViewModel
    {
        public List<VeiculoVendidoViewModel> Veiculos { get; set; } = new();
    }

    public class VeiculoVendidoViewModel
    {
        public string Marca { get; set; } = string.Empty;
        public string Modelo { get; set; } = string.Empty;
        public int QuantidadeVendida { get; set; }
        public decimal ValorTotalVendas { get; set; }
        public decimal TicketMedio { get; set; }
    }

    /// <summary>
    /// Comparativo mensal (mes atual vs anterior)
    /// </summary>
    public class ComparativoMensalViewModel
    {
        public ComparativoMesViewModel MesAtual { get; set; } = new();
        public ComparativoMesViewModel MesAnterior { get; set; } = new();
        public decimal VariacaoFaturamento { get; set; }
        public decimal VariacaoQuantidade { get; set; }
        public decimal VariacaoTicketMedio { get; set; }
    }

    public class ComparativoMesViewModel
    {
        public string Periodo { get; set; } = string.Empty;
        public decimal Faturamento { get; set; }
        public int QuantidadeVendas { get; set; }
        public decimal TicketMedio { get; set; }
        public decimal Lucro { get; set; }
    }
}
