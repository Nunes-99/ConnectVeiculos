namespace ConnectVeiculos.Application.ViewModels.Dashboard
{
    public class LucroDashboardViewModel
    {
        public decimal Receita { get; set; }
        public decimal CustoCompra { get; set; }
        public decimal Despesas { get; set; }
        public decimal Comissoes { get; set; }
        public decimal LucroLiquido { get; set; }
        public decimal MargemMedia { get; set; }
        public int TotalVendas { get; set; }

        public List<LucroPorMesViewModel> LucroPorMes { get; set; } = new();
        public List<TopVeiculoRentavelViewModel> TopVeiculosRentaveis { get; set; } = new();
    }

    public class LucroPorMesViewModel
    {
        public string Periodo { get; set; } = "";
        public decimal Receita { get; set; }
        public decimal Lucro { get; set; }
    }

    public class TopVeiculoRentavelViewModel
    {
        public int VeiId { get; set; }
        public string Marca { get; set; } = "";
        public string Modelo { get; set; } = "";
        public short Ano { get; set; }
        public decimal PrecoVenda { get; set; }
        public decimal PrecoCompra { get; set; }
        public decimal Despesas { get; set; }
        public decimal Lucro { get; set; }
        public decimal Margem { get; set; }
    }
}
