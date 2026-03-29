import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { Loja, LojaInput, PagedResult } from '../models';

@Injectable({
  providedIn: 'root'
})
export class LojaService extends ApiService {
  private endpoint = 'lojas';

  constructor(http: HttpClient) {
    super(http);
  }

  getAll(): Observable<Loja[]> {
    return this.get<Loja[]>(this.endpoint);
  }

  getAllPaged(page: number, pageSize: number, search?: string): Observable<PagedResult<Loja>> {
    let url = `${this.endpoint}/paged?page=${page}&pageSize=${pageSize}`;
    if (search) url += `&search=${encodeURIComponent(search)}`;
    return this.get<PagedResult<Loja>>(url);
  }

  getById(id: number): Observable<Loja> {
    return this.get<Loja>(`${this.endpoint}/${id}`);
  }

  create(loja: LojaInput): Observable<Loja> {
    return this.post<Loja>(this.endpoint, loja);
  }

  update(id: number, loja: LojaInput): Observable<Loja> {
    return this.put<Loja>(`${this.endpoint}/${id}`, loja);
  }

  remove(id: number): Observable<void> {
    return this.delete<void>(`${this.endpoint}/${id}`);
  }
}
