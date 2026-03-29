import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { LogAuditoria, PagedResult } from '../models';

@Injectable({
  providedIn: 'root'
})
export class LogService extends ApiService {
  private endpoint = 'logs';

  constructor(http: HttpClient) {
    super(http);
  }

  getPaged(
    page: number,
    pageSize: number,
    tabela?: string,
    acao?: string,
    dataInicio?: string,
    dataFim?: string
  ): Observable<PagedResult<LogAuditoria>> {
    let url = `${this.endpoint}/paged?page=${page}&pageSize=${pageSize}`;
    if (tabela) url += `&tabela=${encodeURIComponent(tabela)}`;
    if (acao) url += `&acao=${encodeURIComponent(acao)}`;
    if (dataInicio) url += `&dataInicio=${encodeURIComponent(dataInicio)}`;
    if (dataFim) url += `&dataFim=${encodeURIComponent(dataFim)}`;
    return this.get<PagedResult<LogAuditoria>>(url);
  }

  getById(id: number): Observable<LogAuditoria> {
    return this.get<LogAuditoria>(`${this.endpoint}/${id}`);
  }

  getTabelas(): Observable<string[]> {
    return this.get<string[]>(`${this.endpoint}/tabelas`);
  }

  getAcoes(): Observable<string[]> {
    return this.get<string[]>(`${this.endpoint}/acoes`);
  }
}
