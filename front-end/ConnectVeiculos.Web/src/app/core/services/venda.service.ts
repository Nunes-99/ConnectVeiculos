import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { Venda, VendaInput } from '../models';

@Injectable({
  providedIn: 'root'
})
export class VendaService extends ApiService {
  getAll(): Observable<Venda[]> {
    return this.get<Venda[]>('vendas');
  }

  getById(id: number): Observable<Venda> {
    return this.get<Venda>(`vendas/${id}`);
  }

  create(venda: VendaInput): Observable<{ id: number }> {
    return this.post<{ id: number }>('vendas', venda);
  }

  update(id: number, venda: VendaInput): Observable<void> {
    return this.put<void>(`vendas/${id}`, venda);
  }

  estornar(id: number): Observable<{ message: string }> {
    return this.post<{ message: string }>(`vendas/${id}/estornar`, {});
  }
}
