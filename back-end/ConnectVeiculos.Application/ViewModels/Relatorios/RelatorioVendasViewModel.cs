namespace ConnectVeiculos.Application.ViewModels.Relatorios
{
    public class RelatorioVendasViewModel
    {
        public int TotalVendas { get; set; }
        public decimal ValorTotalVendas { get; set; }
        public decimal TotalComissoes { get; set; }
        public int VendasAtivas { get; set; }
        public int VendasEstornadas { get; set; }
        public List<VendaPorPeriodoViewModel> VendasPorMes { get; set; } = new();
        public List<VendaPorVendedorViewModel> VendasPorVendedor { get; set; } = new();
        public List<VendaPorFormaPagamentoViewModel> VendasPorFormaPagamento { get; set; } = new();
    }

    public class VendaPorPeriodoViewModel
    {
        public string Periodo { get; set; }
        public int Quantidade { get; set; }
        public decimal ValorTotal { get; set; }
    }

    public class VendaPorVendedorViewModel
    {
        public int VendedorId { get; set; }
        public string VendedorNome { get; set; }
        public int Quantidade { get; set; }
        public decimal ValorTotal { get; set; }
        public decimal TotalComissoes { get; set; }
    }

    public class VendaPorFormaPagamentoViewModel
    {
        public string FormaPagamento { get; set; }
        public int Quantidade { get; set; }
        public decimal ValorTotal { get; set; }
    }
}
