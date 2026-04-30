using ConnectVeiculos.Application.Exceptions;

namespace ConnectVeiculos.Application.InputModels.Veiculos
{
    public class VeiculoInputModel
    {
        public int VeiId { get; set; }
        public int R_LojId { get; set; }
        public int R_CatId { get; set; }
        public string VeiMarca { get; set; }
        public string VeiModelo { get; set; }
        public short VeiAno { get; set; }
        public string VeiPlaca { get; set; }
        public string VeiChassi { get; set; }
        public string VeiCor { get; set; }
        public int VeiKm { get; set; }
        public decimal VeiPreco { get; set; }
        public DateTime VeiDtEntrada { get; set; }
        public string VeiSts { get; set; }
        public string VeiSitSts { get; set; }
        public decimal VeiPrecoCompra { get; set; }
        public string VeiObservacao { get; set; }
        public string VeiOpcionais { get; set; }
        public string VeiDonoAtual { get; set; }
        public string VeiDonoCelular { get; set; }
        public decimal? VeiPrecoFipe { get; set; }
        public List<int> CaracteristicasIds { get; set; }
        public List<int> ObservacoesIds { get; set; }

        public VeiculoInputModel()
        {
            CaracteristicasIds = new List<int>();
            ObservacoesIds = new List<int>();
        }

        public VeiculoInputModel(int veiId, int rLojId, int rCatId, string veiMarca, string veiModelo,
            short veiAno, string veiPlaca, string veiChassi, string veiCor, int veiKm,
            decimal veiPreco, DateTime veiDtEntrada, string veiSts, string veiSitSts, decimal veiPrecoCompra)
        {
            VeiId = veiId;
            R_LojId = rLojId;
            R_CatId = rCatId;
            VeiMarca = veiMarca;
            VeiModelo = veiModelo;
            VeiAno = veiAno;
            VeiPlaca = veiPlaca;
            VeiChassi = veiChassi;
            VeiCor = veiCor;
            VeiKm = veiKm;
            VeiPreco = veiPreco;
            VeiDtEntrada = veiDtEntrada;
            VeiSts = veiSts;
            VeiSitSts = veiSitSts;
            VeiPrecoCompra = veiPrecoCompra;
            CaracteristicasIds = new List<int>();
            ObservacoesIds = new List<int>();

            Validate();
        }

        private void Validate()
        {
            if (R_LojId <= 0)
                throw new InputModelException("A loja é obrigatória.");

            if (R_CatId <= 0)
                throw new InputModelException("A categoria é obrigatória.");
        }
    }
}
