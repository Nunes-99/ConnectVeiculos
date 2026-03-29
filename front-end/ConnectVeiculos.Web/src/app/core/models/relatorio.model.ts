export interface RelatorioVendas {
  totalVendas: number;
  valorTotalVendas: number;
  totalComissoes: number;
  vendasAtivas: number;
  vendasEstornadas: number;
  vendasPorMes: VendaPorPeriodo[];
  vendasPorVendedor: VendaPorVendedor[];
  vendasPorFormaPagamento: VendaPorFormaPagamento[];
}

export interface VendaPorPeriodo {
  periodo: string;
  quantidade: number;
  valorTotal: number;
}

export interface VendaPorVendedor {
  vendedorId: number;
  vendedorNome: string;
  quantidade: number;
  valorTotal: number;
  totalComissoes: number;
}

export interface VendaPorFormaPagamento {
  formaPagamento: string;
  quantidade: number;
  valorTotal: number;
}

export interface RelatorioEstoque {
  totalVeiculos: number;
  veiculosDisponiveis: number;
  veiculosVendidos: number;
  veiculosReservados: number;
  valorTotalEstoque: number;
  valorMedioVeiculo: number;
  estoquePorLoja: EstoquePorLoja[];
  estoquePorCategoria: EstoquePorCategoria[];
  estoquePorMarca: EstoquePorMarca[];
}

export interface EstoquePorLoja {
  lojaId: number;
  lojaNome: string;
  quantidade: number;
  valorTotal: number;
}

export interface EstoquePorCategoria {
  categoriaId: number;
  categoriaNome: string;
  quantidade: number;
  valorTotal: number;
}

export interface EstoquePorMarca {
  marca: string;
  quantidade: number;
  valorTotal: number;
}

export interface RelatorioFinanceiro {
  receitaBruta: number;
  custoTotal: number;
  lucroBruto: number;
  totalComissoes: number;
  lucroLiquido: number;
  margemLucro: number;
  ticketMedio: number;
  financeiroPorMes: FinanceiroPorMes[];
  financeiroPorLoja: FinanceiroPorLoja[];
}

export interface FinanceiroPorMes {
  periodo: string;
  receita: number;
  custo: number;
  lucro: number;
}

export interface FinanceiroPorLoja {
  lojaId: number;
  lojaNome: string;
  receita: number;
  custo: number;
  lucro: number;
}
