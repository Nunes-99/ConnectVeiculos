namespace ConnectVeiculos.Application.ViewModels.Relatorios
{
    public class RelatorioFinanceiroViewModel
    {
        public decimal ReceitaBruta { get; set; }
        public decimal CustoTotal { get; set; }
        public decimal LucroBruto { get; set; }
        public decimal TotalComissoes { get; set; }
        public decimal LucroLiquido { get; set; }
        public decimal MargemLucro { get; set; }
        public decimal TicketMedio { get; set; }
        public List<FinanceiroPorMesViewModel> FinanceiroPorMes { get; set; } = new();
        public List<FinanceiroPorLojaViewModel> FinanceiroPorLoja { get; set; } = new();
    }

    public class FinanceiroPorMesViewModel
    {
        public string Periodo { get; set; }
        public decimal Receita { get; set; }
        public decimal Custo { get; set; }
        public decimal Lucro { get; set; }
    }

    public class FinanceiroPorLojaViewModel
    {
        public int LojaId { get; set; }
        public string LojaNome { get; set; }
        public decimal Receita { get; set; }
        public decimal Custo { get; set; }
        public decimal Lucro { get; set; }
    }
}
