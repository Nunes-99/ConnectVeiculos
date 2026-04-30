import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Lead {
  leaId: number;
  r_VeiId?: number;
  r_LojId?: number;
  leaNomeCliente: string;
  leaTelefone: string;
  leaEmail: string;
  leaOrigem: string;
  leaStatus: string;
  leaObservacao: string;
  leaDtCriacao: string;
  leaCpf?: string;
  leaRenda?: number;
  leaEntrada?: number;
  leaParcelas?: number;
}

export interface LeadOrigem {
  origem: string;
  total: number;
  convertidos: number;
}

@Injectable({ providedIn: 'root' })
export class LeadService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/leads`;

  registrar(data: any): Observable<any> {
    return this.http.post(this.baseUrl, data);
  }

  listar(lojaId?: number, status?: string, origem?: string): Observable<Lead[]> {
    let params: any = {};
    if (lojaId) params.lojaId = lojaId;
    if (status) params.status = status;
    if (origem) params.origem = origem;
    return this.http.get<Lead[]>(this.baseUrl, { params });
  }

  atualizarStatus(id: number, status: string): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}/status`, { status });
  }

  relatorioOrigens(lojaId?: number): Observable<LeadOrigem[]> {
    let params: any = {};
    if (lojaId) params.lojaId = lojaId;
    return this.http.get<LeadOrigem[]>(`${this.baseUrl}/relatorio/origens`, { params });
  }
}
