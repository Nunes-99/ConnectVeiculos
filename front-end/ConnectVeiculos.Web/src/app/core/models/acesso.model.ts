export interface Acesso {
  acsId: number;
  acsNome: string;
  acsDesc: string;
  acsSts: boolean;
}

export interface AcessoInput {
  acsId?: number;
  acsNome: string;
  acsDesc?: string;
  acsSts: boolean;
}
