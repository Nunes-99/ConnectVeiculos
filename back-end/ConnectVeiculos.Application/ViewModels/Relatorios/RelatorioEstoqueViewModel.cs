namespace ConnectVeiculos.Application.ViewModels.Relatorios
{
    public class RelatorioEstoqueViewModel
    {
        public int TotalVeiculos { get; set; }
        public int VeiculosDisponiveis { get; set; }
        public int VeiculosVendidos { get; set; }
        public int VeiculosReservados { get; set; }
        public decimal ValorTotalEstoque { get; set; }
        public decimal ValorMedioVeiculo { get; set; }
        public List<EstoquePorLojaViewModel> EstoquePorLoja { get; set; } = new();
        public List<EstoquePorCategoriaViewModel> EstoquePorCategoria { get; set; } = new();
        public List<EstoquePorMarcaViewModel> EstoquePorMarca { get; set; } = new();
    }

    public class EstoquePorLojaViewModel
    {
        public int LojaId { get; set; }
        public string LojaNome { get; set; }
        public int Quantidade { get; set; }
        public decimal ValorTotal { get; set; }
    }

    public class EstoquePorCategoriaViewModel
    {
        public int CategoriaId { get; set; }
        public string CategoriaNome { get; set; }
        public int Quantidade { get; set; }
        public decimal ValorTotal { get; set; }
    }

    public class EstoquePorMarcaViewModel
    {
        public string Marca { get; set; }
        public int Quantidade { get; set; }
        public decimal ValorTotal { get; set; }
    }
}
