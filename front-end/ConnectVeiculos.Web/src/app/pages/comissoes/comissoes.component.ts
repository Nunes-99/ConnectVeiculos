import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService, ToastService, UsuarioService } from '../../core/services';
import { Usuario } from '../../core/models';
import {
  ComissaoExtrato, ComissaoItem, ComissaoService, RegraComissao, RegrasComissao, TIPOS_COMISSAO, TipoComissao
} from '../../core/services/comissao.service';

interface LinhaRegra { usuario: Usuario; propria: boolean; regra: RegraComissao; }

/**
 * Comissoes dos vendedores: extrato do mes (por vendedor e por venda),
 * controle de pago/a pagar e regras (padrao da loja e por vendedor), que
 * preenchem a comissao ao registrar a venda. O vendedor ve so as dele.
 */
@Component({
  selector: 'app-comissoes',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './comissoes.component.html',
  styleUrl: './comissoes.component.scss'
})
export class ComissoesComponent implements OnInit {
  private comissoes = inject(ComissaoService);
  private usuariosApi = inject(UsuarioService);
  private auth = inject(AuthService);
  private toast = inject(ToastService);

  readonly tipos = TIPOS_COMISSAO;
  get gestao(): boolean { return this.auth.isGerente(); }

  /** "2026-11" (input type=month). */
  mes = new Date().toISOString().slice(0, 7);
  vendedorId: number | null = null;
  extrato: ComissaoExtrato | null = null;
  carregando = false;
  usuarios: Usuario[] = [];

  selecionadas = new Set<number>();
  dataPagamento = new Date().toISOString().slice(0, 10);
  pagando = false;

  mostrarRegras = false;
  regraPadrao: RegraComissao = { tipo: 'PERCENTUAL_VENDA', valor: 0 };
  linhasRegra: LinhaRegra[] = [];
  salvandoRegras = false;

  ngOnInit(): void {
    this.carregar();
    if (this.gestao) {
      this.usuariosApi.getAll().subscribe({ next: u => this.usuarios = u.filter(x => x.usuSts !== false), error: () => {} });
    }
  }

  carregar(): void {
    const [ano, mes] = this.mes.split('-').map(Number);
    if (!ano || !mes) return;
    const inicio = `${this.mes}-01`;
    const fim = new Date(ano, mes, 0).toISOString().slice(0, 10);
    this.carregando = true;
    this.comissoes.extrato(inicio, fim, this.vendedorId).subscribe({
      next: e => { this.extrato = e; this.carregando = false; this.selecionadas.clear(); },
      error: err => { this.carregando = false; this.toast.error(err?.error?.error || 'Não foi possível carregar as comissões.'); }
    });
  }

  alternar(v: ComissaoItem): void {
    if (this.selecionadas.has(v.venId)) this.selecionadas.delete(v.venId); else this.selecionadas.add(v.venId);
  }

  selecionarAPagar(): void {
    this.selecionadas = new Set((this.extrato?.vendas ?? []).filter(v => !v.pagaEm && v.comissao > 0).map(v => v.venId));
  }

  get totalSelecionado(): number {
    return (this.extrato?.vendas ?? []).filter(v => this.selecionadas.has(v.venId)).reduce((s, v) => s + v.comissao, 0);
  }

  marcar(paga: boolean): void {
    if (this.selecionadas.size === 0) return;
    this.pagando = true;
    this.comissoes.pagar([...this.selecionadas], paga ? this.dataPagamento : null).subscribe({
      next: r => {
        this.pagando = false;
        this.toast.success(paga ? `${r.atualizadas} comissão(ões) marcada(s) como paga(s).` : `${r.atualizadas} comissão(ões) voltaram para "a pagar".`);
        this.carregar();
      },
      error: err => { this.pagando = false; this.toast.error(err?.error?.error || 'Não foi possível atualizar.'); }
    });
  }

  abrirRegras(): void {
    this.mostrarRegras = !this.mostrarRegras;
    if (!this.mostrarRegras) return;
    this.comissoes.regras().subscribe({
      next: r => this.aplicarRegras(r),
      error: err => this.toast.error(err?.error?.error || 'Não foi possível carregar as regras.')
    });
  }

  private aplicarRegras(r: RegrasComissao): void {
    this.regraPadrao = { ...r.padrao };
    this.linhasRegra = this.usuarios.map(u => {
      const propria = r.vendedores?.[u.usuId];
      return { usuario: u, propria: !!propria, regra: propria ? { ...propria } : { ...r.padrao } };
    });
  }

  unidade(tipo: TipoComissao): string {
    return this.tipos.find(t => t.id === tipo)?.unidade ?? '%';
  }

  salvarRegras(): void {
    const vendedores: Record<number, RegraComissao> = {};
    this.linhasRegra.filter(l => l.propria).forEach(l => vendedores[l.usuario.usuId] = { tipo: l.regra.tipo, valor: Number(l.regra.valor) || 0 });
    this.salvandoRegras = true;
    this.comissoes.salvarRegras({ padrao: { tipo: this.regraPadrao.tipo, valor: Number(this.regraPadrao.valor) || 0 }, vendedores }).subscribe({
      next: r => {
        this.salvandoRegras = false;
        this.aplicarRegras(r);
        this.toast.success('Regras salvas. Valem para as próximas vendas.');
      },
      error: err => { this.salvandoRegras = false; this.toast.error(err?.error?.error || 'Não foi possível salvar as regras.'); }
    });
  }

  moeda(v: number): string {
    return (v || 0).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  }
}
