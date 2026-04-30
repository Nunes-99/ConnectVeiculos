import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface VeiculoDocumento {
  docId: number;
  r_VeiId: number;
  docTipo: string;
  docStatus: string;
  docArquivo?: string;
  docObservacao?: string;
  docDtVencimento?: string;
  docDtCriacao: string;
  docDtConclusao?: string;
}

export interface VeiculoDocumentoInput {
  veiculoId: number;
  tipo: string;
  status?: string;
  arquivo?: string;
  observacao?: string;
  dataVencimento?: string;
}

export const TIPOS_DOCUMENTO = [
  { codigo: 'CRLV', label: 'CRLV' },
  { codigo: 'LAUDO_CAUTELAR', label: 'Laudo Cautelar' },
  { codigo: 'TRANSFERENCIA', label: 'Transferência' },
  { codigo: 'VISTORIA', label: 'Vistoria' },
  { codigo: 'IPVA', label: 'IPVA' },
  { codigo: 'LICENCIAMENTO', label: 'Licenciamento' },
  { codigo: 'OUTROS', label: 'Outros' }
];

export const STATUS_DOCUMENTO = [
  { codigo: 'PENDENTE', label: 'Pendente' },
  { codigo: 'EM_ANDAMENTO', label: 'Em andamento' },
  { codigo: 'CONCLUIDO', label: 'Concluído' }
];

@Injectable({ providedIn: 'root' })
export class VeiculoDocumentoService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/veiculos-documentos`;

  listarPorVeiculo(veiculoId: number): Observable<VeiculoDocumento[]> {
    return this.http.get<VeiculoDocumento[]>(`${this.baseUrl}/veiculo/${veiculoId}`);
  }

  listarVencendo(diasAFrente = 30): Observable<VeiculoDocumento[]> {
    return this.http.get<VeiculoDocumento[]>(`${this.baseUrl}/vencendo?diasAFrente=${diasAFrente}`);
  }

  criar(data: VeiculoDocumentoInput): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(this.baseUrl, data);
  }

  atualizar(id: number, data: VeiculoDocumentoInput): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, data);
  }

  alterarStatus(id: number, status: string): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}/status`, { status });
  }

  excluir(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
