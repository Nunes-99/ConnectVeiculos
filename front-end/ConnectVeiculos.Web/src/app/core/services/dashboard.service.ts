import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { Dashboard, VendasPorPeriodo, FaturamentoMensal, TopVeiculosVendidos, ComparativoMensal } from '../models';

@Injectable({
  providedIn: 'root'
})
export class DashboardService extends ApiService {
  getDashboard(): Observable<Dashboard> {
    return this.get<Dashboard>('dashboard');
  }

  getVendasPorPeriodo(dataInicio?: Date, dataFim?: Date): Observable<VendasPorPeriodo> {
    let params: any = {};
    if (dataInicio) params.dataInicio = dataInicio.toISOString().split('T')[0];
    if (dataFim) params.dataFim = dataFim.toISOString().split('T')[0];
    return this.get<VendasPorPeriodo>('dashboard/vendas-periodo', params);
  }

  getFaturamentoMensal(ano?: number): Observable<FaturamentoMensal> {
    const params: any = ano ? { ano: ano.toString() } : undefined;
    return this.get<FaturamentoMensal>('dashboard/faturamento-mensal', params);
  }

  getTopVeiculos(quantidade?: number): Observable<TopVeiculosVendidos> {
    const params: any = quantidade ? { quantidade: quantidade.toString() } : undefined;
    return this.get<TopVeiculosVendidos>('dashboard/top-veiculos', params);
  }

  getComparativoMensal(): Observable<ComparativoMensal> {
    return this.get<ComparativoMensal>('dashboard/comparativo-mensal');
  }
}
