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

export interface FipeAno {
  codigo: string;  // ex: "2024-1" (1=Gasolina, 2=Alcool, 3=Diesel...)
  nome: string;    // ex: "2024 Gasolina"
}

export interface FipePreco {
  valor: string;       // "R$ 50.000,00"
  marca: string;
  modelo: string;
  anoModelo: number;
  combustivel: string;
  codigoFipe: string;
  mesReferencia: string;
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

  getAnos(marcaCodigo: string, modeloCodigo: number): Observable<FipeAno[]> {
    return this.http.get<any[]>(`${this.baseUrl}/cars/brands/${marcaCodigo}/models/${modeloCodigo}/years`).pipe(
      map(data => data.map(a => ({ codigo: a.code, nome: a.name })))
    );
  }

  /** Consulta o preço FIPE pra uma combinação marca/modelo/ano-código. */
  getPreco(marcaCodigo: string, modeloCodigo: number, anoCodigo: string): Observable<FipePreco> {
    return this.http.get<any>(`${this.baseUrl}/cars/brands/${marcaCodigo}/models/${modeloCodigo}/years/${anoCodigo}`).pipe(
      map(d => ({
        valor: d.price,
        marca: d.brand,
        modelo: d.model,
        anoModelo: d.modelYear,
        combustivel: d.fuel,
        codigoFipe: d.codeFipe,
        mesReferencia: d.referenceMonth
      }))
    );
  }

  /** Helper: converte string "R$ 52.500,00" para number 52500 */
  parseValorFipe(valor: string): number | null {
    if (!valor) return null;
    const clean = valor.replace(/[^\d,]/g, '').replace(',', '.');
    const num = parseFloat(clean);
    return isNaN(num) ? null : num;
  }
}
