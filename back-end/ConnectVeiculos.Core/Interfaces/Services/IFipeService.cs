namespace ConnectVeiculos.Core.Interfaces.Services
{
    public interface IFipeService
    {
        Task<IEnumerable<FipeMarca>> GetMarcasAsync(FipeTipoVeiculo tipo);
        Task<IEnumerable<FipeModelo>> GetModelosAsync(FipeTipoVeiculo tipo, int codigoMarca);
        Task<IEnumerable<FipeAno>> GetAnosAsync(FipeTipoVeiculo tipo, int codigoMarca, int codigoModelo);
        Task<FipePreco> GetPrecoAsync(FipeTipoVeiculo tipo, int codigoMarca, int codigoModelo, string codigoAno);
    }

    public enum FipeTipoVeiculo
    {
        Carros = 1,
        Motos = 2,
        Caminhoes = 3
    }

    public class FipeMarca
    {
        public int Codigo { get; set; }
        public string Nome { get; set; }
    }

    public class FipeModelo
    {
        public int Codigo { get; set; }
        public string Nome { get; set; }
    }

    public class FipeAno
    {
        public string Codigo { get; set; }
        public string Nome { get; set; }
    }

    public class FipePreco
    {
        public string TipoVeiculo { get; set; }
        public string Valor { get; set; }
        public string Marca { get; set; }
        public string Modelo { get; set; }
        public int AnoModelo { get; set; }
        public string Combustivel { get; set; }
        public string CodigoFipe { get; set; }
        public string MesReferencia { get; set; }
        public string SiglaCombustivel { get; set; }
    }
}
