import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface TestDrive {
  tdrId: number;
  r_VeiId: number;
  r_LojId?: number;
  tdrNomeCliente: string;
  tdrTelefone: string;
  tdrEmail: string;
  tdrDataAgendamento: string;
  tdrHorario: string;
  tdrObservacao: string;
  tdrStatus: string;
  tdrDtCriacao: string;
  veiculoNome?: string;
}

@Injectable({ providedIn: 'root' })
export class TestDriveService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/testdrives`;

  agendar(data: any): Observable<any> {
    return this.http.post(this.baseUrl, data);
  }

  listar(lojaId?: number, status?: string): Observable<TestDrive[]> {
    let params: any = {};
    if (lojaId) params.lojaId = lojaId;
    if (status) params.status = status;
    return this.http.get<TestDrive[]>(this.baseUrl, { params });
  }

  atualizarStatus(id: number, status: string): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}/status`, { status });
  }
}
