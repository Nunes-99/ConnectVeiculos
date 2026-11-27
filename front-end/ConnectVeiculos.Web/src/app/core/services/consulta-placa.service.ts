import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PixGerado } from './plano.service';

export type CategoriaConsulta = 'CONSULTAS' | 'DOCUMENTOS' | 'ANALISE_PESSOA';

export interface PrecoUf {
  uf: string;
  preco: number;
  requer: string[];
}

export interface ServicoConsulta {
  id: string;
  nome: string;
  descricao: string;
  categoria: CategoriaConsulta;
  prazo: string | null;
  nota: string | null;
  /** Campos exigidos: placa, uf, renavam, cpf, chassi, motor, documento, cnh, chave, data_nascimento. */
  requer: string[];
  /** Null no CRLV (preco por estado em precosUf). */
  preco: number | null;
  precosUf: PrecoUf[];
  exigeFinalidade: boolean;
  recomendado: boolean;
}

export interface PedidoConsulta {
  servico: string;
  placa?: string;
  uf?: string;
  renavam?: string;
  cpf?: string;
  chassi?: string;
  motor?: string;
  documento?: string;
  cnh?: string;
  chave?: string;
  dataNascimento?: string;
  finalidade?: string;
  declaracaoFinalidade?: boolean;
  aceiteAtpveVersao?: string;
}

export interface AvisoAtpve {
  versao: string;
  texto: string;
  declaracao: string;
}

export interface AlertaConsulta {
  texto: string;
  grave: boolean;
}

/** Bloco "veiculo" da ZapCar (snake_case, como a base devolve). */
export interface VeiculoConsultado {
  placa?: string | null;
  renavam?: string | null;
  chassi?: string | null;
  marca?: string | null;
  modelo?: string | null;
  ano_fabricacao?: number | null;
  ano_modelo?: number | null;
  cor?: string | null;
  combustivel?: string | null;
  municipio?: string | null;
  uf?: string | null;
  situacao?: string | null;
  proprietario?: { nome?: string | null; documento?: string | null } | null;
  ultimo_licenciamento?: number | null;
  debitos?: { tipo: string; descricao?: string | null; valor_centavos: number; valor_informado?: boolean }[];
  debitos_total_centavos?: number;
  leilao?: { consta: boolean; ocorrencias: number; fotos?: number } | null;
  sinistro?: boolean | null;
  recall?: boolean | null;
}

export interface ConsultaItem {
  id: number;
  placa: string;
  identificador: string | null;
  servico: string;
  servicoNome: string;
  categoria: CategoriaConsulta;
  status: 'PROCESSANDO' | 'CONCLUIDA' | 'ERRO';
  valor: number;
  erro: string | null;
  estornada: boolean;
  teste: boolean;
  temDocumento: boolean;
  usuario: string | null;
  criadoEm: string;
  concluidoEm: string | null;
  veiculo: VeiculoConsultado | null;
  /** Bloco "dados" como a base devolveu (forma varia por servico). */
  dados: Record<string, unknown> | null;
  alertas: AlertaConsulta[];
  nadaConsta: boolean;
}

export interface MovimentoCredito {
  tipo: 'COMPRA' | 'CONSUMO' | 'ESTORNO' | 'AJUSTE';
  valor: number;
  descricao: string | null;
  data: string;
}

export interface ConsultaPlacaPainel {
  disponivel: boolean;
  teste: boolean;
  saldo: number;
  plano: string | null;
  servicos: ServicoConsulta[];
  pacotes: number[];
  consultas: ConsultaItem[];
  extrato: MovimentoCredito[];
}

export interface PrecoPlano {
  planoId: number;
  plano: string;
  acrescimo: number;
  preco: number | null;
  fixo: number | null;
}

export interface PrecoServico {
  servico: string;
  nome: string;
  categoria: CategoriaConsulta;
  custo: number | null;
  variaPorUf: boolean;
  fixoTodos: number | null;
  planos: PrecoPlano[];
}

@Injectable({ providedIn: 'root' })
export class ConsultaPlacaService {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/consulta-placa`;
  private urlAdmin = `${environment.apiUrl}/admin/consultas/precos`;

  painel(): Observable<ConsultaPlacaPainel> {
    return this.http.get<ConsultaPlacaPainel>(this.url);
  }

  consultar(pedido: PedidoConsulta): Observable<ConsultaItem> {
    return this.http.post<ConsultaItem>(this.url, pedido);
  }

  obter(id: number): Observable<ConsultaItem> {
    return this.http.get<ConsultaItem>(`${this.url}/${id}`);
  }

  documento(id: number): Observable<Blob> {
    return this.http.get(`${this.url}/${id}/documento`, { responseType: 'blob' });
  }

  avisoAtpve(): Observable<AvisoAtpve> {
    return this.http.get<AvisoAtpve>(`${this.url}/aviso-atpve`);
  }

  comprarCreditos(valor: number, email: string): Observable<PixGerado> {
    return this.http.post<PixGerado>(`${this.url}/creditos/pix`, { valor, email });
  }

  // Tela de precos da plataforma (token de administrador, fora do login das lojas)
  precos(token: string): Observable<{ teste: boolean; configurado: boolean; servicos: PrecoServico[] }> {
    return this.http.get<{ teste: boolean; configurado: boolean; servicos: PrecoServico[] }>(this.urlAdmin, { headers: this.admin(token) });
  }

  definirPreco(token: string, servico: string, planoId: number | null, preco: number | null): Observable<{ servicos: PrecoServico[] }> {
    return this.http.put<{ servicos: PrecoServico[] }>(this.urlAdmin, { servico, planoId, preco }, { headers: this.admin(token) });
  }

  private admin(token: string): HttpHeaders {
    return new HttpHeaders({ 'X-Admin-Token': token });
  }
}
