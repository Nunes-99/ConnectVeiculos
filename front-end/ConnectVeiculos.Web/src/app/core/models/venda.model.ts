export interface Venda {
  venId: number;
  r_VeiId: number;
  r_UsuId: number;
  vendedorNome: string;
  venDtVenda: Date;
  venMarca: string;
  venModelo: string;
  venAno: number;
  venChassi: string;
  venValor: number;
  venComissaoPorc: number;
  venComissaoValor: number;
  // Dados do Comprador
  venCompradorNome: string;
  venCompradorCpf: string;
  venCompradorTelefone: string;
  venCompradorEmail: string;
  venCompradorEndereco: string;
  // Forma de Pagamento e Status
  venFormaPagamento: string;
  venObservacao: string;
  venStatus: string;
  venDtEstorno: Date | null;
}

export interface VendaInput {
  r_VeiId: number;
  r_UsuId: number;
  venDtVenda: Date;
  venValor: number;
  venComissaoPorc: number;
  // Dados do Comprador
  venCompradorNome: string;
  venCompradorCpf: string;
  venCompradorTelefone: string;
  venCompradorEmail: string;
  venCompradorEndereco: string;
  // Forma de Pagamento
  venFormaPagamento: string;
  venObservacao: string;
}
