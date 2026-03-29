import { Component, OnInit, OnDestroy, inject, HostListener, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { SignalRService, Notificacao, AuthService } from '../../../core/services';

interface NotificacaoUI extends Notificacao {
  id: number;
  lida: boolean;
  titulo: string;
  mensagem: string;
}

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './notifications.component.html',
  styleUrl: './notifications.component.scss'
})
export class NotificationsComponent implements OnInit, OnDestroy {
  private signalRService = inject(SignalRService);
  private authService = inject(AuthService);
  private elementRef = inject(ElementRef);
  private subscription: Subscription | null = null;
  private statusSubscription: Subscription | null = null;
  private idCounter = 0;

  notificacoes: NotificacaoUI[] = [];
  showDropdown = false;
  conectado = false;

  get naoLidas(): number {
    return this.notificacoes.filter(n => !n.lida).length;
  }

  ngOnInit(): void {
    if (this.authService.isAuthenticated()) {
      this.signalRService.iniciarConexao();

      this.subscription = this.signalRService.notificacoes$.subscribe(notificacao => {
        this.adicionarNotificacao(notificacao);
      });

      this.statusSubscription = this.signalRService.connectionStatus$.subscribe(status => {
        this.conectado = status;
      });
    }
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
    this.statusSubscription?.unsubscribe();
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event): void {
    if (!this.elementRef.nativeElement.contains(event.target)) {
      this.showDropdown = false;
    }
  }

  toggleDropdown(): void {
    this.showDropdown = !this.showDropdown;
  }

  marcarComoLida(notificacao: NotificacaoUI): void {
    notificacao.lida = true;
  }

  marcarTodasComoLidas(): void {
    this.notificacoes.forEach(n => n.lida = true);
  }

  limparNotificacoes(): void {
    this.notificacoes = [];
    this.showDropdown = false;
  }

  removerNotificacao(id: number): void {
    this.notificacoes = this.notificacoes.filter(n => n.id !== id);
  }

  private adicionarNotificacao(notificacao: Notificacao): void {
    const { titulo, mensagem } = this.processarNotificacao(notificacao);

    const notificacaoUI: NotificacaoUI = {
      ...notificacao,
      id: ++this.idCounter,
      lida: false,
      titulo,
      mensagem,
      timestamp: new Date(notificacao.timestamp)
    };

    this.notificacoes.unshift(notificacaoUI);

    // Limitar a 50 notificacoes
    if (this.notificacoes.length > 50) {
      this.notificacoes = this.notificacoes.slice(0, 50);
    }
  }

  private processarNotificacao(notificacao: Notificacao): { titulo: string; mensagem: string } {
    switch (notificacao.tipo) {
      case 'NOVA_VENDA':
        return {
          titulo: 'Nova Venda',
          mensagem: `Venda registrada: ${notificacao.dados.veiculoNome || 'Veiculo'}`
        };
      case 'NOVO_VEICULO':
        return {
          titulo: 'Novo Veiculo',
          mensagem: `Veiculo cadastrado: ${notificacao.dados.marca || ''} ${notificacao.dados.modelo || ''}`
        };
      case 'VEICULO_RESERVADO':
        return {
          titulo: 'Veiculo Reservado',
          mensagem: `${notificacao.dados.marca || ''} ${notificacao.dados.modelo || ''} foi reservado`
        };
      case 'ESTORNO_VENDA':
        return {
          titulo: 'Venda Estornada',
          mensagem: `Venda estornada: ${notificacao.dados.veiculoNome || 'Veiculo'}`
        };
      default:
        return {
          titulo: 'Notificacao',
          mensagem: notificacao.dados?.mensagem || 'Nova notificacao recebida'
        };
    }
  }

  formatarTempo(timestamp: Date): string {
    const agora = new Date();
    const diff = agora.getTime() - new Date(timestamp).getTime();
    const minutos = Math.floor(diff / 60000);
    const horas = Math.floor(diff / 3600000);
    const dias = Math.floor(diff / 86400000);

    if (minutos < 1) return 'Agora';
    if (minutos < 60) return `${minutos}m atras`;
    if (horas < 24) return `${horas}h atras`;
    return `${dias}d atras`;
  }

  getIcone(tipo: string): string {
    switch (tipo) {
      case 'NOVA_VENDA': return 'sell';
      case 'NOVO_VEICULO': return 'directions_car';
      case 'VEICULO_RESERVADO': return 'bookmark';
      case 'ESTORNO_VENDA': return 'undo';
      default: return 'notifications';
    }
  }
}
