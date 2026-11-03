import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface WebmotorsItem { codigo: number; nome: string; }
export interface WebmotorsVersao extends WebmotorsItem { anos: number[]; }
export interface WebmotorsModalidade extends WebmotorsItem { tipoAnuncio: string; limite: number; emUso: number; }

export interface WebmotorsStatus {
  conectado: boolean;
  homologacao: boolean;
  cnpj: string | null;
  email: string | null;
  conectadoEm: string | null;
  modalidadeId: number | null;
  modalidades: WebmotorsModalidade[];
  erro: string | null;
}

export interface WebmotorsAnuncioDados {
  marcaId: number | null;
  modeloId: number | null;
  versaoId: number | null;
  anoModelo: number | null;
  anoFabricacao: number | null;
  cambioId: number | null;
  combustivelId: number | null;
  corId: number | null;
  portas: number | null;
  opcionais: number[];
  unicoDono: boolean;
  ipvaPago: boolean;
  licenciado: boolean;
  blindado: boolean;
  alienado: boolean;
  garantiaDeFabrica: boolean;
  revisoesEmConcessionaria: boolean;
  adaptadoPcd: boolean;
  naoAceitaTroca: boolean;
}

export interface WebmotorsVeiculoInfo {
  publicado: boolean;
  codigoAnuncio: number | null;
  status: string;
  erro: string | null;
  publicadoEm: string | null;
  atualizadoEm: string | null;
  fotos: number;
  dados: WebmotorsAnuncioDados;
  sugestao: boolean;
}

export type WebmotorsTabela = 'marcas' | 'cores' | 'cambios' | 'combustiveis' | 'portas' | 'opcionais';

@Injectable({ providedIn: 'root' })
export class WebmotorsService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/integracoes/webmotors`;

  status(): Observable<WebmotorsStatus> {
    return this.http.get<WebmotorsStatus>(this.base);
  }

  conectar(cnpj: string, email: string, senha: string): Observable<WebmotorsStatus> {
    return this.http.post<WebmotorsStatus>(`${this.base}/conectar`, { cnpj, email, senha });
  }

  escolherModalidade(modalidadeId: number): Observable<WebmotorsStatus> {
    return this.http.put<WebmotorsStatus>(`${this.base}/modalidade`, { modalidadeId });
  }

  desconectar(): Observable<unknown> {
    return this.http.delete(this.base);
  }

  tabela(tabela: WebmotorsTabela): Observable<WebmotorsItem[]> {
    return this.http.get<WebmotorsItem[]>(`${this.base}/tabelas/${tabela}`);
  }

  modelos(marcaId: number): Observable<WebmotorsItem[]> {
    return this.http.get<WebmotorsItem[]>(`${this.base}/marcas/${marcaId}/modelos`);
  }

  versoes(modeloId: number, ano: number | null): Observable<WebmotorsVersao[]> {
    const q = ano ? `?ano=${ano}` : '';
    return this.http.get<WebmotorsVersao[]>(`${this.base}/modelos/${modeloId}/versoes${q}`);
  }

  veiculo(veiculoId: number): Observable<WebmotorsVeiculoInfo> {
    return this.http.get<WebmotorsVeiculoInfo>(`${this.base}/veiculos/${veiculoId}`);
  }

  publicar(veiculoId: number, dados: WebmotorsAnuncioDados): Observable<WebmotorsVeiculoInfo> {
    return this.http.post<WebmotorsVeiculoInfo>(`${this.base}/veiculos/${veiculoId}`, dados);
  }

  remover(veiculoId: number): Observable<WebmotorsVeiculoInfo> {
    return this.http.delete<WebmotorsVeiculoInfo>(`${this.base}/veiculos/${veiculoId}`);
  }
}

export function erroWebmotors(err: any, padrao: string): string {
  return err?.error?.error || padrao;
}
