import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface TestDrive {
  tdrId: number;
  r_VeiId: number;
  r_LojId?: number;
  tdrNomeCliente: string;
  tdrTelefone: string;
  tdrWhatsApp: string;
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

  /** Horarios livres de uma data (publico): expediente, ocupados e ja passados saem no servidor. */
  horariosLivres(data: string, lojaId?: number | null): Observable<string[]> {
    let params = new HttpParams().set('data', data);
    if (lojaId) params = params.set('lojaId', lojaId.toString());
    return this.http.get<string[]>(`${this.baseUrl}/horarios`, { params });
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

  reagendar(id: number, dataAgendamento: string, horario: string): Observable<any> {
    return this.http.put(`${this.baseUrl}/${id}/reagendar`, { dataAgendamento, horario });
  }
}
