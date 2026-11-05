import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export type TipoContrato = 'VENDA' | 'COMPRA' | 'CONSIGNACAO' | 'GARANTIA' | 'RECIBO' | 'OUTRO';

export const TIPOS_CONTRATO: { valor: TipoContrato; nome: string }[] = [
  { valor: 'VENDA', nome: 'Venda' },
  { valor: 'COMPRA', nome: 'Compra' },
  { valor: 'CONSIGNACAO', nome: 'Consignação' },
  { valor: 'GARANTIA', nome: 'Garantia' },
  { valor: 'RECIBO', nome: 'Recibo' },
  { valor: 'OUTRO', nome: 'Outro' }
];

export interface ModeloContrato {
  id: number;
  nome: string;
  descricao: string | null;
  tipo: TipoContrato;
  conteudo: string;
  ativo: boolean;
  padrao: boolean;
  atualizadoEm: string;
}

export interface ContratoVariavel { chave: string; descricao: string; grupo: string; }

export interface ContratoParte {
  nome: string;
  documento: string | null;
  email: string | null;
  telefone: string | null;
  endereco: string | null;
}

export interface ContratoExtra { chave: string; rotulo: string; }

export interface ContratoSugestao {
  tipo: TipoContrato;
  papelCliente: string;
  parte: ContratoParte;
  valor: number | null;
  formaPagamento: string | null;
  origem: 'venda' | 'veiculo' | null;
  extras: ContratoExtra[];
}

export interface ContratoTrecho { texto: string; negrito: boolean; faltando: boolean; }
export interface ContratoBloco { tipo: 'titulo' | 'secao' | 'paragrafo'; trechos: ContratoTrecho[]; }
export interface ContratoPreview { titulo: string; blocos: ContratoBloco[]; faltando: string[]; }

export interface ContratoGerar {
  modeloId: number;
  veiculoId: number;
  parte: ContratoParte;
  valor: number | null;
  formaPagamento: string | null;
  extras: Record<string, string>;
}

export interface ContratoSignatario {
  id: number;
  papel: 'CLIENTE' | 'LOJA';
  papelRotulo: string;
  nome: string;
  documento: string | null;
  email: string | null;
  telefone: string | null;
  status: 'PENDENTE' | 'ASSINADO';
  metodo: string | null;
  visualizadoEm: string | null;
  assinadoEm: string | null;
  token: string | null;
}

export interface Contrato {
  id: number;
  veiculoId: number;
  veiculo: string | null;
  placa: string | null;
  tipo: TipoContrato;
  titulo: string;
  status: 'AGUARDANDO' | 'ASSINADO' | 'CANCELADO';
  hash: string;
  criadoEm: string;
  concluidoEm: string | null;
  signatarios: ContratoSignatario[];
}

export interface AssinaturaDados {
  nome: string;
  documento: string | null;
  imagem: string;
  codigo?: string | null;
  aceite: boolean;
}

export interface AssinaturaPublica {
  loja: string;
  titulo: string;
  blocos: ContratoBloco[];
  hash: string;
  papel: string;
  nome: string;
  emailMascarado: string | null;
  precisaCodigo: boolean;
  jaAssinou: boolean;
  assinadoEm: string | null;
  statusContrato: string;
}

export const ROTULO_STATUS_CONTRATO: Record<string, string> = {
  AGUARDANDO: 'Aguardando assinatura',
  ASSINADO: 'Assinado',
  CANCELADO: 'Cancelado'
};

@Injectable({ providedIn: 'root' })
export class ContratoService {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/contratos`;
  private urlPublica = `${environment.apiUrl}/assinar`;

  modelos(todos = false): Observable<ModeloContrato[]> {
    return this.http.get<ModeloContrato[]>(`${this.url}/modelos`, { params: { todos } });
  }

  salvarModelo(id: number | null, dados: Partial<ModeloContrato>): Observable<ModeloContrato> {
    return id ? this.http.put<ModeloContrato>(`${this.url}/modelos/${id}`, dados) : this.http.post<ModeloContrato>(`${this.url}/modelos`, dados);
  }

  excluirModelo(id: number): Observable<unknown> {
    return this.http.delete(`${this.url}/modelos/${id}`);
  }

  variaveis(): Observable<ContratoVariavel[]> {
    return this.http.get<ContratoVariavel[]>(`${this.url}/variaveis`);
  }

  previewModelo(conteudo: string, tipo: string, veiculoId: number | null): Observable<ContratoPreview> {
    return this.http.post<ContratoPreview>(`${this.url}/modelos/preview`, { conteudo, tipo, veiculoId });
  }

  listar(filtro: { veiculoId?: number; status?: string; busca?: string } = {}): Observable<Contrato[]> {
    const params: Record<string, string> = {};
    for (const [k, v] of Object.entries(filtro)) if (v !== undefined && v !== null && String(v).trim()) params[k] = String(v).trim();
    return this.http.get<Contrato[]>(this.url, { params });
  }

  sugestao(veiculoId: number, modeloId: number): Observable<ContratoSugestao> {
    return this.http.get<ContratoSugestao>(`${this.url}/sugestao`, { params: { veiculoId, modeloId } });
  }

  preview(dados: ContratoGerar): Observable<ContratoPreview> {
    return this.http.post<ContratoPreview>(`${this.url}/preview`, dados);
  }

  gerar(dados: ContratoGerar): Observable<Contrato> {
    return this.http.post<Contrato>(this.url, dados);
  }

  texto(id: number): Observable<ContratoPreview> {
    return this.http.get<ContratoPreview>(`${this.url}/${id}/texto`);
  }

  cancelar(id: number): Observable<Contrato> {
    return this.http.post<Contrato>(`${this.url}/${id}/cancelar`, {});
  }

  assinarLoja(id: number, dados: AssinaturaDados): Observable<Contrato> {
    return this.http.post<Contrato>(`${this.url}/${id}/assinar-loja`, dados);
  }

  assinarPresencial(id: number, dados: AssinaturaDados): Observable<Contrato> {
    return this.http.post<Contrato>(`${this.url}/${id}/assinar-presencial`, dados);
  }

  enviarEmail(id: number): Observable<unknown> {
    return this.http.post(`${this.url}/${id}/enviar-email`, { urlBase: window.location.origin });
  }

  pdf(id: number): Observable<Blob> {
    return this.http.get(`${this.url}/${id}/pdf`, { responseType: 'blob' });
  }

  // ---------- Link publico ----------

  abrir(tenant: string, token: string): Observable<AssinaturaPublica> {
    return this.http.get<AssinaturaPublica>(`${this.urlPublica}/${token}`, { params: { tenant } });
  }

  pedirCodigo(tenant: string, token: string): Observable<{ email: string }> {
    return this.http.post<{ email: string }>(`${this.urlPublica}/${token}/codigo`, {}, { params: { tenant } });
  }

  assinarPeloLink(tenant: string, token: string, dados: AssinaturaDados): Observable<AssinaturaPublica> {
    return this.http.post<AssinaturaPublica>(`${this.urlPublica}/${token}`, dados, { params: { tenant } });
  }

  pdfPeloLink(tenant: string, token: string): Observable<Blob> {
    return this.http.get(`${this.urlPublica}/${token}/pdf`, { params: { tenant }, responseType: 'blob' });
  }
}

/** Link que o cliente abre para assinar. */
export function linkAssinatura(tenant: string, token: string): string {
  return `${window.location.origin}/assinar/${encodeURIComponent(tenant)}/${token}`;
}

/** Mensagem do backend (422 {error}), inclusive quando a resposta veio como Blob. */
export async function erroContrato(err: unknown, padrao: string): Promise<string> {
  if (err instanceof HttpErrorResponse) {
    if (err.error instanceof Blob) {
      try { return JSON.parse(await err.error.text())?.error || padrao; } catch { return padrao; }
    }
    if (err.error?.error) return err.error.error;
    if (err.status === 0) return 'Sem conexão com o servidor.';
  }
  return padrao;
}

export function baixarBlob(blob: Blob, nome: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = nome;
  a.click();
  URL.revokeObjectURL(url);
}
