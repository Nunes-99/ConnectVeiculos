export interface CatalogoVeiculo {
  veiId: number;
  veiMarca: string;
  veiModelo: string;
  veiAno: number;
  veiCor: string;
  veiKm: number;
  veiPreco: number;
  veiPlaca: string;
  veiObservacao?: string;
  veiOpcionais?: string;
  lojaNome: string;
  lojaCidade: string;
  lojaEstado: string;
  lojaWhatsApp: string;
  lojaLogo?: string;
  categoriaNome: string;
  imagens: string[];
}

export interface CatalogoFiltro {
  marcas: string[];
  anoMin: number;
  anoMax: number;
  precoMin: number;
  precoMax: number;
}

export interface CatalogoLoja {
  lojId: number;
  lojNome: string;
  lojSlug?: string;
  lojCidade: string;
  lojEstado: string;
  lojTel1: string;
  lojWhatsApp: string;
  lojEmail: string;
  lojImg: string;
  lojEndereco: string;
  lojCorPrimaria: string;
  lojCorSecundaria: string;
  lojInstagram?: string;
  lojFacebook?: string;
  lojUrlCatalogo?: string;

  // Personalizacao definida pelo admin no cadastro da loja.
  lojTema?: 'claro' | 'escuro';
  lojCorFundo?: string;
  lojBannerImg?: string;
  lojBannerTitulo?: string;
  lojBannerSubtitulo?: string;
  lojFavicon?: string;
  lojHorario?: string;
  lojSobre?: string;
  lojLinkVenderCarro?: string;
  lojMostrarMarcas?: boolean;
  lojMostrarMapa?: boolean;
}

export interface CatalogoLojaResumo {
  lojId: number;
  lojNome: string;
  lojSlug: string;
}

export interface CatalogoResultado {
  veiculos: CatalogoVeiculo[];
  filtros: CatalogoFiltro;
  loja?: CatalogoLoja;
  lojas: CatalogoLojaResumo[];
  total: number;
}
