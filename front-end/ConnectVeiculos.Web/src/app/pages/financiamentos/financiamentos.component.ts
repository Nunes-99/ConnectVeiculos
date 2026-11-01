import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService, ToastService, VeiculoService } from '../../core/services';
import { Veiculo } from '../../core/models';
import { MaskDirective, CurrencyMaskDirective } from '../../shared/directives';
import {
  BancoLoja, EstimativaBanco, FaixaTaxaBanco, Ficha, FichaFinanciamentoService, FichaInput, PropostaBanco,
  ROTULO_FICHA, ROTULO_PROPOSTA, StatusFicha, StatusProposta, erroFinanciamento
} from '../../core/services/ficha-financiamento.service';

/** Prazos que aparecem na tabela de taxas de cada banco. */
const PRAZOS = [12, 18, 24, 36, 48, 60];

interface FaixaEdicao {
  anoDe: number | null;
  anoAte: number | null;
  entradaMinimaPct: number;
  /** taxa mensal por prazo; null = o banco nao oferece esse prazo */
  taxas: Record<number, number | null>;
}

/**
 * Ficha unica de financiamento. O vendedor preenche o cliente uma vez
 * (nome, CPF, telefone, e-mail) e ve na hora a estimativa de parcelas em
 * todos os bancos da loja, pelas tabelas de taxa que a gerencia cadastrou.
 * A aprovacao continua no portal de cada banco; o retorno e anotado aqui.
 */
@Component({
  selector: 'app-financiamentos',
  standalone: true,
  imports: [CommonModule, FormsModule, MaskDirective, CurrencyMaskDirective],
  templateUrl: './financiamentos.component.html',
  styleUrl: './financiamentos.component.scss'
})
export class FinanciamentosComponent implements OnInit {
  private api = inject(FichaFinanciamentoService);
  private veiculoService = inject(VeiculoService);
  private toast = inject(ToastService);
  private auth = inject(AuthService);

  readonly prazos = PRAZOS;
  readonly rotuloProposta = ROTULO_PROPOSTA;
  readonly rotuloFicha = ROTULO_FICHA;
  readonly statusPropostas = Object.keys(ROTULO_PROPOSTA) as StatusProposta[];
  readonly statusFichas = Object.keys(ROTULO_FICHA) as StatusFicha[];
  readonly gestao = this.auth.isGerente();

  aba: 'fichas' | 'bancos' = 'fichas';
  carregando = true;
  salvando = false;

  fichas: Ficha[] = [];
  bancos: BancoLoja[] = [];
  veiculos: Veiculo[] = [];
  busca = '';

  // ---------- ficha aberta ----------
  fichaAberta = false;
  ficha: Ficha | null = null;
  form: FichaInput = this.fichaVazia();
  estimativa: EstimativaBanco[] | null = null;
  estimando = false;
  proposta: PropostaBanco = this.propostaVazia();

  // ---------- banco em edicao ----------
  banco: BancoLoja | null = null;
  faixas: FaixaEdicao[] = [];

  ngOnInit(): void {
    this.carregar();
    this.veiculoService.getAll().subscribe({
      next: v => this.veiculos = v.filter(x => x.veiSts === 'D').sort((a, b) => a.veiMarca.localeCompare(b.veiMarca)),
      error: () => {}
    });
  }

  carregar(): void {
    this.carregando = true;
    this.api.fichas().subscribe({
      next: f => { this.fichas = f; this.carregando = false; },
      error: err => { this.carregando = false; this.toast.error(erroFinanciamento(err, 'Não foi possível carregar as fichas.')); }
    });
    this.api.bancos().subscribe({ next: b => this.bancos = b, error: () => {} });
  }

  get fichasFiltradas(): Ficha[] {
    const t = this.busca.trim().toLowerCase();
    if (!t) return this.fichas;
    const dig = t.replace(/\D/g, '');
    return this.fichas.filter(f => f.nome.toLowerCase().includes(t) || (dig.length >= 3 && f.cpf.includes(dig))
      || (f.veiculo || '').toLowerCase().includes(t));
  }

  get bancosAtivos(): BancoLoja[] {
    return this.bancos.filter(b => b.ativo);
  }

  // ================================================================
  // Ficha
  // ================================================================

  private fichaVazia(): FichaInput {
    return {
      nome: '', cpf: '', telefone: '', email: '', veiculoId: null, valorVeiculo: 0, entrada: 0,
      anoVeiculo: new Date().getFullYear(), observacao: null, consentimento: false
    };
  }

  private propostaVazia(): PropostaBanco {
    return {
      bancoId: 0, banco: '', status: 'ENVIADA', meses: null, taxaMensal: null, valorParcela: null,
      entradaExigida: null, observacao: null
    };
  }

  novaFicha(): void {
    this.ficha = null;
    this.form = this.fichaVazia();
    this.estimativa = null;
    this.proposta = this.propostaVazia();
    this.fichaAberta = true;
  }

  abrirFicha(f: Ficha): void {
    this.ficha = f;
    this.form = {
      nome: f.nome, cpf: this.cpf(f.cpf), telefone: this.telefone(f.telefone), email: f.email,
      veiculoId: f.veiculoId, valorVeiculo: f.valorVeiculo, entrada: f.entrada, anoVeiculo: f.anoVeiculo,
      observacao: f.observacao, consentimento: true
    };
    this.estimativa = null;
    this.proposta = this.propostaVazia();
    this.fichaAberta = true;
    if (this.bancosAtivos.length) this.simular();
  }

  fecharFicha(): void {
    this.fichaAberta = false;
    this.ficha = null;
  }

  escolherVeiculo(): void {
    const v = this.veiculos.find(x => x.veiId === Number(this.form.veiculoId));
    if (!v) return;
    this.form.valorVeiculo = v.veiPreco;
    this.form.anoVeiculo = v.veiAno;
    if (!this.form.entrada) this.form.entrada = Math.round(v.veiPreco * 0.3);
    this.estimativa = null;
  }

  salvarFicha(): void {
    if (!this.ficha && !this.form.consentimento) {
      this.toast.warning('Marque que o cliente autorizou a consulta de crédito.');
      return;
    }
    this.salvando = true;
    const dados = { ...this.form, veiculoId: this.form.veiculoId ? Number(this.form.veiculoId) : null };
    const req = this.ficha ? this.api.atualizar(this.ficha.id, dados) : this.api.criar(dados);
    req.subscribe({
      next: f => {
        this.salvando = false;
        this.ficha = f;
        this.toast.success('Ficha salva.');
        this.atualizarNaLista(f);
        if (!this.estimativa && this.bancosAtivos.length) this.simular();
      },
      error: err => { this.salvando = false; this.toast.error(erroFinanciamento(err, 'Não foi possível salvar a ficha.')); }
    });
  }

  private atualizarNaLista(f: Ficha): void {
    const i = this.fichas.findIndex(x => x.id === f.id);
    if (i >= 0) this.fichas[i] = f; else this.fichas.unshift(f);
  }

  /** Estimativa em todos os bancos da loja, pelas tabelas de taxa (nao consulta ninguem). */
  simular(): void {
    if (!this.form.valorVeiculo || !this.form.anoVeiculo) {
      this.toast.warning('Escolha o veículo ou informe valor e ano.');
      return;
    }
    this.estimando = true;
    this.api.estimar(this.form.valorVeiculo, this.form.entrada || 0, this.form.anoVeiculo).subscribe({
      next: r => { this.estimativa = r; this.estimando = false; },
      error: err => { this.estimando = false; this.estimativa = null; this.toast.error(erroFinanciamento(err, 'Não foi possível simular.')); }
    });
  }

  /** Menor parcela de cada prazo entre os bancos, para destacar na tela. */
  melhorParcela(meses: number): number | null {
    const v = (this.estimativa || []).flatMap(b => b.prazos.filter(p => p.meses === meses).map(p => p.valorParcela));
    return v.length ? Math.min(...v) : null;
  }

  /** Copia os dados que os portais dos bancos pedem, para colar la. */
  copiarDados(): void {
    const f = this.form;
    const veiculo = this.veiculos.find(v => v.veiId === Number(f.veiculoId));
    const linhas = [
      `Nome: ${f.nome}`,
      `CPF: ${this.cpf(f.cpf)}`,
      `Telefone: ${this.telefone(f.telefone)}`,
      `E-mail: ${f.email}`,
      veiculo ? `Veículo: ${veiculo.veiMarca} ${veiculo.veiModelo} ${veiculo.veiAno}${veiculo.veiPlaca ? ' · ' + veiculo.veiPlaca : ''}`
        : (this.ficha?.veiculo ? `Veículo: ${this.ficha.veiculo}` : `Ano do veículo: ${f.anoVeiculo}`),
      `Valor: ${this.moeda(f.valorVeiculo)}`,
      `Entrada: ${this.moeda(f.entrada)}`
    ];
    navigator.clipboard?.writeText(linhas.join('\n')).then(
      () => this.toast.success('Dados copiados. É só colar no portal do banco.'),
      () => this.toast.error('Não foi possível copiar.'));
  }

  alterarStatus(status: StatusFicha): void {
    if (!this.ficha) return;
    this.api.alterarStatus(this.ficha.id, status).subscribe({
      next: f => { this.ficha = f; this.atualizarNaLista(f); this.toast.success('Situação atualizada.'); },
      error: err => this.toast.error(erroFinanciamento(err, 'Não foi possível atualizar.'))
    });
  }

  /** Comeca a anotar o retorno de um banco, ja com o prazo e a parcela estimados. */
  anotarBanco(bancoId: number, meses?: number): void {
    const existente = this.ficha?.propostas.find(p => p.bancoId === bancoId);
    const est = this.estimativa?.find(b => b.bancoId === bancoId)?.prazos.find(p => p.meses === meses);
    this.proposta = existente ? { ...existente } : {
      ...this.propostaVazia(), bancoId,
      meses: meses ?? null, taxaMensal: est?.taxaMensal ?? null, valorParcela: est?.valorParcela ?? null
    };
    setTimeout(() => document.getElementById('anotar-proposta')?.scrollIntoView({ behavior: 'smooth', block: 'center' }));
  }

  registrarProposta(): void {
    if (!this.ficha) { this.toast.warning('Salve a ficha antes.'); return; }
    if (!this.proposta.bancoId) { this.toast.warning('Escolha o banco.'); return; }
    this.salvando = true;
    this.api.registrarProposta(this.ficha.id, { ...this.proposta, bancoId: Number(this.proposta.bancoId) }).subscribe({
      next: f => {
        this.salvando = false;
        this.ficha = f;
        this.atualizarNaLista(f);
        this.proposta = this.propostaVazia();
        this.toast.success('Retorno do banco anotado.');
      },
      error: err => { this.salvando = false; this.toast.error(erroFinanciamento(err, 'Não foi possível anotar.')); }
    });
  }

  removerProposta(p: PropostaBanco): void {
    if (!this.ficha) return;
    this.api.removerProposta(this.ficha.id, p.bancoId).subscribe({
      next: f => { this.ficha = f; this.atualizarNaLista(f); },
      error: err => this.toast.error(erroFinanciamento(err, 'Não foi possível remover.'))
    });
  }

  // ================================================================
  // Bancos da loja (gerencia)
  // ================================================================

  novoBanco(): void {
    this.banco = {
      id: 0, nome: '', portalUrl: null, ativo: true, tarifaCadastro: 0, registroContrato: 0,
      tarifasFinanciadas: true, faixas: [], observacao: null
    };
    this.faixas = [this.faixaVazia()];
  }

  editarBanco(b: BancoLoja): void {
    this.banco = { ...b };
    this.faixas = b.faixas.length ? b.faixas.map(f => this.paraEdicao(f)) : [this.faixaVazia()];
  }

  private faixaVazia(): FaixaEdicao {
    const taxas: Record<number, number | null> = {};
    PRAZOS.forEach(p => taxas[p] = null);
    return { anoDe: null, anoAte: null, entradaMinimaPct: 0, taxas };
  }

  private paraEdicao(f: FaixaTaxaBanco): FaixaEdicao {
    const e = this.faixaVazia();
    e.anoDe = f.anoDe; e.anoAte = f.anoAte; e.entradaMinimaPct = f.entradaMinimaPct;
    f.taxas.forEach(t => e.taxas[t.meses] = t.taxaMensal);
    return e;
  }

  adicionarFaixa(): void {
    this.faixas.push(this.faixaVazia());
  }

  removerFaixa(i: number): void {
    this.faixas.splice(i, 1);
  }

  salvarBanco(): void {
    if (!this.banco) return;
    const faixas: FaixaTaxaBanco[] = this.faixas.map(f => ({
      anoDe: f.anoDe || null,
      anoAte: f.anoAte || null,
      entradaMinimaPct: Number(f.entradaMinimaPct) || 0,
      taxas: PRAZOS
        .filter(p => f.taxas[p] !== null && f.taxas[p] !== undefined && String(f.taxas[p]) !== '')
        .map(p => ({ meses: p, taxaMensal: Number(String(f.taxas[p]).replace(',', '.')) }))
    })).filter(f => f.taxas.length);
    this.salvando = true;
    this.api.salvarBanco({ ...this.banco, faixas }).subscribe({
      next: () => { this.salvando = false; this.banco = null; this.toast.success('Banco salvo.'); this.carregar(); },
      error: err => { this.salvando = false; this.toast.error(erroFinanciamento(err, 'Não foi possível salvar o banco.')); }
    });
  }

  removerBanco(b: BancoLoja): void {
    this.api.removerBanco(b.id).subscribe({
      next: () => { this.toast.success('Banco removido.'); this.carregar(); },
      error: err => this.toast.error(erroFinanciamento(err, 'Não foi possível remover.'))
    });
  }

  resumoFaixas(b: BancoLoja): string {
    if (!b.faixas.length) return 'Sem tabela de taxas';
    const taxas = b.faixas.flatMap(f => f.taxas.map(t => t.taxaMensal));
    const prazos = [...new Set(b.faixas.flatMap(f => f.taxas.map(t => t.meses)))].sort((x, y) => x - y);
    return `${b.faixas.length} faixa(s) · ${Math.min(...taxas).toFixed(2)}% a ${Math.max(...taxas).toFixed(2)}% a.m. · ${prazos.join('/')}x`;
  }

  // ================================================================
  // Formatacao
  // ================================================================

  moeda(v: number | null | undefined): string {
    return (v || 0).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  }

  cpf(v: string): string {
    const d = (v || '').replace(/\D/g, '');
    return d.length === 11 ? `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}` : v;
  }

  telefone(v: string): string {
    const d = (v || '').replace(/\D/g, '');
    if (d.length === 11) return `(${d.slice(0, 2)}) ${d.slice(2, 7)}-${d.slice(7)}`;
    if (d.length === 10) return `(${d.slice(0, 2)}) ${d.slice(2, 6)}-${d.slice(6)}`;
    return v;
  }

  propostasAprovadas(f: Ficha): number {
    return f.propostas.filter(p => p.status === 'APROVADA').length;
  }
}
