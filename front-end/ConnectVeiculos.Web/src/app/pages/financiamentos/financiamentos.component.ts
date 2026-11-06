import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService, ToastService, VeiculoService } from '../../core/services';
import { Veiculo } from '../../core/models';
import { MaskDirective, CurrencyMaskDirective } from '../../shared/directives';
import {
  BancoLoja, EstimativaBanco, FaixaTaxaBanco, Ficha, FichaFinanciamentoService, FichaInput, PropostaBanco,
  ROTULO_FICHA, ROTULO_PROPOSTA, StatusFicha, StatusProposta, erroFinanciamento
} from '../../core/services/ficha-financiamento.service';
import {
  CAMPOS_CREDERE, CredereCampoExigido, CredereCondicao, CredereModeloVeiculo, CredereService, CredereSimulacao,
  CredereSimulacaoInput, CredereStatus, OCUPACOES_CREDERE, erroCredere
} from '../../core/services/credere.service';

/**
 * Credere (simulacao multibanco) desligada em 06/11/2026: fica para depois dos
 * primeiros clientes. Para religar, true aqui e mostrarCredere em integracoes.component.ts.
 */
const CREDERE_LIGADA = false;

/** Consulta o resultado a cada 5 s (o que a Credere sugere) por ate 3 min. */
const INTERVALO_CREDERE = 5000;
const TENTATIVAS_CREDERE = 36;

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
export class FinanciamentosComponent implements OnInit, OnDestroy {
  private api = inject(FichaFinanciamentoService);
  private credereApi = inject(CredereService);
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

  // ---------- Credere ----------
  readonly ocupacoes = OCUPACOES_CREDERE;
  readonly nomesCampos = CAMPOS_CREDERE;
  credere: CredereStatus | null = null;
  cr: CredereSimulacaoInput = this.credereVazio();
  crPrazos: Record<number, boolean> = { 12: false, 18: false, 24: true, 36: true, 48: true, 60: true };
  buscaModelo = '';
  modelos: CredereModeloVeiculo[] = [];
  buscandoModelo = false;
  camposExigidos: CredereCampoExigido[] = [];
  simulacao: CredereSimulacao | null = null;
  simulandoCredere = false;
  private consulta: ReturnType<typeof setInterval> | null = null;

  get credereAtiva(): boolean {
    return CREDERE_LIGADA && !!this.credere?.conectado && !!this.credere.lojaId;
  }

  ngOnDestroy(): void {
    this.pararConsulta();
  }

  ngOnInit(): void {
    this.carregar();
    if (CREDERE_LIGADA) this.credereApi.status().subscribe({ next: s => this.credere = s, error: () => {} });
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
    this.prepararCredere(f);
  }

  fecharFicha(): void {
    this.pararConsulta();
    this.fichaAberta = false;
    this.ficha = null;
  }

  // ================================================================
  // Credere (condicoes reais dos bancos)
  // ================================================================

  private credereVazio(): CredereSimulacaoInput {
    return {
      nascimento: null, possuiCnh: null, genero: null, rendaMensal: null, cep: null, ocupacao: null,
      modeloId: 0, modeloNome: null, anoFabricacao: new Date().getFullYear(), anoModelo: new Date().getFullYear(),
      ufLicenciamento: '', cidadeLicenciamento: '', prazos: []
    };
  }

  /** Preenche a simulacao com o que a ficha ja guardou e a cidade da loja. */
  private prepararCredere(f: Ficha): void {
    this.pararConsulta();
    this.simulacao = null;
    this.modelos = [];
    this.camposExigidos = [];
    if (!this.credereAtiva) return;

    const d = f.credere;
    const loja = this.credere?.lojas.find(l => l.id === this.credere?.lojaId);
    this.cr = {
      ...this.credereVazio(),
      nascimento: d?.nascimento ? d.nascimento.substring(0, 10) : null,
      possuiCnh: d?.possuiCnh ?? null,
      genero: d?.genero ?? null,
      rendaMensal: d?.rendaMensal ?? null,
      cep: d?.cep ?? null,
      ocupacao: d?.ocupacao ?? null,
      modeloId: d?.modeloId ?? 0,
      modeloNome: d?.modeloNome ?? null,
      anoModelo: f.anoVeiculo,
      anoFabricacao: d?.anoFabricacao ?? f.anoVeiculo,
      ufLicenciamento: d?.ufLicenciamento ?? loja?.uf ?? '',
      cidadeLicenciamento: d?.cidadeLicenciamento ?? loja?.cidade ?? ''
    };
    if (d?.prazos?.length) {
      Object.keys(this.crPrazos).forEach(k => this.crPrazos[+k] = d.prazos.includes(+k));
    }
    const v = this.veiculos.find(x => x.veiId === f.veiculoId);
    this.buscaModelo = v ? `${v.veiMarca} ${v.veiModelo}` : '';

    this.credereApi.campos(f.id).subscribe({ next: c => this.camposExigidos = c, error: () => {} });
    if (d?.ultimaSimulacao) this.consultarCredere(false);
  }

  /** Quantos bancos da loja exigem o campo (0 = nenhum). */
  exigidoPor(campo: string): number {
    return this.camposExigidos.find(c => c.campo === campo)?.bancos.length ?? 0;
  }

  buscarModelos(): void {
    if (this.buscaModelo.trim().length < 2) return;
    this.buscandoModelo = true;
    this.credereApi.modelos(this.buscaModelo.trim(), this.cr.anoModelo).subscribe({
      next: m => { this.modelos = m; this.buscandoModelo = false; if (!m.length) this.toast.warning('Nenhuma versão encontrada. Tente só marca e modelo.'); },
      error: err => { this.buscandoModelo = false; this.toast.error(erroCredere(err, 'Não foi possível buscar o modelo.')); }
    });
  }

  escolherModelo(id: number): void {
    const m = this.modelos.find(x => x.id === Number(id));
    this.cr.modeloId = m?.id ?? 0;
    this.cr.modeloNome = m ? `${m.marca} ${m.nome} ${m.versao || ''}`.trim() : null;
  }

  simularCredere(): void {
    if (!this.ficha) { this.toast.warning('Salve a ficha antes.'); return; }
    if (!this.cr.modeloId) { this.toast.warning('Escolha a versão do veículo na lista da Credere.'); return; }
    const dados: CredereSimulacaoInput = {
      ...this.cr,
      nascimento: this.cr.nascimento || null,
      rendaMensal: this.cr.rendaMensal || null,
      prazos: Object.keys(this.crPrazos).map(Number).filter(p => this.crPrazos[p])
    };
    this.simulandoCredere = true;
    this.simulacao = null;
    this.credereApi.simular(this.ficha.id, dados).subscribe({
      next: s => {
        this.simulacao = s;
        if (this.ficha) {
          this.ficha.credere = { ...(this.ficha.credere || {} as any), ultimaSimulacao: s.uuid };
        }
        if (s.concluida) this.simulandoCredere = false; else this.consultarCredere(true);
      },
      error: err => { this.simulandoCredere = false; this.toast.error(erroCredere(err, 'Não foi possível simular na Credere.')); }
    });
  }

  /** Os bancos respondem aos poucos: busca o resultado ate todos terminarem. */
  private consultarCredere(repetir: boolean): void {
    if (!this.ficha) return;
    const fichaId = this.ficha.id;
    let tentativas = 0;
    const buscar = () => this.credereApi.simulacao(fichaId).subscribe({
      next: s => {
        if (this.ficha?.id !== fichaId) return;
        this.simulacao = s;
        if (s.concluida || ++tentativas >= TENTATIVAS_CREDERE) {
          this.pararConsulta();
          this.simulandoCredere = false;
          if (!s.concluida) this.toast.warning('Alguns bancos ainda não responderam. Clique em Atualizar daqui a pouco.');
        }
      },
      error: err => { this.pararConsulta(); this.simulandoCredere = false; this.toast.error(erroCredere(err, 'Não foi possível buscar o resultado.')); }
    });
    this.pararConsulta();
    if (repetir) {
      this.simulandoCredere = true;
      this.consulta = setInterval(buscar, INTERVALO_CREDERE);
    } else {
      buscar();
    }
  }

  atualizarCredere(): void {
    this.consultarCredere(false);
  }

  private pararConsulta(): void {
    if (this.consulta) { clearInterval(this.consulta); this.consulta = null; }
  }

  /** Condicoes agrupadas por banco, na ordem que a API mandou (melhores primeiro). */
  get gruposCredere(): { banco: string; condicoes: CredereCondicao[] }[] {
    const grupos: { banco: string; condicoes: CredereCondicao[] }[] = [];
    for (const c of this.simulacao?.condicoes || []) {
      const g = grupos.find(x => x.banco === c.banco);
      if (g) g.condicoes.push(c); else grupos.push({ banco: c.banco, condicoes: [c] });
    }
    return grupos;
  }

  get pendentesCredere(): number {
    return (this.simulacao?.condicoes || []).filter(c => c.processando).length;
  }

  rotuloCondicao(c: CredereCondicao): { texto: string; tipo: string } {
    if (c.processando) return { texto: 'Aguardando o banco', tipo: 'ENVIADA' };
    if (!c.disponivel) return { texto: 'Indisponível', tipo: 'RECUSADA' };
    if (c.aprovado) return { texto: 'Aprovado', tipo: 'APROVADA' };
    switch (c.preAprovacao) {
      case 3: return { texto: 'Pré-aprovado', tipo: 'APROVADA' };
      case 2: return { texto: 'Chance de aprovação', tipo: 'EM_ANALISE' };
      case 1: return { texto: 'CPF com restrição', tipo: 'RECUSADA' };
      default: return { texto: 'Sem pré-análise', tipo: 'ENVIADA' };
    }
  }

  rotuloSugestao(c: CredereCondicao): string | null {
    if (c.sugestao === 'bank_minimum_down_payment') return 'Entrada mínima do banco';
    if (c.sugestao === 'bank_suggested_down_payment') return 'Entrada sugerida pelo banco';
    return null;
  }

  /** Leva a condicao para "Anotar retorno", se o banco estiver cadastrado na loja. */
  anotarCredere(c: CredereCondicao): void {
    const nome = c.banco.toLowerCase();
    const banco = this.bancos.find(b => b.nome.toLowerCase().includes(nome) || nome.includes(b.nome.toLowerCase()));
    this.proposta = {
      ...this.propostaVazia(),
      bancoId: banco?.id ?? 0,
      status: c.aprovado ? 'APROVADA' : 'ENVIADA',
      meses: c.prazo,
      taxaMensal: c.taxaMensal,
      valorParcela: c.parcela,
      entradaExigida: c.entrada,
      observacao: `Credere: ${this.rotuloCondicao(c).texto}`
    };
    if (!banco) this.toast.warning(`Cadastre o ${c.banco} em Bancos da loja para anotar o retorno.`);
    setTimeout(() => document.getElementById('anotar-proposta')?.scrollIntoView({ behavior: 'smooth', block: 'center' }));
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
    const nova = !this.ficha;
    const dados = { ...this.form, veiculoId: this.form.veiculoId ? Number(this.form.veiculoId) : null };
    const req = this.ficha ? this.api.atualizar(this.ficha.id, dados) : this.api.criar(dados);
    req.subscribe({
      next: f => {
        this.salvando = false;
        this.ficha = f;
        if (nova) this.prepararCredere(f);
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
