import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface BancoInfo {
  codigoBanco: string;
  nomeBanco: string;
  configurado: boolean;
}

export interface SimulacaoRequest {
  valorVeiculo: number;
  valorEntrada: number;
  parcelas: number;
  anoVeiculo: number;
  tipoVeiculo: string;
  cpfCliente: string;
  rendaMensal: number;
}

export interface SimulacaoResultado {
  banco: string;
  codigoBanco: string;
  aprovado: boolean;
  mensagem: string;
  taxaMensal: number;
  taxaAnual: number;
  valorParcela: number;
  valorFinanciado: number;
  valorTotal: number;
  cetAnual: number;
  parcelas: number;
  simulacaoId: string;
}

export interface PropostaRequest {
  simulacaoId: string;
  veiculoId: number;
  marca: string;
  modelo: string;
  ano: number;
  placa: string;
  chassi: string;
  km: number;
  valorVeiculo: number;
  valorEntrada: number;
  parcelas: number;
  nomeCliente: string;
  cpfCliente: string;
  rgCliente: string;
  dataNascimento: string;
  telefoneCliente: string;
  emailCliente: string;
  enderecoCliente: string;
  rendaMensal: number;
}

export interface PropostaResultado {
  banco: string;
  propostaExternaId: string;
  status: string;
  mensagem: string;
  taxaAprovada: number | null;
  valorParcelaAprovada: number | null;
  parcelasAprovadas: number | null;
  urlContrato: string | null;
}

@Injectable({
  providedIn: 'root'
})
export class FinanciamentoBancoService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/financiamento/bancos`;

  listarBancos(): Observable<BancoInfo[]> {
    return this.http.get<BancoInfo[]>(this.baseUrl);
  }

  simularBanco(codigoBanco: string, request: SimulacaoRequest): Observable<SimulacaoResultado> {
    return this.http.post<SimulacaoResultado>(`${this.baseUrl}/simular/${codigoBanco}`, request);
  }

  simularTodos(request: SimulacaoRequest): Observable<SimulacaoResultado[]> {
    return this.http.post<SimulacaoResultado[]>(`${this.baseUrl}/simular-todos`, request);
  }

  enviarProposta(codigoBanco: string, request: PropostaRequest): Observable<PropostaResultado> {
    return this.http.post<PropostaResultado>(`${this.baseUrl}/proposta/${codigoBanco}`, request);
  }

  consultarStatus(codigoBanco: string, propostaExternaId: string): Observable<any> {
    return this.http.get(`${this.baseUrl}/proposta/${codigoBanco}/${propostaExternaId}`);
  }
}
