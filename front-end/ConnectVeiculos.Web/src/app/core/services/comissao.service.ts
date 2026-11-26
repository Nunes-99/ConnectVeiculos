import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export type TipoComissao = 'PERCENTUAL_VENDA' | 'PERCENTUAL_LUCRO' | 'VALOR_FIXO';

export interface RegraComissao { tipo: TipoComissao; valor: number; }

export interface RegrasComissao {
  padrao: RegraComissao;
  /** Excecoes por vendedor (id do usuario). */
  vendedores: Record<number, RegraComissao>;
}

export interface ComissaoSugerida {
  percentual: number;
  valor: number;
  tipo: TipoComissao;
  regra: string;
  regraDoVendedor: boolean;
}

export interface ComissaoVendedor {
  vendedorId: number;
  nome: string;
  vendas: number;
  vendido: number;
  comissao: number;
  pago: number;
  aPagar: number;
}

export interface ComissaoItem {
  venId: number;
  data: string;
  vendedorId: number;
  vendedor: string;
  veiculo: string;
  comprador: string | null;
  valor: number;
  percentual: number;
  comissao: number;
  pagaEm: string | null;
}

export interface ComissaoExtrato {
  inicio: string;
  fim: string;
  totalVendido: number;
  totalComissao: number;
  totalPago: number;
  totalAPagar: number;
  vendedores: ComissaoVendedor[];
  vendas: ComissaoItem[];
}

export const TIPOS_COMISSAO: { id: TipoComissao; nome: string; unidade: string }[] = [
  { id: 'PERCENTUAL_VENDA', nome: '% do valor da venda', unidade: '%' },
  { id: 'PERCENTUAL_LUCRO', nome: '% do lucro (venda - compra - despesas)', unidade: '%' },
  { id: 'VALOR_FIXO', nome: 'Valor fixo por carro', unidade: 'R$' }
];

@Injectable({ providedIn: 'root' })
export class ComissaoService {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/comissoes`;

  regras(): Observable<RegrasComissao> {
    return this.http.get<RegrasComissao>(`${this.url}/regras`);
  }

  salvarRegras(regras: RegrasComissao): Observable<RegrasComissao> {
    return this.http.put<RegrasComissao>(`${this.url}/regras`, regras);
  }

  /** Nulo (204) quando a loja nao cadastrou regras. */
  sugestao(vendedorId: number, veiculoId: number, valor: number): Observable<ComissaoSugerida | null> {
    return this.http.get<ComissaoSugerida | null>(`${this.url}/sugestao`, {
      params: { vendedorId: String(vendedorId), veiculoId: String(veiculoId), valor: String(valor) }
    });
  }

  extrato(inicio: string, fim: string, vendedorId?: number | null): Observable<ComissaoExtrato> {
    const params: Record<string, string> = { inicio, fim };
    if (vendedorId) params['vendedorId'] = String(vendedorId);
    return this.http.get<ComissaoExtrato>(`${this.url}/extrato`, { params });
  }

  pagar(vendaIds: number[], pagaEm: string | null): Observable<{ atualizadas: number }> {
    return this.http.post<{ atualizadas: number }>(`${this.url}/pagar`, { vendaIds, pagaEm });
  }
}
