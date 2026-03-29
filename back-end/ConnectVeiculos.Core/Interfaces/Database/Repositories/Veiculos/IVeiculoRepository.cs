using ConnectVeiculos.Core.Entities.Veiculos;

namespace ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos
{
    public interface IVeiculoRepository
    {
        Task<Veiculo> GetByIdAsync(int id);
        Task<Veiculo> GetByPlacaAsync(string placa);
        Task<IEnumerable<Veiculo>> GetAllAsync();
        Task<IEnumerable<Veiculo>> GetByLojaIdAsync(int lojaId);
        Task<(IEnumerable<Veiculo> Items, int Total)> GetPagedAsync(int page, int pageSize, string? search = null, int? lojaId = null);
        Task<(IEnumerable<Veiculo> Items, int Total)> BuscaAvancadaAsync(BuscaAvancadaParams parametros);
        Task<int> CreateAsync(Veiculo veiculo);
        Task UpdateAsync(Veiculo veiculo);
        Task DeleteAsync(int id);
    }

    /// <summary>
    /// Parametros para busca avancada de veiculos
    /// </summary>
    public class BuscaAvancadaParams
    {
        public string? Texto { get; set; }
        public string? Marca { get; set; }
        public string? Modelo { get; set; }
        public int? AnoMinimo { get; set; }
        public int? AnoMaximo { get; set; }
        public decimal? PrecoMinimo { get; set; }
        public decimal? PrecoMaximo { get; set; }
        public int? KmMaximo { get; set; }
        public string? Cor { get; set; }
        public int? LojaId { get; set; }
        public int? CategoriaId { get; set; }
        public string? Status { get; set; }
        public string? Situacao { get; set; }
        public List<int>? CaracteristicasIds { get; set; }
        public string? OrdenarPor { get; set; }
        public string Direcao { get; set; } = "desc";
        public int Pagina { get; set; } = 1;
        public int TamanhoPagina { get; set; } = 10;
    }
}
