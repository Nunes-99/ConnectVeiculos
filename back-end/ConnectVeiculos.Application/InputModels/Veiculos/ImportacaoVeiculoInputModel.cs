namespace ConnectVeiculos.Application.InputModels.Veiculos
{
    /// <summary>
    /// Modelo para importacao de veiculo via CSV/XML
    /// </summary>
    public class ImportacaoVeiculoInputModel
    {
        public string Marca { get; set; }
        public string Modelo { get; set; }
        public short Ano { get; set; }
        public string Placa { get; set; }
        public string Chassi { get; set; }
        public string Cor { get; set; }
        public int Km { get; set; }
        public decimal Preco { get; set; }
        public decimal PrecoCompra { get; set; }
        public string Categoria { get; set; }
        public DateTime? DataEntrada { get; set; }
        public string Status { get; set; }
        public string Situacao { get; set; }
    }

    /// <summary>
    /// Resultado da importacao de um veiculo
    /// </summary>
    public class ImportacaoVeiculoResultado
    {
        public int Linha { get; set; }
        public bool Sucesso { get; set; }
        public string Mensagem { get; set; }
        public string Placa { get; set; }
        public string Modelo { get; set; }
        public int? VeiculoId { get; set; }
    }

    /// <summary>
    /// Resultado geral da importacao
    /// </summary>
    public class ImportacaoResultado
    {
        public int TotalLinhas { get; set; }
        public int Importados { get; set; }
        public int Erros { get; set; }
        public List<ImportacaoVeiculoResultado> Detalhes { get; set; } = new();
    }
}
