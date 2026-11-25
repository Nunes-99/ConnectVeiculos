import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface NapistaItem { id: string; nome: string; }

export interface NapistaLoja { sellerId: string; nome: string; cnpj: string | null; }

export interface NapistaStatus {
  /** O ConnectVeiculos ja tem client_id do NaPista neste ambiente. */
  configurado: boolean;
  desenvolvimento: boolean;
  conectado: boolean;
  conectadoEm: string | null;
  /** Ate quando a conexao vale sem entrar de novo; nulo = sem prazo. */
  validaAte: string | null;
  sellerId: string | null;
  lojaNome: string | null;
  lojas: NapistaLoja[];
  /** Endereco onde a conexao tem de comecar (o da redirect_uri). */
  siteConexao: string | null;
  erro: string | null;
}

export interface NapistaAtributos {
  cores: NapistaItem[];
  combustiveis: NapistaItem[];
  cambios: NapistaItem[];
  opcionais: NapistaItem[];
}

export interface NapistaAnuncioDados {
  categoria: string;
  marcaId: string | null;
  modeloId: string | null;
  versaoId: string | null;
  anoModelo: number | null;
  anoFabricacao: number | null;
  cor: string | null;
  combustivel: string | null;
  cambio: string | null;
  portas: number | null;
  blindado: boolean;
  opcionais: string[];
}

export interface NapistaVeiculoInfo {
  publicado: boolean;
  anuncioId: string | null;
  status: string;
  erro: string | null;
  publicadoEm: string | null;
  atualizadoEm: string | null;
  fotos: number;
  dados: NapistaAnuncioDados;
  sugestao: boolean;
}

/** Anuncio que ja esta no NaPista, para importar como veiculo. */
export interface NapistaAnuncioExistente {
  anuncioId: string;
  titulo: string;
  status: string;
  anoModelo: number | null;
  anoFabricacao: number | null;
  km: number;
  preco: number;
  placa: string | null;
  placaEscondida: boolean;
  cor: string | null;
  fotos: number;
  fotoCapa: string | null;
  veiculoVinculadoId: number | null;
  veiculoMesmaPlacaId: number | null;
  veiculoDescricao: string | null;
}

export interface NapistaImportarPedido {
  lojaId: number;
  categoriaId: number;
  itens: { anuncioId: string; placa?: string | null }[];
}

export interface NapistaImportacaoResultado {
  criados: number;
  vinculados: number;
  erros: number;
  detalhes: { anuncioId: string; titulo: string; sucesso: boolean; acao: string; veiculoId: number | null; fotos: number; mensagem: string }[];
}

export interface NapistaLeadsResultado { encontrados: number; importados: number; jaExistiam: number; }

@Injectable({ providedIn: 'root' })
export class NapistaService {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/integracoes/napista`;

  status(): Observable<NapistaStatus> {
    return this.http.get<NapistaStatus>(this.url);
  }

  urlAutorizacao(): Observable<{ url: string }> {
    return this.http.get<{ url: string }>(`${this.url}/autorizar`);
  }

  /** Volta do login do NaPista. Sem sessao: a loja vem do state (ver NapistaController.Retorno). */
  retorno(code: string, state: string | null, tenant?: string): Observable<{ conectado: boolean; tenant: string }> {
    const q = tenant ? `?tenant=${encodeURIComponent(tenant)}` : '';
    return this.http.post<{ conectado: boolean; tenant: string }>(`${this.url}/retorno${q}`, { code, state });
  }

  escolherLoja(sellerId: string): Observable<NapistaStatus> {
    return this.http.put<NapistaStatus>(`${this.url}/loja`, { sellerId });
  }

  desconectar(): Observable<{ ok: boolean }> {
    return this.http.delete<{ ok: boolean }>(this.url);
  }

  marcas(categoria: string): Observable<NapistaItem[]> {
    return this.http.get<NapistaItem[]>(`${this.url}/catalogo/${categoria}/marcas`);
  }

  modelos(categoria: string, marcaId: string, ano: number | null): Observable<NapistaItem[]> {
    const params: Record<string, string> = ano ? { ano: String(ano) } : {};
    return this.http.get<NapistaItem[]>(`${this.url}/catalogo/${categoria}/marcas/${encodeURIComponent(marcaId)}/modelos`, { params });
  }

  versoes(categoria: string, modeloId: string, ano: number | null): Observable<NapistaItem[]> {
    const params: Record<string, string> = ano ? { ano: String(ano) } : {};
    return this.http.get<NapistaItem[]>(`${this.url}/catalogo/${categoria}/modelos/${encodeURIComponent(modeloId)}/versoes`, { params });
  }

  atributos(categoria: string): Observable<NapistaAtributos> {
    return this.http.get<NapistaAtributos>(`${this.url}/catalogo/${categoria}/atributos`);
  }

  veiculo(veiculoId: number): Observable<NapistaVeiculoInfo> {
    return this.http.get<NapistaVeiculoInfo>(`${this.url}/veiculos/${veiculoId}`);
  }

  publicar(veiculoId: number, dados: NapistaAnuncioDados): Observable<NapistaVeiculoInfo> {
    return this.http.post<NapistaVeiculoInfo>(`${this.url}/veiculos/${veiculoId}`, dados);
  }

  pausar(veiculoId: number): Observable<NapistaVeiculoInfo> {
    return this.http.post<NapistaVeiculoInfo>(`${this.url}/veiculos/${veiculoId}/pausar`, {});
  }

  anunciosExistentes(): Observable<NapistaAnuncioExistente[]> {
    return this.http.get<NapistaAnuncioExistente[]>(`${this.url}/anuncios-existentes`);
  }

  importarAnuncios(pedido: NapistaImportarPedido): Observable<NapistaImportacaoResultado> {
    return this.http.post<NapistaImportacaoResultado>(`${this.url}/anuncios-existentes/importar`, pedido);
  }

  importarLeads(dias = 30): Observable<NapistaLeadsResultado> {
    return this.http.post<NapistaLeadsResultado>(`${this.url}/leads/importar`, {}, { params: { dias: String(dias) } });
  }
}

/** Texto do erro devolvido pela API ({ error }) ou uma mensagem padrao. */
export function erroNapista(err: any, padrao = 'Não foi possível falar com o NaPista.'): string {
  return err?.error?.error || err?.error?.message || padrao;
}
