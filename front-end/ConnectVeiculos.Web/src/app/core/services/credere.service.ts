import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface CredereLoja { id: number; nome: string; cnpj: string | null; cidade: string | null; uf: string | null; }
export interface CredereBancoLoja { codigo: string; nome: string; ativo: boolean; status: string | null; }

export interface CredereStatus {
  configurado: boolean;
  /** Origem da volta da Credere (ex.: https://connectveiculos.dev.br). */
  siteConexao: string | null;
  conectado: boolean;
  conta: string | null;
  usuario: string | null;
  conectadoEm: string | null;
  expiraEm: string | null;
  lojaId: number | null;
  lojaNome: string | null;
  lojas: CredereLoja[];
  bancos: CredereBancoLoja[];
  erro: string | null;
}

export interface CredereModeloVeiculo {
  id: number;
  marca: string;
  nome: string;
  versao: string | null;
  anoInicio: number | null;
  anoFim: number | null;
  fipe: string | null;
}

export interface CredereCampoExigido { campo: string; bancos: string[]; }

export interface CredereSimulacaoInput {
  nascimento: string | null;
  possuiCnh: boolean | null;
  genero: string | null;
  rendaMensal: number | null;
  cep: string | null;
  ocupacao: string | null;
  modeloId: number;
  modeloNome: string | null;
  anoFabricacao: number;
  anoModelo: number;
  ufLicenciamento: string;
  cidadeLicenciamento: string;
  prazos: number[];
}

export interface CredereCondicao {
  id: number;
  banco: string;
  codigoBanco: string;
  prazo: number;
  entrada: number;
  parcela: number | null;
  ultimaParcela: number | null;
  financiado: number | null;
  totalPago: number | null;
  taxaMensal: number | null;
  cetMensal: number | null;
  processando: boolean;
  disponivel: boolean;
  aprovado: boolean;
  preAprovacao: number | null;
  motivo: string | null;
  motivoCodigo: string | null;
  sugestao: string | null;
  entradaMinima: number | null;
  entradaSugerida: number | null;
  subsidioFabrica: boolean;
}

export interface CredereSimulacao {
  uuid: string;
  concluida: boolean;
  criadaEm: string | null;
  condicoes: CredereCondicao[];
}

/** Campos do lead que os bancos podem exigir, com o nome que aparece na tela. */
export const CAMPOS_CREDERE: Record<string, string> = {
  birthdate: 'Data de nascimento',
  has_cnh: 'Possui CNH',
  retrieve_gender: 'Gênero',
  monthly_income: 'Renda mensal',
  retrieve_occupation: 'Ocupação',
  'address.zip_code': 'CEP',
  email: 'E-mail',
  phone_number: 'Telefone',
  name: 'Nome',
  cpf_cnpj: 'CPF'
};

/** Ocupacoes da Credere (dominio occupation). */
export const OCUPACOES_CREDERE = [
  { codigo: '1', nome: 'Empregado de empresa privada' },
  { codigo: '11', nome: 'Autônomo' },
  { codigo: '12', nome: 'Empresário/Sócio' },
  { codigo: '100', nome: 'Profissional liberal' },
  { codigo: '21', nome: 'Servidor público' },
  { codigo: '61', nome: 'Aposentado/pensionista' },
  { codigo: '51', nome: 'Militar' },
  { codigo: '13', nome: 'Renda de aluguel/capital' },
  { codigo: '91', nome: 'Setor rural' },
  { codigo: '72', nome: 'Bolsista' }
];

@Injectable({ providedIn: 'root' })
export class CredereService {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/integracoes/credere`;

  status(): Observable<CredereStatus> {
    return this.http.get<CredereStatus>(this.url);
  }

  urlAutorizacao(): Observable<{ url: string }> {
    return this.http.get<{ url: string }>(`${this.url}/autorizar`);
  }

  conectar(code: string, state: string | null): Observable<CredereStatus> {
    return this.http.post<CredereStatus>(`${this.url}/conectar`, { code, state });
  }

  /** Volta do login da Credere; funciona sem sessao (a loja vem do state). */
  retorno(code: string, state: string | null, tenant?: string): Observable<{ conectado: boolean; tenant: string }> {
    const q = tenant ? `?tenant=${encodeURIComponent(tenant)}` : '';
    return this.http.post<{ conectado: boolean; tenant: string }>(`${this.url}/retorno${q}`, { code, state });
  }

  escolherLoja(lojaId: number): Observable<CredereStatus> {
    return this.http.put<CredereStatus>(`${this.url}/loja`, { lojaId });
  }

  desconectar(): Observable<unknown> {
    return this.http.delete(this.url);
  }

  modelos(busca: string, ano: number | null): Observable<CredereModeloVeiculo[]> {
    const params: Record<string, string> = { busca };
    if (ano) params['ano'] = String(ano);
    return this.http.get<CredereModeloVeiculo[]>(`${this.url}/modelos`, { params });
  }

  campos(fichaId: number): Observable<CredereCampoExigido[]> {
    return this.http.get<CredereCampoExigido[]>(`${this.url}/fichas/${fichaId}/campos`);
  }

  simular(fichaId: number, dados: CredereSimulacaoInput): Observable<CredereSimulacao> {
    return this.http.post<CredereSimulacao>(`${this.url}/fichas/${fichaId}/simular`, dados);
  }

  simulacao(fichaId: number): Observable<CredereSimulacao> {
    return this.http.get<CredereSimulacao>(`${this.url}/fichas/${fichaId}/simulacao`);
  }
}

export function erroCredere(err: any, padrao: string): string {
  return err?.error?.error || padrao;
}
