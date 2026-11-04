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
  proximoPasso: string;
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

  criarVeiculoTeste(): Observable<RenaveVeiculoTeste> {
    return this.http.post<RenaveVeiculoTeste>(`${this.url}/teste/veiculo`, {});
  }

  simularDetran(veiculoId: number): Observable<RenaveSituacao> {
    return this.http.post<RenaveSituacao>(`${this.url}/teste/veiculos/${veiculoId}/transferencia-detran`, {});
  }
}

/** Texto do erro devolvido pela API ({ error }) ou uma mensagem padrao. */
export function erroRenave(err: any, padrao = 'Não foi possível concluir no Renave.'): string {
  return err?.error?.error || err?.error?.message || padrao;
}
