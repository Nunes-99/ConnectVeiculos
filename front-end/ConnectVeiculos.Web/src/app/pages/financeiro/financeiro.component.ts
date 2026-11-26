import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService, LojaService, ToastService } from '../../core/services';
import { Loja } from '../../core/models';
import {
  CATEGORIAS_PAGAR, CATEGORIAS_RECEBER, FinanceiroMes, FinanceiroService, Lancamento, LancamentoInput
} from '../../core/services/financeiro.service';

/**
 * Financeiro simples: contas a pagar e a receber da loja e o fluxo de caixa
 * do mes. Vendas, compras de carros e despesas de carros entram no fluxo
 * sozinhas; aqui se lanca o resto (aluguel, salarios, impostos...).
 */
@Component({
  selector: 'app-financeiro',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './financeiro.component.html',
  styleUrl: './financeiro.component.scss'
})
export class FinanceiroComponent implements OnInit {
  private financeiro = inject(FinanceiroService);
  private lojasApi = inject(LojaService);
  private auth = inject(AuthService);
  private toast = inject(ToastService);

  get gestao(): boolean { return this.auth.isGerente(); }

  ano = new Date().getFullYear();
  mes = new Date().getMonth() + 1;
  lojaId: number | null = null;
  lojas: Loja[] = [];
  dados: FinanceiroMes | null = null;
  carregando = false;
  aba: 'contas' | 'fluxo' = 'contas';
  filtroContas: 'todas' | 'P' | 'R' | 'abertas' = 'todas';

  editando: (LancamentoInput & { id?: number }) | null = null;
  salvando = false;
  excluirId: number | null = null;

  readonly meses = ['janeiro', 'fevereiro', 'março', 'abril', 'maio', 'junho', 'julho', 'agosto', 'setembro', 'outubro', 'novembro', 'dezembro'];

  get tituloMes(): string { return `${this.meses[this.mes - 1]} de ${this.ano}`; }

  get categorias(): string[] {
    return this.editando?.tipo === 'R' ? CATEGORIAS_RECEBER : CATEGORIAS_PAGAR;
  }

  get contasFiltradas(): Lancamento[] {
    const l = this.dados?.lancamentos ?? [];
    switch (this.filtroContas) {
      case 'P': case 'R': return l.filter(x => x.tipo === this.filtroContas);
      case 'abertas': return l.filter(x => !x.pagoEm);
      default: return l;
    }
  }

  get categoriasSaida(): { nome: string; valor: number; pct: number }[] {
    const c = this.dados?.saidasPorCategoria ?? {};
    const total = Object.values(c).reduce((s, v) => s + v, 0) || 1;
    return Object.entries(c).map(([nome, valor]) => ({ nome, valor, pct: Math.round(valor / total * 100) }));
  }

  ngOnInit(): void {
    this.carregar();
    this.lojasApi.getAll().subscribe({ next: l => this.lojas = l, error: () => {} });
  }

  mudarMes(delta: number): void {
    const d = new Date(this.ano, this.mes - 1 + delta, 1);
    this.ano = d.getFullYear();
    this.mes = d.getMonth() + 1;
    this.carregar();
  }

  carregar(): void {
    this.carregando = true;
    this.financeiro.mes(this.ano, this.mes, this.lojaId).subscribe({
      next: d => { this.dados = d; this.carregando = false; },
      error: err => { this.carregando = false; this.toast.error(err?.error?.error || 'Não foi possível carregar o financeiro.'); }
    });
  }

  novo(tipo: 'P' | 'R'): void {
    const hoje = new Date().toISOString().slice(0, 10);
    this.editando = { tipo, descricao: '', categoria: null, valor: 0, vencimento: hoje, lojaId: this.lojaId, observacao: null, recorrente: false, pagoEm: null };
  }

  editar(l: Lancamento): void {
    this.editando = {
      id: l.id, tipo: l.tipo, descricao: l.descricao, categoria: l.categoria, valor: l.valor,
      vencimento: l.vencimento.slice(0, 10), lojaId: l.lojaId, observacao: l.observacao, recorrente: l.recorrente
    };
  }

  salvar(): void {
    if (!this.editando) return;
    const { id, ...input } = this.editando;
    input.valor = Number(input.valor) || 0;
    this.salvando = true;
    const obs = id ? this.financeiro.alterar(id, input) : this.financeiro.criar(input);
    obs.subscribe({
      next: () => { this.salvando = false; this.editando = null; this.toast.success('Lançamento salvo.'); this.carregar(); },
      error: err => { this.salvando = false; this.toast.error(err?.error?.error || 'Não foi possível salvar.'); }
    });
  }

  pagar(l: Lancamento, pago: boolean): void {
    this.financeiro.pagar(l.id, pago ? new Date().toISOString().slice(0, 10) : null).subscribe({
      next: () => {
        this.toast.success(pago ? (l.tipo === 'R' ? 'Marcado como recebido.' : 'Marcado como pago.') + (l.recorrente ? ' O do mês que vem já foi lançado.' : '') : 'Voltou para em aberto.');
        this.carregar();
      },
      error: err => this.toast.error(err?.error?.error || 'Não foi possível atualizar.')
    });
  }

  /** Primeiro clique pede confirmacao no proprio botao. */
  excluir(l: Lancamento): void {
    if (this.excluirId !== l.id) { this.excluirId = l.id; return; }
    this.excluirId = null;
    this.financeiro.excluir(l.id).subscribe({
      next: () => { this.toast.success('Lançamento excluído.'); this.carregar(); },
      error: err => this.toast.error(err?.error?.error || 'Não foi possível excluir.')
    });
  }

  situacaoTexto(l: Lancamento): string {
    switch (l.situacao) {
      case 'pago': return l.tipo === 'R' ? 'Recebido' : 'Pago';
      case 'atrasado': return 'Atrasado';
      case 'vence_hoje': return 'Vence hoje';
      default: return 'Em aberto';
    }
  }

  moeda(v: number): string {
    return (v || 0).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  }
}
