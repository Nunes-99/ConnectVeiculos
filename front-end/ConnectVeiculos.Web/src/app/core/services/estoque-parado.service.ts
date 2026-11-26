import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface EstoqueParadoConfig {
  diasAtencao: number;
  diasCritico: number;
  /** Margem minima sobre o custo, em %. */
  margemMinima: number;
}

export interface EstoqueParadoItem {
  veiId: number;
  descricao: string;
  placa: string | null;
  loja: string | null;
  dias: number;
  entrada: string;
  preco: number;
  fipe: number | null;
  /** Compra + despesas; 0 sem preco de compra. */
  custo: number;
  despesas: number;
  faixa: 'normal' | 'atencao' | 'critico';
  precoSugerido: number;
  mudar: boolean;
  precoMinimo: number;
  margemAtual: number | null;
  diferencaFipe: number | null;
  motivo: string;
}

export interface EstoqueParado {
  config: EstoqueParadoConfig;
  disponiveis: number;
  atencao: number;
  critico: number;
  custoParado: number;
  mediaDias: number;
  veiculos: EstoqueParadoItem[];
}

/** Carros parados no estoque e sugestao de preco (Administrador, Gerente e Visualizador). */
@Injectable({ providedIn: 'root' })
export class EstoqueParadoService {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/estoque-parado`;

  consultar(lojaId?: number | null): Observable<EstoqueParado> {
    const params: Record<string, string> = lojaId ? { lojaId: String(lojaId) } : {};
    return this.http.get<EstoqueParado>(this.url, { params });
  }

  sugestao(veiculoId: number): Observable<EstoqueParadoItem> {
    return this.http.get<EstoqueParadoItem>(`${this.url}/veiculos/${veiculoId}`);
  }

  salvarConfig(config: EstoqueParadoConfig): Observable<EstoqueParadoConfig> {
    return this.http.put<EstoqueParadoConfig>(`${this.url}/config`, config);
  }
}
