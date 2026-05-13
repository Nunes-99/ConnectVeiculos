export interface Loja {
  lojId: number;
  lojNome: string;
  lojSlug?: string;
  lojLogradouro: string;
  lojNumero: string;
  lojBairro: string;
  lojCidade: string;
  lojEstado: string;
  lojCEP: string;
  lojComplemento: string;
  lojEmail: string;
  lojTel1: string;
  lojTel2: string;
  lojWhatsApp: string;
  lojImg: string;
  lojCNPJ: string;
  lojIE: string;
  lojSts: boolean;
  lojCorPrimaria: string;
  lojCorSecundaria: string;
  lojInstagram?: string;
  lojFacebook?: string;
  lojUrlCatalogo?: string;
  lojPadraoCatalogo?: boolean;
}

export interface LojaInput {
  lojId?: number;
  lojNome: string;
  lojSlug?: string;
  lojLogradouro?: string;
  lojNumero?: string;
  lojBairro?: string;
  lojCidade?: string;
  lojEstado?: string;
  lojCEP?: string;
  lojComplemento?: string;
  lojEmail?: string;
  lojTel1?: string;
  lojTel2?: string;
  lojWhatsApp?: string;
  lojImg?: string;
  lojCNPJ?: string;
  lojIE?: string;
  lojSts: boolean;
  lojCorPrimaria?: string;
  lojCorSecundaria?: string;
  lojInstagram?: string;
  lojFacebook?: string;
  lojUrlCatalogo?: string;
  lojPadraoCatalogo?: boolean;
}
