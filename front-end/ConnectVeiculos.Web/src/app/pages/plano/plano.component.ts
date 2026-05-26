import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PlanoService, MeuPlano, PlanoPublico, ToastService } from '../../core/services';

@Component({
  selector: 'app-plano',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './plano.component.html',
  styleUrl: './plano.component.scss'
})
export class PlanoComponent implements OnInit {
  private planoService = inject(PlanoService);
  private toast = inject(ToastService);

  meu: MeuPlano | null = null;
  planosDisponiveis: PlanoPublico[] = [];
  carregando = true;

  ngOnInit(): void {
    this.planoService.meuPlano().subscribe({
      next: (m) => { this.meu = m; this.carregando = false; },
      error: () => { this.carregando = false; this.toast.error('Falha ao carregar plano.'); }
    });
    this.planoService.listarPublicos().subscribe({
      next: (lista) => this.planosDisponiveis = lista,
      error: () => {}
    });
  }

  porcentagem(atual: number, max: number | null | undefined): number {
    if (max === null || max === undefined) return 0;
    if (max === 0) return 100;
    return Math.min(100, Math.round((atual / max) * 100));
  }

  classeBarra(atual: number, max: number | null | undefined): string {
    if (max === null || max === undefined) return 'ilimitado';
    const pct = this.porcentagem(atual, max);
    if (pct >= 90) return 'critico';
    if (pct >= 70) return 'aviso';
    return 'ok';
  }

  formatarLimite(max: number | null | undefined): string {
    return max === null || max === undefined ? '∞' : String(max);
  }

  contatarUpgrade(planoNome: string): void {
    const tel = '5511999999999'; // TODO: configurar telefone real do suporte
    const msg = encodeURIComponent(
      `Olá! Sou ${this.meu?.tenantNome} e quero fazer upgrade para o plano ${planoNome}.`);
    window.open(`https://wa.me/${tel}?text=${msg}`, '_blank');
  }
}
