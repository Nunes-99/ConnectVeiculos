import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TestDriveService, TestDrive, ToastService } from '../../core/services';
import { TelefonePipe } from '../../shared/pipes';

@Component({
  selector: 'app-test-drives',
  standalone: true,
  imports: [CommonModule, FormsModule, TelefonePipe],
  templateUrl: './test-drives.component.html',
  styleUrl: './test-drives.component.scss'
})
export class TestDrivesComponent implements OnInit {
  private testDriveService = inject(TestDriveService);
  private toast = inject(ToastService);

  testDrives: TestDrive[] = [];
  loading = false;
  showInfo = false;
  filtroStatus = '';

  /**
   * Remarcacao. Antes disso a unica saida era cancelar e criar outro, o que
   * dispara um cancelamento para um cliente que so pediu para mudar de horario.
   */
  tdRemarcando: TestDrive | null = null;
  novaData = '';
  novoHorario = '';
  remarcando = false;

  /** O input de data nao aceita dia anterior a hoje. */
  get hoje(): string {
    return new Date().toISOString().slice(0, 10);
  }

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading = true;
    this.testDriveService.listar(undefined, this.filtroStatus || undefined).subscribe({
      next: (data) => {
        this.testDrives = data;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  atualizarStatus(id: number, status: string): void {
    this.testDriveService.atualizarStatus(id, status).subscribe({
      next: (resp: any) => {
        this.loadData();
        const n = resp?.notificacao;
        const acao = status === 'C' ? 'confirmado' : status === 'X' ? 'cancelado' : 'atualizado';
        if (!n || !n.aplicavel) {
          this.toast?.success(`Test drive ${acao}.`);
          return;
        }
        if (n.enviada) {
          this.toast?.success(`Test drive ${acao} — cliente notificado via WhatsApp.`);
        } else if (n.motivo === 'nao-configurado') {
          this.toast?.warning(`Test drive ${acao}. WhatsApp não integrado — só o status interno foi atualizado. Configure em /integracoes para enviar mensagens automáticas.`);
        } else if (n.motivo === 'sem-telefone') {
          this.toast?.warning(`Test drive ${acao}. Cliente sem WhatsApp cadastrado — contate manualmente.`);
        } else if (n.motivo === 'falha-envio') {
          this.toast?.warning(`Test drive ${acao}. Falha ao enviar WhatsApp — confira se os templates estão aprovados no Meta Business. Contate o cliente manualmente.`);
        } else {
          this.toast?.success(`Test drive ${acao}.`);
        }
      },
      error: () => this.toast?.error('Erro ao atualizar status.')
    });
  }

  abrirRemarcar(td: TestDrive): void {
    this.tdRemarcando = td;
    this.novaData = (td.tdrDataAgendamento || '').slice(0, 10);
    this.novoHorario = td.tdrHorario || '';
  }

  fecharRemarcar(): void {
    if (this.remarcando) return;
    this.tdRemarcando = null;
    this.novaData = '';
    this.novoHorario = '';
  }

  confirmarRemarcar(): void {
    const td = this.tdRemarcando;
    if (!td || !this.novaData || this.remarcando) return;

    this.remarcando = true;
    this.testDriveService.reagendar(td.tdrId, this.novaData, this.novoHorario).subscribe({
      next: (resp: any) => {
        this.remarcando = false;
        this.fecharRemarcar();
        this.loadData();

        const n = resp?.notificacao;
        if (!n || !n.aplicavel) {
          this.toast?.success('Test drive remarcado.');
        } else if (n.enviada) {
          this.toast?.success('Test drive remarcado — cliente avisado via WhatsApp.');
        } else if (n.motivo === 'nao-configurado') {
          this.toast?.warning('Test drive remarcado. WhatsApp não integrado — avise o cliente da nova data.');
        } else if (n.motivo === 'sem-telefone') {
          this.toast?.warning('Test drive remarcado. Cliente sem WhatsApp cadastrado — avise da nova data.');
        } else {
          this.toast?.warning('Test drive remarcado, mas o aviso no WhatsApp falhou — avise o cliente da nova data.');
        }
      },
      error: (err) => {
        this.remarcando = false;
        const msg = typeof err?.error === 'string' ? err.error : 'Não foi possível remarcar.';
        this.toast?.error(msg);
      }
    });
  }

  getStatusLabel(status: string): string {
    const labels: Record<string, string> = {
      'P': 'Pendente',
      'C': 'Confirmado',
      'R': 'Realizado',
      'X': 'Cancelado'
    };
    return labels[status] || status;
  }

  getCountByStatus(status: string): number {
    return this.testDrives.filter(td => td.tdrStatus === status).length;
  }

  isWhatsApp(telefone: string): boolean {
    if (!telefone) return false;
    const digits = telefone.replace(/\D/g, '');
    return digits.length >= 11;
  }

  ligar(td: TestDrive): void {
    if (td.tdrTelefone) {
      window.open(`tel:${td.tdrTelefone}`, '_self');
    }
  }

  abrirWhatsApp(td: TestDrive): void {
    const numero = td.tdrWhatsApp || td.tdrTelefone;
    if (numero) {
      const phone = numero.replace(/\D/g, '');
      const fullPhone = phone.startsWith('55') ? phone : '55' + phone;
      const veiculo = td.veiculoNome || 'veículo';
      const msg = encodeURIComponent(`Olá ${td.tdrNomeCliente}! Sobre o test drive do ${veiculo} agendado para ${td.tdrHorario || ''}, gostaria de confirmar.`);
      window.open(`https://wa.me/${fullPhone}?text=${msg}`, '_blank');
    }
  }
}
