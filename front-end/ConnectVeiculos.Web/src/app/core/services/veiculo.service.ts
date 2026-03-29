import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { Veiculo, VeiculoInput } from '../models';

@Injectable({
  providedIn: 'root'
})
export class VeiculoService extends ApiService {
  private endpoint = 'veiculos';

  constructor(http: HttpClient) {
    super(http);
  }

  getAll(): Observable<Veiculo[]> {
    return this.get<Veiculo[]>(this.endpoint);
  }

  getById(id: number): Observable<Veiculo> {
    return this.get<Veiculo>(`${this.endpoint}/${id}`);
  }

  getByLoja(lojaId: number): Observable<Veiculo[]> {
    return this.get<Veiculo[]>(`${this.endpoint}/loja/${lojaId}`);
  }

  getByCategoria(categoriaId: number): Observable<Veiculo[]> {
    return this.get<Veiculo[]>(`${this.endpoint}/categoria/${categoriaId}`);
  }

  search(termo: string): Observable<Veiculo[]> {
    const params = new HttpParams().set('termo', termo);
    return this.get<Veiculo[]>(`${this.endpoint}/search`, params);
  }

  create(veiculo: VeiculoInput): Observable<Veiculo> {
    return this.post<Veiculo>(this.endpoint, veiculo);
  }

  update(id: number, veiculo: VeiculoInput): Observable<Veiculo> {
    return this.put<Veiculo>(`${this.endpoint}/${id}`, veiculo);
  }

  remove(id: number): Observable<void> {
    return this.delete<void>(`${this.endpoint}/${id}`);
  }

  uploadImagem(veiculoId: number, formData: FormData): Observable<any> {
    return this.http.post(`${this.baseUrl}/${this.endpoint}/${veiculoId}/imagens`, formData);
  }

  removeImagem(veiculoId: number, imagemId: number): Observable<void> {
    return this.delete<void>(`${this.endpoint}/${veiculoId}/imagens/${imagemId}`);
  }

  atualizarStatusSocial(id: number, rede: string, postado: boolean): Observable<void> {
    return this.put<void>(`${this.endpoint}/${id}/social-status`, { rede, postado });
  }
}
