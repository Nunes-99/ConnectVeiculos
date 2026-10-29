import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface FavoritoRelatorio {
  veiculoId: number;
  marca: string;
  modelo: string;
  ano: number;
  preco: number;
  status: string;
  totalFavoritos: number;
  ultimoFavorito: string;
}

export interface FavoritoVisitante {
  email: string;
  nome: string;
  telefone: string;
  totalFavoritos: number;
  ultimaAtividade: string;
}

@Injectable({ providedIn: 'root' })
export class FavoritoService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/favoritos`;

  // A API devolve uma chave ao favoritar; listar e remover exigem ela, para
  // que ninguem veja ou apague os favoritos de outra pessoa so pelo e-mail.
  favoritar(veiculoId: number, email: string, nome: string, telefone: string): Observable<{ chave?: string }> {
    return this.http.post<{ chave?: string }>(this.baseUrl, { veiculoId, email, nome, telefone });
  }

  desfavoritar(email: string, veiculoId: number, chave: string): Observable<void> {
    return this.http.delete<void>(this.baseUrl, { params: { email, veiculoId: veiculoId.toString(), chave } });
  }

  meusFavoritos(email: string, chave: string): Observable<number[]> {
    return this.http.get<number[]>(`${this.baseUrl}/meus`, { params: { email, chave } });
  }

  relatorio(): Observable<FavoritoRelatorio[]> {
    return this.http.get<FavoritoRelatorio[]>(`${this.baseUrl}/relatorio`);
  }

  visitantes(veiculoId?: number): Observable<FavoritoVisitante[]> {
    let params: any = {};
    if (veiculoId) params.veiculoId = veiculoId;
    return this.http.get<FavoritoVisitante[]>(`${this.baseUrl}/visitantes`, { params });
  }
}
