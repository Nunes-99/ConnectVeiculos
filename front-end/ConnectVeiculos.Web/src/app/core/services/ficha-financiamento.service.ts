import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface TaxaPrazo { meses: number; taxaMensal: number; }

export interface FaixaTaxaBanco {
  anoDe: number | null;
  anoAte: number | null;
  entradaMinimaPct: number;
  taxas: TaxaPrazo[];
}

export interface BancoLoja {
  id: number;
  nome: string;
  portalUrl: string | null;
  ativo: boolean;
  tarifaCadastro: number;
  registroContrato: number;
  tarifasFinanciadas: boolean;
  faixas: FaixaTaxaBanco[];
  observacao: string | null;
}

export interface EstimativaPrazo {
  meses: number;
  taxaMensal: number;
  valorParcela: number;
  valorFinanciado: number;
  iof: number;
  tarifas: number;
  totalPago: number;
  cetMensal: number;
  cetAnual: number;
}

export interface EstimativaBanco {
  bancoId: number;
  banco: string;
  portalUrl: string | null;
  motivo: string | null;
  prazos: EstimativaPrazo[];
}

export type StatusProposta = 'ENVIADA' | 'EM_ANALISE' | 'PENDENCIA' | 'APROVADA' | 'RECUSADA';
export type StatusFicha = 'ABERTA' | 'APROVADA' | 'VENDIDA' | 'PERDIDA';

export interface PropostaBanco {
  bancoId: number;
  banco: string;
  status: StatusProposta;
  meses: number | null;
  taxaMensal: number | null;
  valorParcela: number | null;
  entradaExigida: number | null;
  observacao: string | null;
  atualizadoEm?: string;
}

export interface FichaInput {
  nome: string;
  cpf: string;
  telefone: string;
  email: string;
  veiculoId: number | null;
  valorVeiculo: number;
  entrada: number;
  anoVeiculo: number;
  observacao: string | null;
  consentimento: boolean;
}

export interface Ficha {
  id: number;
  veiculoId: number | null;
  veiculo: string | null;
  vendedorId: number;
  nome: string;
  cpf: string;
  telefone: string;
  email: string;
  valorVeiculo: number;
  entrada: number;
  anoVeiculo: number;
  status: StatusFicha;
  observacao: string | null;
  consentimentoEm: string;
  criadoEm: string;
  atualizadoEm: string;
  propostas: PropostaBanco[];
  credere: CredereFichaDados | null;
}

/** Dados extras da ficha para a Credere e a ultima simulacao feita la. */
export interface CredereFichaDados {
  nascimento: string | null;
  possuiCnh: boolean | null;
  genero: string | null;
  rendaMensal: number | null;
  cep: string | null;
  ocupacao: string | null;
  modeloId: number | null;
  modeloNome: string | null;
  anoFabricacao: number | null;
  ufLicenciamento: string | null;
  cidadeLicenciamento: string | null;
  prazos: number[];
  ultimaSimulacao: string | null;
  ultimaSimulacaoEm: string | null;
}

export const ROTULO_PROPOSTA: Record<StatusProposta, string> = {
  ENVIADA: 'Enviada', EM_ANALISE: 'Em análise', PENDENCIA: 'Pendência', APROVADA: 'Aprovada', RECUSADA: 'Recusada'
};

export const ROTULO_FICHA: Record<StatusFicha, string> = {
  ABERTA: 'Aberta', APROVADA: 'Aprovada', VENDIDA: 'Vendida', PERDIDA: 'Perdida'
};

/** Ficha unica de financiamento: bancos da loja, estimativa e propostas. */
@Injectable({ providedIn: 'root' })
export class FichaFinanciamentoService {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/financiamento`;

  bancos(): Observable<BancoLoja[]> {
    return this.http.get<BancoLoja[]>(`${this.url}/bancos-loja`);
  }

  salvarBanco(banco: BancoLoja): Observable<BancoLoja> {
    return this.http.post<BancoLoja>(`${this.url}/bancos-loja`, banco);
  }

  removerBanco(id: number): Observable<unknown> {
    return this.http.delete(`${this.url}/bancos-loja/${id}`);
  }

  estimar(valorVeiculo: number, entrada: number, anoVeiculo: number): Observable<EstimativaBanco[]> {
    return this.http.post<EstimativaBanco[]>(`${this.url}/estimativa`, { valorVeiculo, entrada, anoVeiculo });
  }

  fichas(): Observable<Ficha[]> {
    return this.http.get<Ficha[]>(`${this.url}/fichas`);
  }

  criar(dados: FichaInput): Observable<Ficha> {
    return this.http.post<Ficha>(`${this.url}/fichas`, dados);
  }

  atualizar(id: number, dados: FichaInput): Observable<Ficha> {
    return this.http.put<Ficha>(`${this.url}/fichas/${id}`, dados);
  }

  alterarStatus(id: number, status: StatusFicha): Observable<Ficha> {
    return this.http.post<Ficha>(`${this.url}/fichas/${id}/status`, { status });
  }

  registrarProposta(id: number, proposta: PropostaBanco): Observable<Ficha> {
    return this.http.post<Ficha>(`${this.url}/fichas/${id}/propostas`, proposta);
  }

  removerProposta(id: number, bancoId: number): Observable<Ficha> {
    return this.http.delete<Ficha>(`${this.url}/fichas/${id}/propostas/${bancoId}`);
  }
}

/** Mensagem de erro da API (422 traz { error }). */
export function erroFinanciamento(err: any, padrao: string): string {
  return err?.error?.error || padrao;
}
