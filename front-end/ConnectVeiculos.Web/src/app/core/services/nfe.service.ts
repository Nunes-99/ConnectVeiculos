import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface NfeRegraOperacao {
  naturezaOperacao: string;
  cfopInterno: string;
  cfopInterestadual: string;
  cstIcms: string;
  aliquotaIcms: number;
  reducaoBaseIcms: number;
  cstPis: string;
  cstCofins: string;
}

export interface NfeConfig {
  ambiente: 'homologacao' | 'producao';
  cnpj: string | null;
  inscricaoEstadual: string | null;
  razaoSocial: string | null;
  nomeFantasia: string | null;
  crt: number;
  logradouro: string | null;
  numero: string | null;
  complemento: string | null;
  bairro: string | null;
  cep: string | null;
  codigoMunicipio: number | null;
  municipio: string | null;
  uf: string | null;
  telefone: string | null;
  serie: number;
  proximoNumero: number;
  ncmPadrao: string;
  compra: NfeRegraOperacao;
  venda: NfeRegraOperacao;
  informacoesComplementares: string | null;
  respTecCnpj: string | null;
  respTecContato: string | null;
  respTecEmail: string | null;
  respTecTelefone: string | null;
  certificadoValido: boolean;
  certificadoCnpj: string | null;
  pronto: boolean;
  pendencias: string[];
}

export interface NfeEndereco {
  logradouro: string;
  numero: string;
  complemento: string;
  bairro: string;
  cep: string;
  codigoMunicipio: number;
  municipio: string;
  uf: string;
}

export interface NfeEmissao {
  tipo: 'ENTRADA' | 'SAIDA';
  documento: string;
  nome: string;
  inscricaoEstadual: string | null;
  email: string | null;
  telefone: string | null;
  endereco: NfeEndereco;
  valor: number;
  ncm: string | null;
  meioPagamento: string;
  informacoesComplementares: string | null;
}

/** Dados ja conhecidos para preencher a nota; origem diz de onde vieram. */
export interface NfeSugestao {
  dados: NfeEmissao;
  origem: 'nota' | 'venda' | 'renave' | null;
}

export const ORIGEM_SUGESTAO: Record<string, string> = {
  nota: 'da última tentativa desta nota',
  venda: 'da venda registrada',
  renave: 'do Renave'
};

export interface NfeResumo {
  id: number;
  veiculoId: number;
  tipo: 'ENTRADA' | 'SAIDA';
  ambiente: string;
  serie: number;
  numero: number;
  chave: string | null;
  status: 'PENDENTE' | 'AUTORIZADA' | 'REJEITADA' | 'CANCELADA' | 'INUTILIZADA';
  cStat: number | null;
  motivo: string | null;
  protocolo: string | null;
  dataEmissao: string;
  dataAutorizacao: string | null;
  valor: number;
  destinatarioNome: string | null;
  dataCancelamento: string | null;
  podeReenviar: boolean;
  podeCancelar: boolean;
  cartaCorrecaoSequencia: number;
  cartaCorrecaoTexto: string | null;
  cartaCorrecaoData: string | null;
  podeCorrigir: boolean;
  /** So na lista geral (tela Notas fiscais). */
  veiculo?: string | null;
  placa?: string | null;
}

/** Filtro da tela Notas fiscais; vazio = sem filtro. Datas em yyyy-MM-dd. */
export interface NfeFiltro {
  de: string;
  ate: string;
  tipo: '' | 'ENTRADA' | 'SAIDA';
  status: string;
  busca: string;
}

export interface NfeInutilizacao {
  id: number;
  ambiente: string;
  serie: number;
  numeroInicial: number;
  numeroFinal: number;
  justificativa: string;
  protocolo: string | null;
  data: string;
}

export interface NfeMunicipio { codigo: number; nome: string; uf: string; }

export interface CertificadoInfo {
  cadastrado: boolean;
  titular: string | null;
  cnpj: string | null;
  validade: string | null;
  valido: boolean;
}

/** Meios de pagamento da NF-e mais usados na venda de veiculo. */
export const MEIOS_PAGAMENTO_NFE = [
  { codigo: '17', nome: 'Pix' },
  { codigo: '01', nome: 'Dinheiro' },
  { codigo: '16', nome: 'Depósito bancário' },
  { codigo: '18', nome: 'Transferência (TED/DOC)' },
  { codigo: '15', nome: 'Boleto' },
  { codigo: '03', nome: 'Cartão de crédito' },
  { codigo: '04', nome: 'Cartão de débito' },
  { codigo: '02', nome: 'Cheque' },
  { codigo: '90', nome: 'Sem pagamento' },
  { codigo: '99', nome: 'Outros (financiamento, troca...)' }
];

@Injectable({ providedIn: 'root' })
export class NfeService {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/nfe`;
  private urlCert = `${environment.apiUrl}/certificado-digital`;

  obterConfig(): Observable<NfeConfig> {
    return this.http.get<NfeConfig>(`${this.url}/config`);
  }

  salvarConfig(config: NfeConfig): Observable<NfeConfig> {
    return this.http.put<NfeConfig>(`${this.url}/config`, config);
  }

  statusSefaz(): Observable<{ cStat: number; motivo: string; ambiente: string }> {
    return this.http.post<{ cStat: number; motivo: string; ambiente: string }>(`${this.url}/status-sefaz`, {});
  }

  municipios(uf: string, nome: string): Observable<NfeMunicipio[]> {
    return this.http.get<NfeMunicipio[]>(`${this.url}/municipios`, { params: { uf, nome } });
  }

  listar(filtro: NfeFiltro): Observable<NfeResumo[]> {
    const params: Record<string, string> = {};
    for (const [k, v] of Object.entries(filtro)) if (v && String(v).trim()) params[k] = String(v).trim();
    return this.http.get<NfeResumo[]>(this.url, { params });
  }

  doVeiculo(veiculoId: number): Observable<NfeResumo[]> {
    return this.http.get<NfeResumo[]>(`${this.url}/veiculos/${veiculoId}`);
  }

  sugestao(veiculoId: number, tipo: 'ENTRADA' | 'SAIDA'): Observable<NfeSugestao> {
    return this.http.get<NfeSugestao>(`${this.url}/veiculos/${veiculoId}/sugestao`, { params: { tipo } });
  }

  emitir(veiculoId: number, dados: NfeEmissao): Observable<NfeResumo> {
    return this.http.post<NfeResumo>(`${this.url}/veiculos/${veiculoId}`, dados);
  }

  reenviar(notaId: number, dados: NfeEmissao | null): Observable<NfeResumo> {
    return this.http.post<NfeResumo>(`${this.url}/${notaId}/reenviar`, dados);
  }

  cancelar(notaId: number, justificativa: string): Observable<NfeResumo> {
    return this.http.post<NfeResumo>(`${this.url}/${notaId}/cancelar`, { justificativa });
  }

  danfe(notaId: number): Observable<Blob> {
    return this.http.get(`${this.url}/${notaId}/danfe`, { responseType: 'blob' });
  }

  corrigir(notaId: number, correcao: string): Observable<NfeResumo> {
    return this.http.post<NfeResumo>(`${this.url}/${notaId}/carta-correcao`, { correcao });
  }

  xmlCartaCorrecao(notaId: number): Observable<Blob> {
    return this.http.get(`${this.url}/${notaId}/carta-correcao/xml`, { responseType: 'blob' });
  }

  inutilizacoes(): Observable<NfeInutilizacao[]> {
    return this.http.get<NfeInutilizacao[]>(`${this.url}/inutilizacoes`);
  }

  inutilizar(dados: { serie: number; numeroInicial: number; numeroFinal: number; justificativa: string }): Observable<NfeInutilizacao> {
    return this.http.post<NfeInutilizacao>(`${this.url}/inutilizacoes`, dados);
  }

  xmlInutilizacao(id: number): Observable<Blob> {
    return this.http.get(`${this.url}/inutilizacoes/${id}/xml`, { responseType: 'blob' });
  }

  xml(notaId: number): Observable<Blob> {
    return this.http.get(`${this.url}/${notaId}/xml`, { responseType: 'blob' });
  }

  certificado(): Observable<CertificadoInfo> {
    return this.http.get<CertificadoInfo>(this.urlCert);
  }

  enviarCertificado(arquivo: File, senha: string): Observable<CertificadoInfo> {
    const form = new FormData();
    form.append('arquivo', arquivo);
    form.append('senha', senha);
    return this.http.post<CertificadoInfo>(this.urlCert, form);
  }

  removerCertificado(): Observable<CertificadoInfo> {
    return this.http.delete<CertificadoInfo>(this.urlCert);
  }
}

/** Baixa um Blob como arquivo. */
export function baixarArquivo(blob: Blob, nome: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = nome;
  a.click();
  URL.revokeObjectURL(url);
}

/** Erro de API que chegou como Blob (responseType blob) ou JSON. */
export async function erroApi(err: any, padrao: string): Promise<string> {
  if (err?.error instanceof Blob) {
    try { return JSON.parse(await err.error.text()).error || padrao; } catch { return padrao; }
  }
  return err?.error?.error || err?.error?.message || padrao;
}
