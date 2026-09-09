namespace ConnectVeiculos.Application.ViewModels.Lojas
{
    public class LojaViewModel
    {
        public int LojId { get; set; }
        public string LojNome { get; set; }
        public string LojLogradouro { get; set; }
        public string LojNumero { get; set; }
        public string LojBairro { get; set; }
        public string LojCidade { get; set; }
        public string LojEstado { get; set; }
        public string LojCEP { get; set; }
        public string LojComplemento { get; set; }
        public string LojEmail { get; set; }
        public string LojTel1 { get; set; }
        public string LojTel2 { get; set; }
        public string LojWhatsApp { get; set; }
        public string LojImg { get; set; }
        public string LojCNPJ { get; set; }
        public string LojIE { get; set; }
        public bool LojSts { get; set; }
        public string LojCorPrimaria { get; set; }
        public string LojCorSecundaria { get; set; }
        public string LojInstagram { get; set; }
        public string LojFacebook { get; set; }
        public string LojSlug { get; set; }
        public string LojUrlCatalogo { get; set; }
        public bool LojPadraoCatalogo { get; set; }

        // Personalizacao do catalogo publico (ver Loja.SetPersonalizacao)
        public string LojTema { get; set; }
        public string LojCorFundo { get; set; }
        public string LojBannerImg { get; set; }
        public string LojBannerTitulo { get; set; }
        public string LojBannerSubtitulo { get; set; }
        public string LojFavicon { get; set; }
        public string LojHorario { get; set; }
        public string LojSobre { get; set; }
        public string LojLinkVenderCarro { get; set; }
        public bool LojMostrarMarcas { get; set; } = true;
        public bool LojMostrarMapa { get; set; } = true;
    }
}
