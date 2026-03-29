export interface Dashboard {
  totalVeiculos: number;
  veiculosDisponiveis: number;
  veiculosVendidos: number;
  veiculosReservados: number;
  valorTotalEstoque: number;
  valorMedioVeiculo: number;
  totalLojas: number;
  totalCategorias: number;
  totalUsuarios: number;
  veiculosPorCategoria: VeiculoPorCategoria[];
  veiculosPorLoja: VeiculoPorLoja[];
  veiculosRecentes: VeiculoRecente[];
}

export interface VeiculoPorCategoria {
  categoria: string;
  quantidade: number;
}

export interface VeiculoPorLoja {
  loja: string;
  quantidade: number;
  valorTotal: number;
}

export interface VeiculoRecente {
  veiId: number;
  marca: string;
  modelo: string;
  ano: number;
  preco: number;
  status: string;
}

// Dashboard Avancado
export interface VendasPorPeriodo {
  vendas: VendaDia[];
  totalPeriodo: number;
  quantidadeVendas: number;
}

export interface VendaDia {
  data: string;
  quantidade: number;
  valor: number;
}

export interface FaturamentoMensal {
  meses: FaturamentoMes[];
  totalAnual: number;
  mediaMensal: number;
}

export interface FaturamentoMes {
  mes: string;
  ano: number;
  faturamento: number;
  lucro: number;
  quantidadeVendas: number;
}

export interface TopVeiculosVendidos {
  veiculos: VeiculoVendido[];
}

export interface VeiculoVendido {
  marca: string;
  modelo: string;
  quantidadeVendida: number;
  valorTotalVendas: number;
  ticketMedio: number;
}

export interface ComparativoMensal {
  mesAtual: ComparativoMes;
  mesAnterior: ComparativoMes;
  variacaoFaturamento: number;
  variacaoQuantidade: number;
  variacaoTicketMedio: number;
}

export interface ComparativoMes {
  periodo: string;
  faturamento: number;
  quantidadeVendas: number;
  ticketMedio: number;
  lucro: number;
}
