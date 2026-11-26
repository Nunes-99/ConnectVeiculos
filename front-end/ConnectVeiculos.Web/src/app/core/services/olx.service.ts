import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface OlxItem { id: string; nome: string; }

export interface OlxStatus {
  /** O ConnectVeiculos ja e' integrador cadastrado na OLX. */
  configurado: boolean;
  conectado: boolean;
  conectadoEm: string | null;
  conta: string | null;
  anunciosUsados: number | null;
  anunciosDisponiveis: number | null;
  anunciosTotal: number | null;
  proximaRenovacao: string | null;
  /** Endereco onde a conexao tem de comecar (o da redirect_uri). */
  siteConexao: string | null;
  erro: string | null;
}

export interface OlxAtributos {
  combustiveis: OlxItem[];
  cambios: OlxItem[];
  cores: OlxItem[];
  portas: OlxItem[];
  tipos: OlxItem[];
  direcoes: OlxItem[];
  potencias: OlxItem[];
  opcionais: OlxItem[];
}

export interface OlxAnuncioDados {
  marcaId: string | null;
  modeloId: string | null;
  versaoId: string | null;
  marcaNome: string | null;
  modeloNome: string | null;
  versaoNome: string | null;
  ano: number | null;
  combustivel: string | null;
  cambio: string | null;
  cor: string | null;
  portas: string | null;
  tipo: string | null;
  direcao: string | null;
  potencia: string | null;
  opcionais: string[];
  aceitaTroca: boolean | null;
  unicoDono: boolean | null;
}

export interface OlxVeiculoInfo {
  publicado: boolean;
  status: string;
  /** Resultado do ultimo envio na OLX: pending, queued, accepted, refused, error. */
  situacao: string | null;
  url: string | null;
  erro: string | null;
  publicadoEm: string | null;
  atualizadoEm: string | null;
  fotos: number;
  dados: OlxAnuncioDados;
  sugestao: boolean;
  pendencias: string[];
}

@Injectable({ providedIn: 'root' })
export class OlxService {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/integracoes/olx`;

  status(): Observable<OlxStatus> {
    return this.http.get<OlxStatus>(this.url);
  }

  urlAutorizacao(): Observable<{ url: string }> {
    return this.http.get<{ url: string }>(`${this.url}/autorizar`);
  }

  /** Volta do login da OLX. Sem sessao: a loja vem do state (ver OlxController.Retorno). */
  retorno(code: string, state: string | null, tenant?: string): Observable<{ conectado: boolean; tenant: string }> {
    const q = tenant ? `?tenant=${encodeURIComponent(tenant)}` : '';
    return this.http.post<{ conectado: boolean; tenant: string }>(`${this.url}/retorno${q}`, { code, state });
  }

  desconectar(): Observable<{ ok: boolean }> {
    return this.http.delete<{ ok: boolean }>(this.url);
  }

  marcas(): Observable<OlxItem[]> {
    return this.http.get<OlxItem[]>(`${this.url}/catalogo/marcas`);
  }

  modelos(marcaId: string): Observable<OlxItem[]> {
    return this.http.get<OlxItem[]>(`${this.url}/catalogo/marcas/${encodeURIComponent(marcaId)}/modelos`);
  }

  versoes(marcaId: string, modeloId: string): Observable<OlxItem[]> {
    return this.http.get<OlxItem[]>(`${this.url}/catalogo/marcas/${encodeURIComponent(marcaId)}/modelos/${encodeURIComponent(modeloId)}/versoes`);
  }

  atributos(): Observable<OlxAtributos> {
    return this.http.get<OlxAtributos>(`${this.url}/catalogo/atributos`);
  }

  veiculo(veiculoId: number): Observable<OlxVeiculoInfo> {
    return this.http.get<OlxVeiculoInfo>(`${this.url}/veiculos/${veiculoId}`);
  }

  publicar(veiculoId: number, dados: OlxAnuncioDados): Observable<OlxVeiculoInfo> {
    return this.http.post<OlxVeiculoInfo>(`${this.url}/veiculos/${veiculoId}`, dados);
  }

  pausar(veiculoId: number): Observable<OlxVeiculoInfo> {
    return this.http.post<OlxVeiculoInfo>(`${this.url}/veiculos/${veiculoId}/pausar`, {});
  }
}

/** Texto do erro devolvido pela API ({ error }) ou uma mensagem padrao. */
export function erroOlx(err: any, padrao = 'Não foi possível falar com a OLX.'): string {
  return err?.error?.error || err?.error?.message || padrao;
}
