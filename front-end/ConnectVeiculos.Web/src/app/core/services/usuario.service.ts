import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { Usuario, UsuarioInput, PagedResult } from '../models';

@Injectable({
  providedIn: 'root'
})
export class UsuarioService extends ApiService {
  private endpoint = 'usuarios';

  constructor(http: HttpClient) {
    super(http);
  }

  getAll(): Observable<Usuario[]> {
    return this.get<Usuario[]>(this.endpoint);
  }

  getAllPaged(page: number = 1, pageSize: number = 10, search?: string): Observable<PagedResult<Usuario>> {
    let url = `${this.endpoint}/paged?page=${page}&pageSize=${pageSize}`;
    if (search) {
      url += `&search=${encodeURIComponent(search)}`;
    }
    return this.get<PagedResult<Usuario>>(url);
  }

  getById(id: number): Observable<Usuario> {
    return this.get<Usuario>(`${this.endpoint}/${id}`);
  }

  getByLoja(lojaId: number): Observable<Usuario[]> {
    return this.get<Usuario[]>(`${this.endpoint}/loja/${lojaId}`);
  }

  create(usuario: UsuarioInput): Observable<Usuario> {
    return this.post<Usuario>(this.endpoint, usuario);
  }

  update(id: number, usuario: UsuarioInput): Observable<Usuario> {
    return this.put<Usuario>(`${this.endpoint}/${id}`, usuario);
  }

  remove(id: number): Observable<void> {
    return this.delete<void>(`${this.endpoint}/${id}`);
  }

  login(email: string, senha: string): Observable<Usuario> {
    return this.post<Usuario>(`${this.endpoint}/login`, { email, senha });
  }
}
