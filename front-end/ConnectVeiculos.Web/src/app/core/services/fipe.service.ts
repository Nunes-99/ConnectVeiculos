import { Injectable } from '@angular/core';
import { HttpClient, HttpBackend } from '@angular/common/http';
import { Observable, map } from 'rxjs';

export interface FipeMarca {
  codigo: string;
  nome: string;
}

export interface FipeModelo {
  codigo: number;
  nome: string;
}

@Injectable({
  providedIn: 'root'
})
export class FipeService {
  private http: HttpClient;
  private baseUrl = 'https://fipe.parallelum.com.br/api/v2';

  constructor(handler: HttpBackend) {
    // HttpClient sem interceptors para não enviar token JWT para API externa
    this.http = new HttpClient(handler);
  }

  getMarcas(): Observable<FipeMarca[]> {
    return this.http.get<any[]>(`${this.baseUrl}/cars/brands`).pipe(
      map(data => data.map(m => ({ codigo: m.code, nome: m.name })))
    );
  }

  getModelos(marcaCodigo: string): Observable<FipeModelo[]> {
    return this.http.get<any[]>(`${this.baseUrl}/cars/brands/${marcaCodigo}/models`).pipe(
      map(data => data.map(m => ({ codigo: m.code, nome: m.name })))
    );
  }
}
