namespace ConnectVeiculos.Application.ViewModels.Veiculos
{
    public class VeiculoViewModel
    {
        public int VeiId { get; set; }
        public int R_LojId { get; set; }
        public string LojaNome { get; set; }
        public int R_CatId { get; set; }
        public string CategoriaNome { get; set; }
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
        public bool VeiPostadoInsta { get; set; }
        public bool VeiPostadoFace { get; set; }
        public DateTime? VeiDtPostagemInsta { get; set; }
        public DateTime? VeiDtPostagemFace { get; set; }
        public List<CaracteristicaVeiculoViewModel> Caracteristicas { get; set; }
        public List<ObservacaoVeiculoViewModel> Observacoes { get; set; }
        public List<ImagemVeiculoViewModel> Imagens { get; set; }

        public VeiculoViewModel()
        {
            Caracteristicas = new List<CaracteristicaVeiculoViewModel>();
            Observacoes = new List<ObservacaoVeiculoViewModel>();
            Imagens = new List<ImagemVeiculoViewModel>();
        }
    }

    public class CaracteristicaVeiculoViewModel
    {
        public int CarId { get; set; }
        public string CarNome { get; set; }
    }

    public class ObservacaoVeiculoViewModel
    {
        public int ObsId { get; set; }
        public string ObsNome { get; set; }
    }

    public class ImagemVeiculoViewModel
    {
        public int ImgId { get; set; }
        public string ImgCaminho { get; set; }
        public int ImgOrdem { get; set; }
    }
}
