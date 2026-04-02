import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Negociacao {
  negId: number;
  r_VeiId: number;
  r_LojId?: number;
  negNomeCliente: string;
  negTelefone: string;
  negEmail: string;
  negValorProposta: number;
  negStatus: string;
  negObservacao: string;
  negDtCriacao: string;
}

export interface NegociacaoInput {
  veiculoId: number;
  lojaId?: number;
  nomeCliente: string;
  telefone: string;
  email: string;
  valorProposta: number;
  status: string;
  observacao: string;
}

@Injectable({ providedIn: 'root' })
export class NegociacaoService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/negociacoes`;

  listar(veiculoId?: number, lojaId?: number, status?: string): Observable<Negociacao[]> {
    let params: any = {};
    if (veiculoId) params.veiculoId = veiculoId;
    if (lojaId) params.lojaId = lojaId;
    if (status) params.status = status;
    return this.http.get<Negociacao[]>(this.baseUrl, { params });
  }

  listarPorVeiculo(veiculoId: number): Observable<Negociacao[]> {
    return this.http.get<Negociacao[]>(`${this.baseUrl}/veiculo/${veiculoId}`);
  }

  registrar(data: NegociacaoInput): Observable<any> {
    return this.http.post(this.baseUrl, data);
  }

  atualizar(id: number, data: NegociacaoInput): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, data);
  }

  atualizarStatus(id: number, status: string): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}/status`, { status });
  }

  excluir(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
