import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { RelatorioVendas, RelatorioEstoque, RelatorioFinanceiro } from '../models/relatorio.model';

@Injectable({
  providedIn: 'root'
})
export class RelatorioService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/relatorios`;

  getRelatorioVendas(dataInicio?: string, dataFim?: string, lojaId?: number): Observable<RelatorioVendas> {
    let params = new HttpParams();
    if (dataInicio) params = params.set('dataInicio', dataInicio);
    if (dataFim) params = params.set('dataFim', dataFim);
    if (lojaId) params = params.set('lojaId', lojaId.toString());
    return this.http.get<RelatorioVendas>(`${this.apiUrl}/vendas`, { params });
  }

  getRelatorioEstoque(lojaId?: number, categoriaId?: number): Observable<RelatorioEstoque> {
    let params = new HttpParams();
    if (lojaId) params = params.set('lojaId', lojaId.toString());
    if (categoriaId) params = params.set('categoriaId', categoriaId.toString());
    return this.http.get<RelatorioEstoque>(`${this.apiUrl}/estoque`, { params });
  }

  getRelatorioFinanceiro(dataInicio?: string, dataFim?: string, lojaId?: number): Observable<RelatorioFinanceiro> {
    let params = new HttpParams();
    if (dataInicio) params = params.set('dataInicio', dataInicio);
    if (dataFim) params = params.set('dataFim', dataFim);
    if (lojaId) params = params.set('lojaId', lojaId.toString());
    return this.http.get<RelatorioFinanceiro>(`${this.apiUrl}/financeiro`, { params });
  }
}
