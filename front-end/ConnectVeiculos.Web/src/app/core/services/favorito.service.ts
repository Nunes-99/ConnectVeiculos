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

  favoritar(veiculoId: number, email: string, nome: string, telefone: string): Observable<any> {
    return this.http.post(this.baseUrl, { veiculoId, email, nome, telefone });
  }

  desfavoritar(email: string, veiculoId: number): Observable<void> {
    return this.http.delete<void>(this.baseUrl, { params: { email, veiculoId: veiculoId.toString() } });
  }

  meusFavoritos(email: string): Observable<number[]> {
    return this.http.get<number[]>(`${this.baseUrl}/meus`, { params: { email } });
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
