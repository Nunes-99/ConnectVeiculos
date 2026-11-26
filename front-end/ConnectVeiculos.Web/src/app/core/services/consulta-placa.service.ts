import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PixGerado } from './plano.service';

export interface ServicoConsulta {
  id: string;
  nome: string;
  descricao: string;
  preco: number;
  recomendado: boolean;
}

export interface AlertaConsulta {
  texto: string;
  grave: boolean;
}

/** Bloco "veiculo" da ZapCar (snake_case, como a base devolve). */
export interface VeiculoConsultado {
  placa?: string | null;
  renavam?: string | null;
  chassi?: string | null;
  marca?: string | null;
  modelo?: string | null;
  ano_fabricacao?: number | null;
  ano_modelo?: number | null;
  cor?: string | null;
  combustivel?: string | null;
  municipio?: string | null;
  uf?: string | null;
  situacao?: string | null;
  proprietario?: { nome?: string | null; documento?: string | null } | null;
  ultimo_licenciamento?: number | null;
  debitos?: { tipo: string; descricao?: string | null; valor_centavos: number; valor_informado?: boolean }[];
  debitos_total_centavos?: number;
  leilao?: { consta: boolean; ocorrencias: number; fotos?: number } | null;
  sinistro?: boolean | null;
  recall?: boolean | null;
}

export interface ConsultaItem {
  id: number;
  placa: string;
  servico: string;
  servicoNome: string;
  status: 'PROCESSANDO' | 'CONCLUIDA' | 'ERRO';
  valor: number;
  erro: string | null;
  estornada: boolean;
  teste: boolean;
  usuario: string | null;
  criadoEm: string;
  concluidoEm: string | null;
  veiculo: VeiculoConsultado | null;
  alertas: AlertaConsulta[];
  nadaConsta: boolean;
}

export interface MovimentoCredito {
  tipo: 'COMPRA' | 'CONSUMO' | 'ESTORNO' | 'AJUSTE';
  valor: number;
  descricao: string | null;
  data: string;
}

export interface ConsultaPlacaPainel {
  disponivel: boolean;
  teste: boolean;
  saldo: number;
  servicos: ServicoConsulta[];
  pacotes: number[];
  consultas: ConsultaItem[];
  extrato: MovimentoCredito[];
}

@Injectable({ providedIn: 'root' })
export class ConsultaPlacaService {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/consulta-placa`;

  painel(): Observable<ConsultaPlacaPainel> {
    return this.http.get<ConsultaPlacaPainel>(this.url);
  }

  consultar(placa: string, servico: string): Observable<ConsultaItem> {
    return this.http.post<ConsultaItem>(this.url, { placa, servico });
  }

  obter(id: number): Observable<ConsultaItem> {
    return this.http.get<ConsultaItem>(`${this.url}/${id}`);
  }

  documento(id: number): Observable<Blob> {
    return this.http.get(`${this.url}/${id}/documento`, { responseType: 'blob' });
  }

  comprarCreditos(valor: number, email: string): Observable<PixGerado> {
    return this.http.post<PixGerado>(`${this.url}/creditos/pix`, { valor, email });
  }
}
