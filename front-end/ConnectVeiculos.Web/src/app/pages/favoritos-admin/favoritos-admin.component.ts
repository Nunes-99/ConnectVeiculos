import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FavoritoService, FavoritoRelatorio, FavoritoVisitante } from '../../core/services';

@Component({
  selector: 'app-favoritos-admin',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './favoritos-admin.component.html',
  styleUrl: './favoritos-admin.component.scss'
})
export class FavoritosAdminComponent implements OnInit {
  private favoritoService = inject(FavoritoService);

  relatorio: FavoritoRelatorio[] = [];
  visitantes: FavoritoVisitante[] = [];
  loading = false;
  showInfo = false;
  activeTab: 'veiculos' | 'visitantes' = 'veiculos';
  selectedVeiculoId: number | null = null;

  ngOnInit(): void {
    this.loadRelatorio();
  }

  loadRelatorio(): void {
    this.loading = true;
    this.favoritoService.relatorio().subscribe({
      next: (data) => { this.relatorio = data; this.loading = false; },
      error: () => this.loading = false
    });
  }

  loadVisitantes(veiculoId?: number): void {
    this.selectedVeiculoId = veiculoId || null;
    this.activeTab = 'visitantes';
    this.favoritoService.visitantes(veiculoId).subscribe({
      next: (data) => this.visitantes = data
    });
  }

  getStatusLabel(status: string): string {
    const labels: Record<string, string> = { 'D': 'Disponivel', 'V': 'Vendido', 'R': 'Reservado', 'I': 'Inativo' };
    return labels[status] || status;
  }

  abrirWhatsApp(v: FavoritoVisitante): void {
    if (v.telefone) {
      const phone = v.telefone.replace(/\D/g, '');
      const fullPhone = phone.startsWith('55') ? phone : '55' + phone;
      window.open(`https://wa.me/${fullPhone}`, '_blank');
    }
  }

  formatarPreco(valor: number): string {
    return valor.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  }
}
