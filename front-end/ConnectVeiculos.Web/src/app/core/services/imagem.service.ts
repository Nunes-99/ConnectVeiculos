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

  /**
   * Sobe banner ou favicon da loja. Devolve o caminho relativo gravado; o
   * arquivo em si nao vai no registro da loja (ao contrario do logo, que e'
   * base64), pra nao inchar a resposta do catalogo publico.
   */
  uploadLoja(lojaId: number, arquivo: File, tipo: 'banner' | 'favicon'): Observable<{ caminho: string }> {
    const formData = new FormData();
    formData.append('arquivo', arquivo);
    return this.http.post<{ caminho: string }>(`${this.baseUrl}/imagens/loja/${lojaId}?tipo=${tipo}`, formData);
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
