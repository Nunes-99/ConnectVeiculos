import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface NotificacaoDB {
  notId: number;
  r_UsuId: number;
  notTitulo: string;
  notMensagem: string;
  notTipo: string;
  notLink: string | null;
  notLida: boolean;
  notCriadaEm: string;
  notLidaEm: string | null;
}

@Injectable({
  providedIn: 'root'
})
export class NotificacaoService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/notificacoes`;

  listar(apenasNaoLidas = false): Observable<NotificacaoDB[]> {
    return this.http.get<NotificacaoDB[]>(`${this.baseUrl}?apenasNaoLidas=${apenasNaoLidas}`);
  }

  contarNaoLidas(): Observable<{ count: number }> {
    return this.http.get<{ count: number }>(`${this.baseUrl}/nao-lidas/count`);
  }

  marcarComoLida(id: number): Observable<any> {
    return this.http.post(`${this.baseUrl}/${id}/marcar-lida`, {});
  }

  marcarTodasComoLidas(): Observable<any> {
    return this.http.post(`${this.baseUrl}/marcar-todas-lidas`, {});
  }
}
