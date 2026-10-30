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
  /** Mercado Pago configurado no servidor: sem isso, upgrade continua pelo WhatsApp. */
  pagamentoNoCartao: boolean;
  /** Plano pago por Pix vale até esta data (cada Pix soma 30 dias). */
  planoPagoAte: string | null;
  /** Até quando cancelar devolve o dinheiro (7 dias do pagamento). Null se já passou. */
  reembolsoAte: string | null;
  /** Assinatura mensal no cartão (status do Mercado Pago: authorized, paused, cancelled). */
  assinatura: { status: string; planoId: number | null; atualizadaEm: string | null } | null;
  uso: {
    veiculos: number;
    lojas: number;
    usuarios: number;
    leadsMes: number;
  };
}

export interface PixGerado {
  codigo: string;
  qrCodeBase64: string;
  valor: number;
  expiraEm: string;
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

  /** Cria a assinatura e devolve a página do Mercado Pago onde o cartão é cadastrado. */
  assinar(planoId: number, email: string): Observable<{ initPoint: string }> {
    return this.post<{ initPoint: string }>('assinatura/checkout', { planoId, email });
  }

  /** Pix de 30 dias: QR Code e copia e cola. O plano muda quando o pagamento for aprovado. */
  gerarPix(planoId: number, email: string): Observable<PixGerado> {
    return this.post<PixGerado>('assinatura/pix', { planoId, email });
  }

  cancelarAssinatura(): Observable<{ mensagem: string }> {
    return this.post<{ mensagem: string }>('assinatura/cancelar', {});
  }
}
