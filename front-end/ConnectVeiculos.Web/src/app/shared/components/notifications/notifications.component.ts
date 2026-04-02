import { Component, OnInit, OnDestroy, inject, HostListener, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { SignalRService, Notificacao, AuthService, NotificacaoService, NotificacaoDB } from '../../../core/services';

interface NotificacaoUI {
  id: number;
  dbId: number | null;
  lida: boolean;
  titulo: string;
  mensagem: string;
  tipo: string;
  timestamp: Date;
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
  private notificacaoService = inject(NotificacaoService);
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
      this.carregarNotificacoes();

      this.signalRService.iniciarConexao();

      this.subscription = this.signalRService.notificacoes$.subscribe(notificacao => {
        this.adicionarNotificacao(notificacao);
        // Recarrega do backend para ter o ID persistido
        setTimeout(() => this.carregarNotificacoes(), 1000);
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
    if (notificacao.dbId) {
      this.notificacaoService.marcarComoLida(notificacao.dbId).subscribe();
    }
  }

  marcarTodasComoLidas(): void {
    this.notificacoes.forEach(n => n.lida = true);
    this.notificacaoService.marcarTodasComoLidas().subscribe();
  }

  limparNotificacoes(): void {
    this.marcarTodasComoLidas();
    this.notificacoes = [];
    this.showDropdown = false;
  }

  removerNotificacao(id: number): void {
    const notificacao = this.notificacoes.find(n => n.id === id);
    if (notificacao && !notificacao.lida && notificacao.dbId) {
      this.notificacaoService.marcarComoLida(notificacao.dbId).subscribe();
    }
    this.notificacoes = this.notificacoes.filter(n => n.id !== id);
  }

  private carregarNotificacoes(): void {
    this.notificacaoService.listar().subscribe({
      next: (notificacoes) => {
        this.notificacoes = notificacoes.map(n => this.mapearNotificacaoDB(n));
      }
    });
  }

  private mapearNotificacaoDB(n: NotificacaoDB): NotificacaoUI {
    return {
      id: ++this.idCounter,
      dbId: n.notId,
      lida: n.notLida,
      titulo: n.notTitulo,
      mensagem: n.notMensagem,
      tipo: n.notTipo || 'Sistema',
      timestamp: new Date(n.notCriadaEm)
    };
  }

  private adicionarNotificacao(notificacao: Notificacao): void {
    const { titulo, mensagem } = this.processarNotificacao(notificacao);

    const notificacaoUI: NotificacaoUI = {
      id: ++this.idCounter,
      dbId: notificacao.dados?.notificacaoId || null,
      lida: false,
      titulo,
      mensagem,
      tipo: notificacao.tipo,
      timestamp: new Date(notificacao.timestamp)
    };

    this.notificacoes.unshift(notificacaoUI);

    if (this.notificacoes.length > 50) {
      this.notificacoes = this.notificacoes.slice(0, 50);
    }
  }

  private processarNotificacao(notificacao: Notificacao): { titulo: string; mensagem: string } {
    switch (notificacao.tipo) {
      case 'NOVA_VENDA':
        return {
          titulo: 'Nova Venda',
          mensagem: `Venda registrada: ${notificacao.dados.veiculoNome || 'Veículo'}`
        };
      case 'NOVO_VEICULO':
        return {
          titulo: 'Novo Veículo',
          mensagem: `Veículo cadastrado: ${notificacao.dados.marca || ''} ${notificacao.dados.modelo || ''}`
        };
      case 'VEICULO_RESERVADO':
        return {
          titulo: 'Veículo Reservado',
          mensagem: `${notificacao.dados.marca || ''} ${notificacao.dados.modelo || ''} foi reservado`
        };
      case 'ESTORNO_VENDA':
        return {
          titulo: 'Venda Estornada',
          mensagem: `Venda estornada: ${notificacao.dados.veiculoNome || 'Veículo'}`
        };
      default:
        return {
          titulo: 'Notificação',
          mensagem: notificacao.dados?.mensagem || 'Nova notificação recebida'
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
    if (minutos < 60) return `${minutos}m atrás`;
    if (horas < 24) return `${horas}h atrás`;
    return `${dias}d atrás`;
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
