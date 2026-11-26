import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService, ToastService } from '../../core/services';
import { PixGerado } from '../../core/services/plano.service';
import {
  ConsultaItem, ConsultaPlacaPainel, ConsultaPlacaService, ServicoConsulta
} from '../../core/services/consulta-placa.service';

/**
 * Consulta veicular por placa (ZapCar), paga com os creditos da loja:
 * gravame, leilao, sinistro, roubo e furto, restricoes e debitos antes de
 * comprar ou pegar um carro na troca. A gestao compra creditos por Pix.
 */
@Component({
  selector: 'app-consulta-placa',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './consulta-placa.component.html',
  styleUrl: './consulta-placa.component.scss'
})
export class ConsultaPlacaComponent implements OnInit, OnDestroy {
  private api = inject(ConsultaPlacaService);
  private auth = inject(AuthService);
  private toast = inject(ToastService);

  readonly placasTeste = [
    { placa: 'TST0001', texto: 'nada consta' },
    { placa: 'TST0002', texto: 'gravame' },
    { placa: 'TST0003', texto: 'RENAJUD' },
    { placa: 'TST0004', texto: 'débitos' },
    { placa: 'TST0005', texto: 'sinistro e leilão' },
    { placa: 'TST0404', texto: 'não encontrada' }
  ];

  painel: ConsultaPlacaPainel | null = null;
  carregando = true;

  placa = '';
  servico = 'consulta-completa';
  consultando = false;
  atual: ConsultaItem | null = null;
  baixando = false;

  mostrarCompra = false;
  pacote: number | null = null;
  email = '';
  gerandoPix = false;
  pix: PixGerado | null = null;
  mostrarExtrato = false;

  private timerConsulta: ReturnType<typeof setInterval> | null = null;
  private timerPix: ReturnType<typeof setInterval> | null = null;

  get gestao(): boolean { return this.auth.isGerente(); }
  get podeConsultar(): boolean { return this.auth.isVendedor(); }

  get escolhido(): ServicoConsulta | undefined {
    return this.painel?.servicos.find(s => s.id === this.servico);
  }

  get semSaldo(): boolean {
    return !!this.painel && !!this.escolhido && this.painel.saldo < this.escolhido.preco;
  }

  get placaNormalizada(): string {
    return (this.placa || '').toUpperCase().replace(/[^A-Z0-9]/g, '');
  }

  get placaValida(): boolean {
    return /^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$/.test(this.placaNormalizada);
  }

  ngOnInit(): void {
    this.email = this.auth.getUser()?.usuEmail || '';
    this.carregar();
  }

  ngOnDestroy(): void {
    this.pararConsulta();
    this.pararPix();
  }

  carregar(): void {
    this.api.painel().subscribe({
      next: p => {
        this.painel = p;
        this.carregando = false;
        if (!this.pacote && p.pacotes.length) this.pacote = p.pacotes[Math.min(1, p.pacotes.length - 1)];
      },
      error: err => {
        this.carregando = false;
        this.toast.error(err?.error?.error || 'Não foi possível carregar a consulta por placa.');
      }
    });
  }

  consultar(): void {
    if (!this.placaValida || !this.escolhido || this.consultando) return;
    this.consultando = true;
    this.api.consultar(this.placaNormalizada, this.servico).subscribe({
      next: c => {
        this.consultando = false;
        this.atual = c;
        this.placa = '';
        this.carregar();
        this.acompanhar(c);
      },
      error: err => {
        this.consultando = false;
        this.toast.error(err?.error?.error || 'Não foi possível fazer a consulta.');
        this.carregar();
      }
    });
  }

  abrir(c: ConsultaItem): void {
    this.atual = c;
    this.acompanhar(c);
    setTimeout(() => document.getElementById('resultado')?.scrollIntoView({ behavior: 'smooth', block: 'start' }));
  }

  /** Pergunta a cada 4s ate concluir (a base costuma levar poucos minutos). */
  private acompanhar(c: ConsultaItem): void {
    this.pararConsulta();
    if (c.status !== 'PROCESSANDO') return;
    const inicio = Date.now();
    this.timerConsulta = setInterval(() => {
      if (Date.now() - inicio > 10 * 60 * 1000) { this.pararConsulta(); return; }
      this.api.obter(c.id).subscribe({
        next: r => {
          if (this.atual?.id === r.id) this.atual = r;
          if (r.status !== 'PROCESSANDO') {
            this.pararConsulta();
            this.carregar();
            if (r.status === 'ERRO') this.toast.error(r.erro || 'A consulta não teve resultado. O valor voltou para os créditos.');
          }
        },
        error: () => {}
      });
    }, 4000);
  }

  private pararConsulta(): void {
    if (this.timerConsulta) clearInterval(this.timerConsulta);
    this.timerConsulta = null;
  }

  baixarDocumento(c: ConsultaItem): void {
    this.baixando = true;
    this.api.documento(c.id).subscribe({
      next: blob => {
        this.baixando = false;
        const url = URL.createObjectURL(blob);
        window.open(url, '_blank');
        setTimeout(() => URL.revokeObjectURL(url), 60000);
      },
      error: () => {
        this.baixando = false;
        this.toast.error(c.teste ? 'No ambiente de teste não há laudo em PDF.' : 'O laudo desta consulta não está disponível.');
      }
    });
  }

  gerarPix(): void {
    if (!this.pacote || !this.email || this.gerandoPix) return;
    this.gerandoPix = true;
    const saldoAntes = this.painel?.saldo ?? 0;
    this.api.comprarCreditos(this.pacote, this.email).subscribe({
      next: p => {
        this.gerandoPix = false;
        this.pix = p;
        this.esperarPagamento(saldoAntes);
      },
      error: err => {
        this.gerandoPix = false;
        this.toast.error(err?.error?.error || 'Não foi possível gerar o Pix.');
      }
    });
  }

  /** Confere o saldo a cada 5s: o credito entra quando o Mercado Pago avisa. */
  private esperarPagamento(saldoAntes: number): void {
    this.pararPix();
    this.timerPix = setInterval(() => {
      this.api.painel().subscribe({
        next: p => {
          this.painel = p;
          if (p.saldo > saldoAntes) {
            this.pararPix();
            this.pix = null;
            this.mostrarCompra = false;
            this.toast.success('Pix recebido: créditos liberados.');
          }
        },
        error: () => {}
      });
    }, 5000);
  }

  private pararPix(): void {
    if (this.timerPix) clearInterval(this.timerPix);
    this.timerPix = null;
  }

  fecharPix(): void {
    this.pararPix();
    this.pix = null;
  }

  copiarPix(): void {
    if (!this.pix) return;
    navigator.clipboard?.writeText(this.pix.codigo).then(
      () => this.toast.success('Código Pix copiado.'),
      () => this.toast.error('Não consegui copiar. Selecione o código e copie.')
    );
  }

  usarPlacaTeste(placa: string): void {
    this.placa = placa;
  }

  reais(centavos: number | null | undefined): number {
    return (centavos ?? 0) / 100;
  }

  nomeDebito(tipo: string): string {
    return ({ IPVA: 'IPVA', LICENCIAMENTO: 'Licenciamento', DPVAT: 'DPVAT', MULTA: 'Multa', OUTRO: 'Outro' } as Record<string, string>)[tipo] || tipo;
  }

  nomeMovimento(tipo: string): string {
    return ({ COMPRA: 'Compra', CONSUMO: 'Consulta', ESTORNO: 'Devolução', AJUSTE: 'Ajuste' } as Record<string, string>)[tipo] || tipo;
  }

  resultadoCurto(c: ConsultaItem): string {
    if (c.status === 'PROCESSANDO') return 'Consultando...';
    if (c.status === 'ERRO') return c.estornada ? 'Sem resultado (devolvido)' : 'Sem resultado';
    if (c.nadaConsta) return 'Nada consta';
    const graves = c.alertas.filter(a => a.grave).length;
    return graves ? `${graves} ${graves === 1 ? 'alerta grave' : 'alertas graves'}` : `${c.alertas.length} ${c.alertas.length === 1 ? 'aviso' : 'avisos'}`;
  }

  classeResultado(c: ConsultaItem): string {
    if (c.status === 'PROCESSANDO') return 'processando';
    if (c.status === 'ERRO') return 'erro';
    if (c.nadaConsta) return 'ok';
    return c.alertas.some(a => a.grave) ? 'grave' : 'aviso';
  }
}
