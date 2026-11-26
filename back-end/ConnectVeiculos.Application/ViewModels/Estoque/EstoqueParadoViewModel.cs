using ConnectVeiculos.Application.UseCases.Estoque;

namespace ConnectVeiculos.Application.ViewModels.Estoque
{
    public class EstoqueParadoViewModel
    {
        public EstoqueParadoConfig Config { get; set; } = new();
        public int Disponiveis { get; set; }
        public int Atencao { get; set; }
        public int Critico { get; set; }
        /// <summary>Soma do custo (compra + despesas) dos carros em atencao/critico: dinheiro parado.</summary>
        public decimal CustoParado { get; set; }
        /// <summary>Media de dias em estoque dos disponiveis.</summary>
        public int MediaDias { get; set; }
        /// <summary>Carros em atencao ou critico, do mais antigo ao mais novo.</summary>
        public List<EstoqueParadoItemViewModel> Veiculos { get; set; } = new();
    }

    public class EstoqueParadoItemViewModel
    {
        public int VeiId { get; set; }
        public string Descricao { get; set; } = "";
        public string? Placa { get; set; }
        public string? Loja { get; set; }
        public int Dias { get; set; }
        public DateTime Entrada { get; set; }
        public decimal Preco { get; set; }
        public decimal? Fipe { get; set; }
        /// <summary>Compra + despesas; 0 sem preco de compra.</summary>
        public decimal Custo { get; set; }
        public decimal Despesas { get; set; }
        /// <summary>"normal", "atencao" ou "critico".</summary>
        public string Faixa { get; set; } = "normal";
        public decimal PrecoSugerido { get; set; }
        public bool Mudar { get; set; }
        public decimal PrecoMinimo { get; set; }
        public decimal? MargemAtual { get; set; }
        public decimal? DiferencaFipe { get; set; }
        public string Motivo { get; set; } = "";
    }
}
