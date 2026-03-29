namespace ConnectVeiculos.Application.InputModels.Veiculos
{
    /// <summary>
    /// Modelo para busca avancada de veiculos com multiplos filtros
    /// </summary>
    public class BuscaAvancadaVeiculoInputModel
    {
        /// <summary>
        /// Texto livre para busca (marca, modelo, placa, chassi, cor)
        /// </summary>
        public string? Texto { get; set; }

        /// <summary>
        /// Filtro por marca especifica
        /// </summary>
        public string? Marca { get; set; }

        /// <summary>
        /// Filtro por modelo especifico
        /// </summary>
        public string? Modelo { get; set; }

        /// <summary>
        /// Ano minimo do veiculo
        /// </summary>
        public int? AnoMinimo { get; set; }

        /// <summary>
        /// Ano maximo do veiculo
        /// </summary>
        public int? AnoMaximo { get; set; }

        /// <summary>
        /// Preco minimo
        /// </summary>
        public decimal? PrecoMinimo { get; set; }

        /// <summary>
        /// Preco maximo
        /// </summary>
        public decimal? PrecoMaximo { get; set; }

        /// <summary>
        /// Quilometragem maxima
        /// </summary>
        public int? KmMaximo { get; set; }

        /// <summary>
        /// Cor do veiculo
        /// </summary>
        public string? Cor { get; set; }

        /// <summary>
        /// ID da loja
        /// </summary>
        public int? LojaId { get; set; }

        /// <summary>
        /// ID da categoria
        /// </summary>
        public int? CategoriaId { get; set; }

        /// <summary>
        /// Status do veiculo (A = Ativo, I = Inativo, V = Vendido)
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Situacao do veiculo (Disponivel, Reservado, etc)
        /// </summary>
        public string? Situacao { get; set; }

        /// <summary>
        /// IDs das caracteristicas desejadas
        /// </summary>
        public List<int>? CaracteristicasIds { get; set; }

        /// <summary>
        /// Campo para ordenacao (preco, ano, km, dataEntrada)
        /// </summary>
        public string? OrdenarPor { get; set; }

        /// <summary>
        /// Direcao da ordenacao (asc ou desc)
        /// </summary>
        public string? Direcao { get; set; } = "desc";

        /// <summary>
        /// Pagina atual
        /// </summary>
        public int Pagina { get; set; } = 1;

        /// <summary>
        /// Quantidade por pagina
        /// </summary>
        public int TamanhoPagina { get; set; } = 10;
    }
}
