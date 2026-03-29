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

    const token = this.authService.getToken();
    if (!token) {
      console.warn('SignalR: Token nao encontrado, conexao nao estabelecida');
      return;
    }

    const hubUrl = `${environment.apiUrl.replace('/api', '')}/hubs/notificacoes`;

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => token
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
