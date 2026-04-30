import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LeadService, Lead } from '../../core/services';

@Component({
  selector: 'app-leads',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './leads.component.html',
  styleUrl: './leads.component.scss'
})
export class LeadsComponent implements OnInit {
  private leadService = inject(LeadService);

  leads: Lead[] = [];
  loading = false;
  showInfo = false;
  filtroStatus = '';
  filtroOrigem = '';

  ngOnInit(): void {
    this.loadData();
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
    if (lead.leaTelefone) {
      const phone = lead.leaTelefone.replace(/\D/g, '');
      const fullPhone = phone.startsWith('55') ? phone : '55' + phone;
      window.open(`https://wa.me/${fullPhone}`, '_blank');
    }
  }

  getCountByStatus(status: string): number {
    return this.leads.filter(l => l.leaStatus === status).length;
  }
}
