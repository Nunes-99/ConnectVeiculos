export interface Categoria {
  catId: number;
  catNome: string;
  catDesc: string;
  catSts: boolean;
}

export interface CategoriaInput {
  catId?: number;
  catNome: string;
  catDesc?: string;
  catSts: boolean;
}
