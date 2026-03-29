export interface Veiculo {
  veiId: number;
  r_LojId: number;
  lojaNome: string;
  r_CatId: number;
  categoriaNome: string;
  veiMarca: string;
  veiModelo: string;
  veiAno: number;
  veiPlaca: string;
  veiChassi: string;
  veiCor: string;
  veiKm: number;
  veiPreco: number;
  veiDtEntrada: Date;
  veiSts: string;
  veiSitSts: string;
  veiPrecoCompra: number;
  veiObservacao?: string;
  caracteristicas?: CaracteristicaVeiculo[];
  observacoes?: ObservacaoVeiculo[];
  imagens?: ImagemVeiculo[];
  veiPostadoInsta?: boolean;
  veiPostadoFace?: boolean;
}

export interface VeiculoInput {
  veiId?: number;
  r_LojId: number;
  r_CatId: number;
  veiMarca: string;
  veiModelo: string;
  veiAno: number;
  veiPlaca?: string;
  veiChassi?: string;
  veiCor?: string;
  veiKm?: number;
  veiPreco: number;
  veiDtEntrada?: Date;
  veiSts: string;
  veiSitSts?: string;
  veiPrecoCompra?: number;
  veiObservacao?: string;
  caracteristicasIds?: number[];
  observacoesIds?: number[];
}

export interface CaracteristicaVeiculo {
  carId: number;
  carNome: string;
}

export interface ObservacaoVeiculo {
  obsId: number;
  obsNome: string;
}

export interface ImagemVeiculo {
  imgId: number;
  imgCaminho: string;
  imgOrdem: number;
}
