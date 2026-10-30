import { Component, inject, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService, PlanoService, MeuPlano, PlanoPublico, ToastService } from '../../core/services';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-plano',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './plano.component.html',
  styleUrl: './plano.component.scss'
})
export class PlanoComponent implements OnInit, OnDestroy {
  private planoService = inject(PlanoService);
  private toast = inject(ToastService);
  private auth = inject(AuthService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  meu: MeuPlano | null = null;
  planosDisponiveis: PlanoPublico[] = [];
  carregando = true;

  // Plano cujo card está pedindo o e-mail do pagamento.
  assinandoPlanoId: number | null = null;
  emailPagamento = '';
  enviando = false;
  confirmandoCancelamento = false;
  // Voltou do Mercado Pago: o plano muda quando o aviso deles chegar.
  aguardandoConfirmacao = false;
  private consultas?: ReturnType<typeof setInterval>;

  ngOnInit(): void {
    this.emailPagamento = this.auth.getUser()?.usuEmail || '';
    this.carregar();

    if (this.route.snapshot.queryParamMap.get('assinatura') === 'retorno') {
      this.router.navigate([], { queryParams: {}, replaceUrl: true });
      this.aguardarConfirmacao();
    }
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

  ngOnDestroy(): void {
    if (this.consultas) clearInterval(this.consultas);
  }

  private carregar(depois?: (m: MeuPlano) => void): void {
    this.planoService.meuPlano().subscribe({
      next: (m) => { this.meu = m; this.carregando = false; depois?.(m); },
      error: () => { this.carregando = false; this.toast.error('Falha ao carregar plano.'); }
    });
  }

  get assinaturaAtiva(): boolean {
    return this.meu?.assinatura?.status === 'authorized';
  }

  // Consulta por até 2 minutos: o Mercado Pago costuma avisar em segundos.
  private aguardarConfirmacao(): void {
    this.aguardandoConfirmacao = true;
    let tentativas = 0;
    this.consultas = setInterval(() => {
      tentativas++;
      this.carregar((m) => {
        if (m.assinatura?.status === 'authorized' || tentativas >= 24) {
          clearInterval(this.consultas);
          this.aguardandoConfirmacao = false;
          if (m.assinatura?.status === 'authorized')
            this.toast.success(`Pagamento confirmado! Sua loja está no plano ${m.plano?.nome}.`);
        }
      });
    }, 5000);
  }

  abrirAssinatura(p: PlanoPublico): void {
    this.assinandoPlanoId = p.id;
    this.confirmandoCancelamento = false;
  }

  assinar(p: PlanoPublico): void {
    if (this.enviando) return;
    this.enviando = true;
    this.planoService.assinar(p.id, this.emailPagamento).subscribe({
      // O cartão é cadastrado na página do Mercado Pago, que devolve para /plano.
      next: (r) => window.location.href = r.initPoint,
      // A mensagem sai pelo httpErrorInterceptor.
      error: () => this.enviando = false
    });
  }

  cancelarAssinatura(): void {
    if (this.enviando) return;
    this.enviando = true;
    this.planoService.cancelarAssinatura().subscribe({
      next: (r) => {
        this.enviando = false;
        this.confirmandoCancelamento = false;
        this.toast.success(r.mensagem);
        this.carregar();
      },
      error: () => this.enviando = false
    });
  }

  contatarUpgrade(planoNome: string): void {
    const tel = environment.suporteWhatsApp;
    const msg = encodeURIComponent(
      `Olá! Sou ${this.meu?.tenantNome} e quero fazer upgrade para o plano ${planoNome}.`);
    window.open(`https://wa.me/${tel}?text=${msg}`, '_blank');
  }
}
