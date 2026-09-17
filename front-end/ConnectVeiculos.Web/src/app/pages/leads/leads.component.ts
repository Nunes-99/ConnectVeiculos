import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LeadService, Lead, IntegracaoService, ToastService, SignalRService } from '../../core/services';
import { TelefonePipe } from '../../shared/pipes';

@Component({
  selector: 'app-leads',
  standalone: true,
  imports: [CommonModule, FormsModule, TelefonePipe],
  templateUrl: './leads.component.html',
  styleUrl: './leads.component.scss'
})
export class LeadsComponent implements OnInit {
  private leadService = inject(LeadService);
  private integracaoService = inject(IntegracaoService);
  private toast = inject(ToastService);
  private signalR = inject(SignalRService);
  private destroyRef = inject(DestroyRef);

  leads: Lead[] = [];
  loading = false;
  showInfo = false;
  filtroStatus = '';
  filtroOrigem = '';

  /**
   * Com a API do WhatsApp configurada, responder acontece aqui dentro e a
   * conversa fica registrada. Sem ela, o botao continua abrindo o wa.me — que
   * e' o comportamento util para a loja que nao tem (nem precisa ter) a conta
   * WhatsApp Business API.
   */
  whatsAppApiAtiva = false;
  leadRespondendo: Lead | null = null;
  mensagemResposta = '';
  enviandoResposta = false;

  ngOnInit(): void {
    this.loadData();

    // Lead que chega pelo WhatsApp aparece sozinho, sem recarregar. O backend
    // ja emitia LEAD_WHATSAPP pelo SignalR desde sempre; faltava alguem escutar
    // — a tela nunca se inscreveu, e o aviso nunca chegava a lugar nenhum.
    //
    // Recarrega a lista em vez de inserir o lead vindo do evento: os cartoes de
    // contagem no topo e os filtros de status e origem que estao aplicados
    // continuam corretos, sem precisar reproduzir essa logica aqui.
    this.signalR.notificacoes$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(notificacao => {
        if (notificacao?.tipo !== 'LEAD_WHATSAPP') return;

        const nome = notificacao.dados?.nome;
        this.toast.info(nome ? `Novo lead no WhatsApp: ${nome}` : 'Novo lead no WhatsApp.');
        this.loadData();
      });

    this.integracaoService.getWhatsAppStatus().subscribe({
      next: (s) => this.whatsAppApiAtiva = s?.configurado === true,
      error: () => this.whatsAppApiAtiva = false
    });
  }

  fecharResposta(): void {
    if (this.enviandoResposta) return;
    this.leadRespondendo = null;
    this.mensagemResposta = '';
  }

  enviarResposta(): void {
    const lead = this.leadRespondendo;
    const texto = this.mensagemResposta.trim();
    if (!lead?.leaTelefone || !texto || this.enviandoResposta) return;

    this.enviandoResposta = true;
    this.integracaoService.enviarWhatsApp({
      telefone: this.normalizarTelefone(lead.leaTelefone),
      mensagem: texto
    }).subscribe({
      next: (r) => {
        this.toast.success(r?.mensagem ?? 'Mensagem enviada.');
        this.enviandoResposta = false;
        this.fecharResposta();
      },
      error: (err) => {
        const msg = err?.error?.message ?? err?.error?.mensagem;
        this.toast.error(typeof msg === 'string' ? msg : 'Não foi possível enviar a mensagem.');
        this.enviandoResposta = false;
      }
    });
  }

  /**
   * A Meta exige E.164 sem o "+". O telefone do lead pode vir com mascara, e
   * quem chega pelo webhook ja vem com o codigo do pais — por isso so
   * acrescenta o 55 quando ele nao esta la.
   */
  private normalizarTelefone(telefone: string): string {
    const digitos = telefone.replace(/\D/g, '');
    return digitos.startsWith('55') ? digitos : '55' + digitos;
  }

  loadData(): void {
    this.loading = true;
    this.leadService.listar(
      undefined,
      this.filtroStatus || undefined,
      this.filtroOrigem || undefined
    ).subscribe({
      next: (data) => {
        this.leads = data;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  atualizarStatus(id: number, status: string): void {
    this.leadService.atualizarStatus(id, status).subscribe({
      next: () => this.loadData()
    });
  }

  getOrigemLabel(origem: string): string {
    const labels: Record<string, string> = {
      'WHATSAPP_CATALOGO': 'WhatsApp Catálogo',
      'WHATSAPP_DETALHE': 'WhatsApp Detalhe',
      'TEST_DRIVE': 'Test Drive',
      'DIRETO': 'Direto',
      'INDICACAO': 'Indicação',
      'FINANCIAMENTO': 'Solicitação de Financiamento'
    };
    return labels[origem] || origem;
  }

  ligar(lead: Lead): void {
    if (lead.leaTelefone) {
      window.open(`tel:${lead.leaTelefone}`, '_self');
    }
  }

  abrirWhatsApp(lead: Lead): void {
    if (!lead.leaTelefone) return;

    if (this.whatsAppApiAtiva) {
      this.leadRespondendo = lead;
      this.mensagemResposta = '';
      return;
    }

    window.open(`https://wa.me/${this.normalizarTelefone(lead.leaTelefone)}`, '_blank');
  }

  getCountByStatus(status: string): number {
    return this.leads.filter(l => l.leaStatus === status).length;
  }
}
