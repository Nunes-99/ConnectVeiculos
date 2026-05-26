import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';

export interface PlanoPublico {
  id: number;
  nome: string;
  preco: number;
  maxVeiculos: number | null;
  maxLojas: number | null;
  maxUsuarios: number | null;
  maxLeadsMes: number | null;
  ordem: number;
}

export interface MeuPlano {
  tenantId: number;
  tenantNome: string;
  emTrial: boolean;
  trialAte: string | null;
  diasRestantesTrial: number;
  plano: {
    id: number;
    nome: string;
    preco: number;
    maxVeiculos: number | null;
    maxLojas: number | null;
    maxUsuarios: number | null;
    maxLeadsMes: number | null;
  } | null;
  uso: {
    veiculos: number;
    lojas: number;
    usuarios: number;
    leadsMes: number;
  };
}

@Injectable({ providedIn: 'root' })
export class PlanoService extends ApiService {
  constructor(http: HttpClient) {
    super(http);
  }

  listarPublicos(): Observable<PlanoPublico[]> {
    return this.get<PlanoPublico[]>('plano');
  }

  meuPlano(): Observable<MeuPlano> {
    return this.get<MeuPlano>('plano/meu');
  }
}
