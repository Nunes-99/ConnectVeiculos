namespace ConnectVeiculos.Application.ViewModels.Catalogo
{
    public class CatalogoVeiculoViewModel
    {
        public int VeiId { get; set; }
        public string VeiMarca { get; set; }
        public string VeiModelo { get; set; }
        public short VeiAno { get; set; }
        public string VeiCor { get; set; }
        public int VeiKm { get; set; }
        public decimal VeiPreco { get; set; }
        public string VeiPlaca { get; set; }
        public string VeiObservacao { get; set; }
        public string VeiOpcionais { get; set; }
        public string LojaNome { get; set; }
        public string LojaCidade { get; set; }
        public string LojaEstado { get; set; }
        public string LojaWhatsApp { get; set; }
        public string LojaLogo { get; set; }
        public string CategoriaNome { get; set; }
        public List<string> Imagens { get; set; }

        public CatalogoVeiculoViewModel()
        {
            Imagens = new List<string>();
        }
    }

    public class CatalogoFiltroViewModel
    {
        public List<string> Marcas { get; set; }
        public int AnoMin { get; set; }
        public int AnoMax { get; set; }
        public decimal PrecoMin { get; set; }
        public decimal PrecoMax { get; set; }

        public CatalogoFiltroViewModel()
        {
            Marcas = new List<string>();
        }
    }

    public class CatalogoLojaViewModel
    {
        public int LojId { get; set; }
        public string LojNome { get; set; }
        public string LojCidade { get; set; }
        public string LojEstado { get; set; }
        public string LojTel1 { get; set; }
        public string LojWhatsApp { get; set; }
        public string LojEmail { get; set; }
        public string LojImg { get; set; }
        public string LojEndereco { get; set; }
        public string LojCorPrimaria { get; set; }
        public string LojCorSecundaria { get; set; }
        public string LojInstagram { get; set; }
        public string LojFacebook { get; set; }
        public string LojSlug { get; set; }
        public string LojUrlCatalogo { get; set; }
    }

    public class CatalogoLojaResumoViewModel
    {
        public int LojId { get; set; }
        public string LojNome { get; set; }
        public string LojSlug { get; set; }
    }

    public class CatalogoResultadoViewModel
    {
        public List<CatalogoVeiculoViewModel> Veiculos { get; set; }
        public CatalogoFiltroViewModel Filtros { get; set; }
        public CatalogoLojaViewModel Loja { get; set; }
        public List<CatalogoLojaResumoViewModel> Lojas { get; set; }
        public int Total { get; set; }

        public CatalogoResultadoViewModel()
        {
            Veiculos = new List<CatalogoVeiculoViewModel>();
            Filtros = new CatalogoFiltroViewModel();
            Lojas = new List<CatalogoLojaResumoViewModel>();
        }
    }
}
