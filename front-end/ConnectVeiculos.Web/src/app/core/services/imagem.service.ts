import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface VeiculoImagem {
  imgId: number;
  r_VeiId: number;
  imgCaminho: string;
  imgOrdem: number;
}

@Injectable({
  providedIn: 'root'
})
export class ImagemService {
  private http = inject(HttpClient);
  private baseUrl = environment.apiUrl;

  getByVeiculo(veiculoId: number): Observable<VeiculoImagem[]> {
    return this.http.get<VeiculoImagem[]>(`${this.baseUrl}/imagens/veiculo/${veiculoId}`);
  }

  upload(veiculoId: number, arquivo: File): Observable<VeiculoImagem> {
    const formData = new FormData();
    formData.append('arquivo', arquivo);
    return this.http.post<VeiculoImagem>(`${this.baseUrl}/imagens/veiculo/${veiculoId}`, formData);
  }

  delete(imagemId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/imagens/${imagemId}`);
  }

  definirPrincipal(imagemId: number): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/imagens/${imagemId}/principal`, {});
  }

  getImageUrl(caminho: string): string {
    return `${this.baseUrl}/imagens/file?path=${encodeURIComponent(caminho)}`;
  }
}
