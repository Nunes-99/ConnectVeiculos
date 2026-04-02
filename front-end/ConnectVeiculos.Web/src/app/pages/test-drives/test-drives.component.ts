import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TestDriveService, TestDrive } from '../../core/services';

@Component({
  selector: 'app-test-drives',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './test-drives.component.html',
  styleUrl: './test-drives.component.scss'
})
export class TestDrivesComponent implements OnInit {
  private testDriveService = inject(TestDriveService);

  testDrives: TestDrive[] = [];
  loading = false;
  showInfo = false;
  filtroStatus = '';

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
      next: () => this.loadData()
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
