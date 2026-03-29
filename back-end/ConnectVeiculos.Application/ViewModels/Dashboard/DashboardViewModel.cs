namespace ConnectVeiculos.Application.ViewModels.Dashboard
{
    public class DashboardViewModel
    {
        public int TotalVeiculos { get; set; }
        public int VeiculosDisponiveis { get; set; }
        public int VeiculosVendidos { get; set; }
        public int VeiculosReservados { get; set; }
        public decimal ValorTotalEstoque { get; set; }
        public decimal ValorMedioVeiculo { get; set; }
        public int TotalLojas { get; set; }
        public int TotalCategorias { get; set; }
        public int TotalUsuarios { get; set; }
        public List<VeiculoPorCategoriaViewModel> VeiculosPorCategoria { get; set; }
        public List<VeiculoPorLojaViewModel> VeiculosPorLoja { get; set; }
        public List<VeiculoRecenteViewModel> VeiculosRecentes { get; set; }

        public DashboardViewModel()
        {
            VeiculosPorCategoria = new List<VeiculoPorCategoriaViewModel>();
            VeiculosPorLoja = new List<VeiculoPorLojaViewModel>();
            VeiculosRecentes = new List<VeiculoRecenteViewModel>();
        }
    }

    public class VeiculoPorCategoriaViewModel
    {
        public string Categoria { get; set; }
        public int Quantidade { get; set; }
    }

    public class VeiculoPorLojaViewModel
    {
        public string Loja { get; set; }
        public int Quantidade { get; set; }
        public decimal ValorTotal { get; set; }
    }

    public class VeiculoRecenteViewModel
    {
        public int VeiId { get; set; }
        public string Marca { get; set; }
        public string Modelo { get; set; }
        public short Ano { get; set; }
        public decimal Preco { get; set; }
        public string Status { get; set; }
    }
}
