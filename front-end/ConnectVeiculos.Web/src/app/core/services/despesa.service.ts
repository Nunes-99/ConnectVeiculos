import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface VeiculoDespesa {
  desId: number;
  r_VeiId: number;
  desTipo: string;
  desDescricao: string;
  desValor: number;
  desDtDespesa: string;
  desDtCriacao: string;
}

@Injectable({ providedIn: 'root' })
export class DespesaService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/despesas`;

  listarPorVeiculo(veiculoId: number): Observable<VeiculoDespesa[]> {
    return this.http.get<VeiculoDespesa[]>(`${this.baseUrl}/veiculo/${veiculoId}`);
  }

  totalPorVeiculo(veiculoId: number): Observable<{ total: number }> {
    return this.http.get<{ total: number }>(`${this.baseUrl}/veiculo/${veiculoId}/total`);
  }

  criar(data: any): Observable<any> {
    return this.http.post(this.baseUrl, data);
  }

  excluir(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
