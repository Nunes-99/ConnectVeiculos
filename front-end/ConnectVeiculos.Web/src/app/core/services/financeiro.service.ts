import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface LancamentoInput {
  /** "P" = a pagar, "R" = a receber. */
  tipo: 'P' | 'R';
  descricao: string;
  categoria: string | null;
  valor: number;
  vencimento: string;
  lojaId: number | null;
  observacao: string | null;
  recorrente: boolean;
  pagoEm?: string | null;
}

export interface Lancamento extends LancamentoInput {
  id: number;
  categoria: string;
  pagoEm: string | null;
  situacao: 'pago' | 'atrasado' | 'vence_hoje' | 'aberto';
}

export interface MovimentoCaixa {
  data: string;
  sentido: 'entrada' | 'saida';
  descricao: string;
  categoria: string;
  valor: number;
  origem: 'lancamento' | 'venda' | 'compra' | 'despesa';
  realizado: boolean;
  lancamentoId: number | null;
  veiculoId: number | null;
}

export interface FinanceiroMes {
  ano: number;
  mes: number;
  entradasRealizadas: number;
  saidasRealizadas: number;
  entradasPrevistas: number;
  saidasPrevistas: number;
  saldoRealizado: number;
  saldoPrevisto: number;
  atrasadas: number;
  valorAtrasado: number;
  vencemEmBreve: number;
  lancamentos: Lancamento[];
  movimentos: MovimentoCaixa[];
  saidasPorCategoria: Record<string, number>;
}

export const CATEGORIAS_PAGAR = [
  'Aluguel', 'Salários', 'Pró-labore', 'Comissões', 'Impostos', 'Água/Luz/Internet', 'Marketing e anúncios',
  'Manutenção da loja', 'Contador', 'Sistemas', 'Empréstimo/Financiamento', 'Documentação', 'Outros'
];
export const CATEGORIAS_RECEBER = [
  'Parcela de cliente', 'Retorno de financiamento', 'Consignação', 'Serviços', 'Outros'
];

@Injectable({ providedIn: 'root' })
export class FinanceiroService {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/financeiro`;

  mes(ano: number, mes: number, lojaId?: number | null): Observable<FinanceiroMes> {
    const params: Record<string, string> = { ano: String(ano), mes: String(mes) };
    if (lojaId) params['lojaId'] = String(lojaId);
    return this.http.get<FinanceiroMes>(this.url, { params });
  }

  criar(input: LancamentoInput): Observable<Lancamento> {
    return this.http.post<Lancamento>(`${this.url}/lancamentos`, input);
  }

  alterar(id: number, input: LancamentoInput): Observable<Lancamento> {
    return this.http.put<Lancamento>(`${this.url}/lancamentos/${id}`, input);
  }

  excluir(id: number): Observable<{ ok: boolean }> {
    return this.http.delete<{ ok: boolean }>(`${this.url}/lancamentos/${id}`);
  }

  pagar(id: number, pagoEm: string | null): Observable<Lancamento> {
    return this.http.post<Lancamento>(`${this.url}/lancamentos/${id}/pagar`, { pagoEm });
  }
}
