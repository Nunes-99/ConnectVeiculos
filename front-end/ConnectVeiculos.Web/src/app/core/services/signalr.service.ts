import { Injectable, inject } from '@angular/core';
import { Subject, Observable } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';

export interface Notificacao {
  tipo: string;
  dados: any;
  timestamp: Date;
}

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private authService = inject(AuthService);
  private hubConnection: signalR.HubConnection | null = null;
  private notificacaoSubject = new Subject<Notificacao>();
  private connectionStatusSubject = new Subject<boolean>();

  notificacoes$: Observable<Notificacao> = this.notificacaoSubject.asObservable();
  connectionStatus$: Observable<boolean> = this.connectionStatusSubject.asObservable();

  iniciarConexao(): void {
    if (this.hubConnection) {
      return;
    }

    // Verificacao inicial — se nem token tem na primeira vez, evita
    // tentar conectar e logar erro feio.
    if (!this.authService.getToken()) {
      console.warn('SignalR: Token nao encontrado, conexao nao estabelecida');
      return;
    }

    // SignalR JS client nao passa pelo HttpInterceptor do Angular, entao o
     // header X-Tenant-Slug nao chega ao backend. Em dev (localhost) o middleware
     // resolveria para tenant "default" e o JWT (que carrega tenant_id real)
     // seria rejeitado pelo cross-tenant check com 401. O override ?tenant=slug
     // na query string e aceito pelo TenantResolutionMiddleware.
     const tenantSlug = this.authService.getTenantSlug();
     const tenantQuery = tenantSlug ? `?tenant=${encodeURIComponent(tenantSlug)}` : '';
     const hubUrl = `${environment.apiUrl.replace('/api', '')}/hubs/notificacoes${tenantQuery}`;

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        // IMPORTANTE: factory e chamado a CADA tentativa de conexao/reconexao
        // pelo SignalR — precisa ler do storage toda vez para pegar o token
        // renovado via refresh_token. Capturar token em variavel local aqui
        // congelava o valor da primeira chamada, e quando o JWT expirava (8h)
        // todas as reconexoes recebiam 401 eternamente.
        accessTokenFactory: () => this.authService.getToken() || ''
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this.registrarEventos();
    this.conectar();
  }

  private registrarEventos(): void {
    if (!this.hubConnection) return;

    this.hubConnection.on('ReceberNotificacao', (notificacao: Notificacao) => {
      this.notificacaoSubject.next(notificacao);
    });

    this.hubConnection.onreconnecting(() => {
      console.log('SignalR: Reconectando...');
      this.connectionStatusSubject.next(false);
    });

    this.hubConnection.onreconnected(() => {
      console.log('SignalR: Reconectado');
      this.connectionStatusSubject.next(true);
    });

    this.hubConnection.onclose(() => {
      console.log('SignalR: Conexao fechada');
      this.connectionStatusSubject.next(false);
    });
  }

  private async conectar(): Promise<void> {
    if (!this.hubConnection) return;

    try {
      await this.hubConnection.start();
      console.log('SignalR: Conectado com sucesso');
      this.connectionStatusSubject.next(true);
    } catch (error) {
      console.error('SignalR: Erro ao conectar', error);
      this.connectionStatusSubject.next(false);
      // Tentar reconectar apos 5 segundos
      setTimeout(() => this.conectar(), 5000);
    }
  }

  async entrarGrupo(grupoNome: string): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      await this.hubConnection.invoke('JoinGroup', grupoNome);
    }
  }

  async sairGrupo(grupoNome: string): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      await this.hubConnection.invoke('LeaveGroup', grupoNome);
    }
  }

  pararConexao(): void {
    if (this.hubConnection) {
      this.hubConnection.stop();
      this.hubConnection = null;
      this.connectionStatusSubject.next(false);
    }
  }

  isConectado(): boolean {
    return this.hubConnection?.state === signalR.HubConnectionState.Connected;
  }
}
