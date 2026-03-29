import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { Acesso, AcessoInput, PagedResult } from '../models';

@Injectable({
  providedIn: 'root'
})
export class AcessoService extends ApiService {
  private endpoint = 'acessos';

  constructor(http: HttpClient) {
    super(http);
  }

  getAll(): Observable<Acesso[]> {
    return this.get<Acesso[]>(this.endpoint);
  }

  getAllPaged(page: number, pageSize: number, search?: string): Observable<PagedResult<Acesso>> {
    let url = `${this.endpoint}/paged?page=${page}&pageSize=${pageSize}`;
    if (search) url += `&search=${encodeURIComponent(search)}`;
    return this.get<PagedResult<Acesso>>(url);
  }

  getById(id: number): Observable<Acesso> {
    return this.get<Acesso>(`${this.endpoint}/${id}`);
  }

  create(acesso: AcessoInput): Observable<Acesso> {
    return this.post<Acesso>(this.endpoint, acesso);
  }

  update(id: number, acesso: AcessoInput): Observable<Acesso> {
    return this.put<Acesso>(`${this.endpoint}/${id}`, acesso);
  }

  remove(id: number): Observable<void> {
    return this.delete<void>(`${this.endpoint}/${id}`);
  }
}
