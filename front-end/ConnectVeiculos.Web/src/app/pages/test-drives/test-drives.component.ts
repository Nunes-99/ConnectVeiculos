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

  abrirWhatsApp(td: TestDrive): void {
    if (td.tdrTelefone) {
      const phone = td.tdrTelefone.replace(/\D/g, '');
      const fullPhone = phone.startsWith('55') ? phone : '55' + phone;
      window.open(`https://wa.me/${fullPhone}`, '_blank');
    }
  }
}
