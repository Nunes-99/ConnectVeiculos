import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

/** Configuracao do Renave da loja (certificado e senha nunca voltam da API). */
export interface RenaveConfig {
  ambiente: 'homologacao' | 'producao';
  operadorCpf: string | null;
  operadorNome: string | null;
  emailLoja: string | null;
  /** CNPJ enviado no cabecalho quando quem autentica e' uma integradora. */
  cnpjEstabelecimento: string | null;
  certificadoCadastrado: boolean;
  certificadoTitular: string | null;
  certificadoValidade: string | null;
  pronto: boolean;
}

export interface RenaveConexao {
  ambiente: string;
  cnpj: string | null;
  nome: string | null;
  integradora: string | null;
}

export interface RenaveSituacao {
  veiculoId: number;
  idEstoque: number | null;
  estado: string | null;
  dataEntrada: string | null;
  valorCompra: number | null;
  dataAssinaturaVendedor: string | null;
  chaveNfEntrada: string | null;
  dataSaida: string | null;
  valorVenda: number | null;
  compradorNome: string | null;
  chaveNfSaida: string | null;
  numeroAtpv: string | null;
  ultimoErro: string | null;
  atualizadoEm: string | null;
  /** Zero km: sem CRV, sem assinatura do vendedor, sem ATPVe e sem termos. */
  zeroKm: boolean;
  proximoPasso: string;
}

export interface RenaveImportacaoItem {
  idEstoque: number;
  placa: string | null;
  chassi: string | null;
  estado: string | null;
  zeroKm: boolean;
  veiculoId: number | null;
  acao: 'vinculado' | 'criado' | 'ja-vinculado' | 'ignorado';
  mensagem: string | null;
}

export interface RenaveImportacao {
  encontrados: number;
  vinculados: number;
  criados: number;
  jaVinculados: number;
  ignorados: number;
  soUltimosTresMeses: boolean;
  itens: RenaveImportacaoItem[];
}

export interface RenaveZeroKmPendente {
  chassi: string;
  dataHoraCadastro: string | null;
  acabado: boolean;
  veiculoId: number | null;
  veiculoDescricao: string | null;
}

export interface RenaveEntradaZeroKm {
  chassi: string;
  veiculoId: number | null;
  dataEntrada: string;
  valorCompra: number;
  quilometragemHodometro: number;
  chaveNotaFiscal: string;
}

export type MotivoRejeicaoZeroKm = 'DESISTENCIA' | 'ERRO_OPERACIONAL_MONTADORA';

export interface RenaveEntrega {
  id: number;
  chassi: string | null;
  estado: 'PENDENTE' | 'REALIZADA' | 'CANCELADA' | null;
  cnpjEntregador: string | null;
  cnpjVendedor: string | null;
  idAtpv: string | null;
  dataRealizacao: string | null;
  dataCancelamento: string | null;
  motivoCancelamento: string | null;
}

export interface RenaveConsulta {
  placa: string | null;
  renavam: string | null;
  chassi: string | null;
  acabado: boolean;
  dataPreCadastro: string | null;
  restricoes: string[];
  estoques: { id: number; estado: string | null; dataEntrada: string | null; dataSaida: string | null; motivoSaida: string | null }[];
  veiculoId: number | null;
}

export interface RenaveAptidao {
  apto: boolean;
  motivos: string[];
  comunicacaoComDetranFalhou: boolean;
  existemDebitos: boolean;
  debitoIpva: number | null;
  debitoLicenciamento: number | null;
  debitoMultas: number | null;
  debitoDpvat: number | null;
  outrosDebitos: number | null;
  restricoes: string[];
}

export type TipoCrv = 'AZUL' | 'VERDE' | 'BRANCO' | 'DIGITAL';

export interface RenaveEntrada {
  tipoCrv: TipoCrv;
  numeroCrv: string | null;
  codigoSegurancaCrv: string;
  documentoVendedor: string;
  emailVendedor: string;
  dataCompra: string;
  valorCompra: number;
  quilometragemHodometro: number;
}

export interface RenaveSaida {
  codigoSegurancaCrv: string;
  numeroCrv: string | null;
  documentoComprador: string;
  nomeComprador: string | null;
  emailComprador: string | null;
  codigoMunicipio: number;
  cep: string | null;
  logradouro: string | null;
  numero: string | null;
  complemento: string | null;
  bairro: string | null;
  dataVenda: string;
  valorVenda: number;
  /** NF-e de venda: obrigatoria no zero km (vai junto com a saida). */
  chaveNotaFiscal?: string | null;
}

export interface RenaveMunicipio { id: number; nome: string; uf: string; }

export interface RenaveVeiculoTeste {
  placa: string;
  renavam: string;
  chassi: string;
  numeroCrv: string;
  codigoSegurancaCrv: string;
  documentoProprietario: string;
}

@Injectable({ providedIn: 'root' })
export class RenaveService {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/renave`;

  obterConfig(): Observable<RenaveConfig> {
    return this.http.get<RenaveConfig>(`${this.url}/config`);
  }

  salvarConfig(config: Partial<RenaveConfig>): Observable<RenaveConfig> {
    return this.http.put<RenaveConfig>(`${this.url}/config`, config);
  }

  enviarCertificado(arquivo: File, senha: string): Observable<RenaveConfig> {
    const form = new FormData();
    form.append('arquivo', arquivo);
    form.append('senha', senha);
    return this.http.post<RenaveConfig>(`${this.url}/certificado`, form);
  }

  removerCertificado(): Observable<RenaveConfig> {
    return this.http.delete<RenaveConfig>(`${this.url}/certificado`);
  }

  testarConexao(): Observable<RenaveConexao> {
    return this.http.post<RenaveConexao>(`${this.url}/testar-conexao`, {});
  }

  municipios(uf: string, nome: string): Observable<RenaveMunicipio[]> {
    return this.http.get<RenaveMunicipio[]>(`${this.url}/municipios`, { params: { uf, nome } });
  }

  situacao(veiculoId: number): Observable<RenaveSituacao> {
    return this.http.get<RenaveSituacao>(`${this.url}/veiculos/${veiculoId}`);
  }

  situacoes(ids: number[]): Observable<Record<number, RenaveSituacao>> {
    return this.http.get<Record<number, RenaveSituacao>>(`${this.url}/veiculos`, { params: { ids: ids.join(',') } });
  }

  atualizar(veiculoId: number): Observable<RenaveSituacao> {
    return this.http.post<RenaveSituacao>(`${this.url}/veiculos/${veiculoId}/atualizar`, {});
  }

  aptidao(veiculoId: number, tipoCrv: TipoCrv, numeroCrv: string | null): Observable<RenaveAptidao> {
    return this.http.post<RenaveAptidao>(`${this.url}/veiculos/${veiculoId}/aptidao`, { tipoCrv, numeroCrv });
  }

  entrada(veiculoId: number, dados: RenaveEntrada): Observable<RenaveSituacao> {
    return this.http.post<RenaveSituacao>(`${this.url}/veiculos/${veiculoId}/entrada`, dados);
  }

  assinaturaVendedor(veiculoId: number, tipo: 'crv-papel' | 'atpve-impresso', foto: File): Observable<RenaveSituacao> {
    const form = new FormData();
    form.append('tipo', tipo);
    form.append('foto', foto);
    return this.http.post<RenaveSituacao>(`${this.url}/veiculos/${veiculoId}/assinatura-vendedor`, form);
  }

  notaFiscal(veiculoId: number, evento: 'COMPRA' | 'VENDA', chave: string): Observable<RenaveSituacao> {
    return this.http.post<RenaveSituacao>(`${this.url}/veiculos/${veiculoId}/nota-fiscal`, { evento, chave });
  }

  saida(veiculoId: number, dados: RenaveSaida): Observable<RenaveSituacao> {
    return this.http.post<RenaveSituacao>(`${this.url}/veiculos/${veiculoId}/saida`, dados);
  }

  atpv(veiculoId: number): Observable<Blob> {
    return this.http.get(`${this.url}/veiculos/${veiculoId}/atpv`, { responseType: 'blob' });
  }

  /** Termo de entrada ou de saida do estoque (PDF). */
  termo(veiculoId: number, tipo: 'entrada' | 'saida'): Observable<Blob> {
    return this.http.get(`${this.url}/veiculos/${veiculoId}/termo-${tipo}`, { responseType: 'blob' });
  }

  /** Traz o estoque que ja esta no Renave para o sistema (nao faz nova entrada). */
  importarEstoque(): Observable<RenaveImportacao> {
    return this.http.post<RenaveImportacao>(`${this.url}/importar-estoque`, {});
  }

  consultar(filtro: { placa?: string; chassi?: string; renavam?: string }): Observable<RenaveConsulta> {
    const params: Record<string, string> = {};
    for (const [k, v] of Object.entries(filtro)) if (v && v.trim()) params[k] = v.trim();
    return this.http.get<RenaveConsulta>(`${this.url}/consulta`, { params });
  }

  zeroKmPendentes(): Observable<RenaveZeroKmPendente[]> {
    return this.http.get<RenaveZeroKmPendente[]>(`${this.url}/zero-km/pendentes`);
  }

  entradaZeroKm(dados: RenaveEntradaZeroKm): Observable<RenaveSituacao> {
    return this.http.post<RenaveSituacao>(`${this.url}/zero-km/entrada`, dados);
  }

  rejeitarZeroKm(chassi: string, motivo: MotivoRejeicaoZeroKm): Observable<{ ok: boolean }> {
    return this.http.post<{ ok: boolean }>(`${this.url}/zero-km/rejeicao`, { chassi, motivo });
  }

  entregas(estado: string): Observable<RenaveEntrega[]> {
    const params: Record<string, string> = estado ? { estado } : {};
    return this.http.get<RenaveEntrega[]>(`${this.url}/entregas`, { params });
  }

  realizarEntrega(id: number): Observable<RenaveEntrega> {
    return this.http.post<RenaveEntrega>(`${this.url}/entregas/${id}/realizar`, {});
  }

  cancelarEntrega(id: number): Observable<RenaveEntrega> {
    return this.http.post<RenaveEntrega>(`${this.url}/entregas/${id}/cancelar`, {});
  }

  criarVeiculoTeste(): Observable<RenaveVeiculoTeste> {
    return this.http.post<RenaveVeiculoTeste>(`${this.url}/teste/veiculo`, {});
  }

  criarZeroKmTeste(): Observable<{ chassi: string; acabado: boolean }> {
    return this.http.post<{ chassi: string; acabado: boolean }>(`${this.url}/teste/zero-km`, {});
  }

  simularDetran(veiculoId: number): Observable<RenaveSituacao> {
    return this.http.post<RenaveSituacao>(`${this.url}/teste/veiculos/${veiculoId}/transferencia-detran`, {});
  }
}

/** Texto do erro devolvido pela API ({ error }) ou uma mensagem padrao. */
export function erroRenave(err: any, padrao = 'Não foi possível concluir no Renave.'): string {
  return err?.error?.error || err?.error?.message || padrao;
}

/** Salva um PDF vindo da API; com responseType blob, o erro ({ error }) chega como Blob. */
export function baixarPdfRenave(obs: Observable<Blob>, nome: string, aoFim: (erro: string | null) => void): void {
  obs.subscribe({
    next: blob => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = nome;
      a.click();
      URL.revokeObjectURL(url);
      aoFim(null);
    },
    error: async err => {
      let msg = 'Não foi possível baixar o PDF.';
      try { msg = JSON.parse(await (err.error as Blob).text()).error || msg; } catch { /* mantem a padrao */ }
      aoFim(msg);
    }
  });
}
