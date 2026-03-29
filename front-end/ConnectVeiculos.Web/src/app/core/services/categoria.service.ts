import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { Categoria, CategoriaInput, PagedResult } from '../models';

@Injectable({
  providedIn: 'root'
})
export class CategoriaService extends ApiService {
  private endpoint = 'categorias';

  constructor(http: HttpClient) {
    super(http);
  }

  getAll(): Observable<Categoria[]> {
    return this.get<Categoria[]>(this.endpoint);
  }

  getAllPaged(page: number, pageSize: number, search?: string): Observable<PagedResult<Categoria>> {
    let url = `${this.endpoint}/paged?page=${page}&pageSize=${pageSize}`;
    if (search) url += `&search=${encodeURIComponent(search)}`;
    return this.get<PagedResult<Categoria>>(url);
  }

  getById(id: number): Observable<Categoria> {
    return this.get<Categoria>(`${this.endpoint}/${id}`);
  }

  create(categoria: CategoriaInput): Observable<Categoria> {
    return this.post<Categoria>(this.endpoint, categoria);
  }

  update(id: number, categoria: CategoriaInput): Observable<Categoria> {
    return this.put<Categoria>(`${this.endpoint}/${id}`, categoria);
  }

  remove(id: number): Observable<void> {
    return this.delete<void>(`${this.endpoint}/${id}`);
  }
}
