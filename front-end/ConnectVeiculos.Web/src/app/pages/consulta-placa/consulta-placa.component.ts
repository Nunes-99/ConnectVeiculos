import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService, ToastService } from '../../core/services';
import { PixGerado } from '../../core/services/plano.service';
import {
  AvisoAtpve, CategoriaConsulta, ConsultaItem, ConsultaPlacaPainel, ConsultaPlacaService, PedidoConsulta, ServicoConsulta
} from '../../core/services/consulta-placa.service';
import { DadosConsultaComponent } from './dados-consulta.component';

interface Aba { id: CategoriaConsulta; nome: string; icone: string; }

/**
 * Consultas da ZapCar pagas com os creditos da loja: veiculo (gravame,
 * leilao, sinistro, debitos...), documentos (CRLV, ATPV-e, codigo de
 * seguranca, NF-e) e pessoa/empresa (credito, cadastro, CNH) com a
 * finalidade exigida pela LGPD. O catalogo e os precos vem da API.
 */
@Component({
  selector: 'app-consulta-placa',
  standalone: true,
  imports: [CommonModule, FormsModule, DadosConsultaComponent],
  templateUrl: './consulta-placa.component.html',
  styleUrl: './consulta-placa.component.scss'
})
export class ConsultaPlacaComponent implements OnInit, OnDestroy {
  private api = inject(ConsultaPlacaService);
  private auth = inject(AuthService);
  private toast = inject(ToastService);

  readonly abas: Aba[] = [
    { id: 'CONSULTAS', nome: 'Veículo', icone: 'directions_car' },
    { id: 'DOCUMENTOS', nome: 'Documentos', icone: 'description' },
    { id: 'ANALISE_PESSOA', nome: 'Pessoa e empresa', icone: 'badge' }
  ];

  readonly placasTeste = [
    { placa: 'TST0001', texto: 'nada consta' },
    { placa: 'TST0002', texto: 'gravame' },
    { placa: 'TST0003', texto: 'RENAJUD' },
    { placa: 'TST0004', texto: 'débitos' },
    { placa: 'TST0005', texto: 'sinistro e leilão' },
    { placa: 'TST0404', texto: 'não encontrada' }
  ];

  readonly finalidades = [
    { id: 'NEGOCIACAO_VEICULO', nome: 'Negociação de veículo (compra, venda ou troca)' },
    { id: 'ANALISE_CREDITO', nome: 'Análise de crédito (financiamento)' },
    { id: 'VERIFICACAO_CADASTRAL', nome: 'Verificação cadastral' },
    { id: 'OUTRA', nome: 'Outra' }
  ];

  readonly ufs = ['AC', 'AL', 'AM', 'AP', 'BA', 'CE', 'DF', 'ES', 'GO', 'MA', 'MG', 'MS', 'MT', 'PA', 'PB', 'PE', 'PI',
    'PR', 'RJ', 'RN', 'RO', 'RR', 'RS', 'SC', 'SE', 'SP', 'TO'];

  painel: ConsultaPlacaPainel | null = null;
  carregando = true;

  aba: CategoriaConsulta = 'CONSULTAS';
  servicoId = 'consulta-completa';
  form: PedidoConsulta = { servico: 'consulta-completa' };
  aviso: AvisoAtpve | null = null;
  aceiteAtpve = false;
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

  get servicosDaAba(): ServicoConsulta[] {
    return (this.painel?.servicos ?? []).filter(s => s.categoria === this.aba);
  }

  get escolhido(): ServicoConsulta | undefined {
    return this.painel?.servicos.find(s => s.id === this.servicoId);
  }

  /** Campos exigidos, incluindo os do estado no CRLV. */
  get campos(): Set<string> {
    const s = this.escolhido;
    const set = new Set(s?.requer ?? []);
    const uf = s?.precosUf.find(u => u.uf === this.form.uf);
    uf?.requer.forEach(r => set.add(r));
    return set;
  }

  get precoAtual(): number | null {
    const s = this.escolhido;
    if (!s) return null;
    if (s.precosUf.length) return s.precosUf.find(u => u.uf === this.form.uf)?.preco ?? null;
    return s.preco;
  }

  get semSaldo(): boolean {
    return !!this.painel && this.precoAtual !== null && this.painel.saldo < this.precoAtual;
  }

  get documentoRotulo(): string {
    const id = this.servicoId;
    if (id.endsWith('-cnpj')) return 'CNPJ';
    if (id.endsWith('-cpf') || id === 'situacao-cpf' || id === 'cnh-nacional') return 'CPF';
    return 'CPF ou CNPJ';
  }

  get prontoParaConsultar(): boolean {
    const s = this.escolhido;
    if (!s || this.precoAtual === null) return false;
    const f = this.form;
    const c = this.campos;
    const digitos = (v?: string) => (v || '').replace(/\D/g, '');
    if (c.has('placa') && !/^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$/.test(this.placaNormalizada)) return false;
    if (c.has('uf') && !f.uf) return false;
    if (c.has('renavam') && digitos(f.renavam).length < 9) return false;
    if (c.has('cpf') && ![11, 14].includes(digitos(f.cpf).length)) return false;
    if (c.has('chassi') && (f.chassi || '').replace(/[^A-Za-z0-9]/g, '').length !== 17) return false;
    if (c.has('motor') && (f.motor || '').trim().length < 5) return false;
    if (c.has('documento') && ![11, 14].includes(digitos(f.documento).length)) return false;
    if (c.has('cnh') && digitos(f.cnh).length < 9) return false;
    if (c.has('chave') && digitos(f.chave).length !== 44) return false;
    if (c.has('data_nascimento') && !f.dataNascimento) return false;
    if (s.exigeFinalidade && (!f.finalidade || !f.declaracaoFinalidade)) return false;
    if (s.id === 'atpve' && (!this.aviso || !this.aceiteAtpve)) return false;
    return true;
  }

  get placaNormalizada(): string {
    return (this.form.placa || '').toUpperCase().replace(/[^A-Z0-9]/g, '');
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
        if (!p.servicos.some(s => s.id === this.servicoId) && p.servicos.length) this.escolher(p.servicos[0]);
      },
      error: err => {
        this.carregando = false;
        this.toast.error(err?.error?.error || 'Não foi possível carregar as consultas.');
      }
    });
  }

  trocarAba(aba: CategoriaConsulta): void {
    this.aba = aba;
    const primeiro = this.servicosDaAba.find(s => s.recomendado) ?? this.servicosDaAba[0];
    if (primeiro) this.escolher(primeiro);
  }

  escolher(s: ServicoConsulta): void {
    this.servicoId = s.id;
    this.aba = s.categoria;
    // Mantem placa/documento ao trocar de servico; limpa o que e' especifico.
    this.form = { servico: s.id, placa: this.form.placa, documento: this.form.documento, uf: s.precosUf.length ? undefined : this.form.uf };
    this.aceiteAtpve = false;
    this.aviso = null;
    if (s.id === 'atpve') {
      this.api.avisoAtpve().subscribe({
        next: a => this.aviso = a,
        error: err => this.toast.error(err?.error?.error || 'Não consegui carregar o aviso do ATPV-e.')
      });
    }
  }

  consultar(): void {
    if (!this.prontoParaConsultar || this.consultando) return;
    const pedido: PedidoConsulta = { ...this.form, servico: this.servicoId };
    if (this.campos.has('placa')) pedido.placa = this.placaNormalizada;
    if (this.servicoId === 'atpve' && this.aviso) pedido.aceiteAtpveVersao = this.aviso.versao;
    this.consultando = true;
    this.api.consultar(pedido).subscribe({
      next: c => {
        this.consultando = false;
        this.atual = c;
        this.form = { servico: this.servicoId, uf: this.form.uf };
        this.aceiteAtpve = false;
        this.carregar();
        this.acompanhar(c);
        setTimeout(() => document.getElementById('resultado')?.scrollIntoView({ behavior: 'smooth', block: 'start' }));
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
        this.toast.error(c.teste ? 'No ambiente de teste não há documento em PDF.' : 'O documento desta consulta não está disponível.');
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
    this.form.placa = placa;
  }

  quantasCabem(valor: number): string {
    const s = this.painel?.servicos.find(x => x.recomendado) ?? this.painel?.servicos[0];
    if (!s?.preco) return '';
    const n = Math.floor(valor / s.preco);
    return n >= 1 ? `${n} ${n === 1 ? 'consulta completa' : 'consultas completas'}` : '';
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

  alvo(c: ConsultaItem): string {
    return c.placa || c.identificador || '—';
  }

  resultadoCurto(c: ConsultaItem): string {
    if (c.status === 'PROCESSANDO') return 'Consultando...';
    if (c.status === 'ERRO') return c.estornada ? 'Sem resultado (devolvido)' : 'Sem resultado';
    if (!c.veiculo) return 'Concluída';
    if (c.nadaConsta) return 'Nada consta';
    const graves = c.alertas.filter(a => a.grave).length;
    return graves ? `${graves} ${graves === 1 ? 'alerta grave' : 'alertas graves'}` : `${c.alertas.length} ${c.alertas.length === 1 ? 'aviso' : 'avisos'}`;
  }

  classeResultado(c: ConsultaItem): string {
    if (c.status === 'PROCESSANDO') return 'processando';
    if (c.status === 'ERRO') return 'erro';
    if (!c.veiculo || c.nadaConsta) return 'ok';
    return c.alertas.some(a => a.grave) ? 'grave' : 'aviso';
  }
}
