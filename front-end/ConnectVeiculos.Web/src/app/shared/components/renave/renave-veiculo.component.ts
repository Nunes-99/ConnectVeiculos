import { Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Veiculo } from '../../../core/models';
import { ToastService } from '../../../core/services/toast.service';
import { CepService, mesmaCidade } from '../../../core/services/cep.service';
import { NfeService, ORIGEM_SUGESTAO } from '../../../core/services/nfe.service';
import {
  RenaveAptidao, RenaveConfig, RenaveMunicipio, RenaveService, RenaveSituacao, TipoCrv, baixarPdfRenave, erroRenave
} from '../../../core/services/renave.service';

interface Etapa { titulo: string; feita: boolean; detalhe?: string | null; }

/**
 * Janela do Renave de um veiculo: mostra em que etapa ele esta e o
 * formulario da proxima (entrada, assinatura do vendedor, nota de compra,
 * saida, ATPVe, nota de venda), na ordem do manual do Serpro.
 */
@Component({
  selector: 'app-renave-veiculo',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './renave-veiculo.component.html',
  styleUrl: './renave.scss'
})
export class RenaveVeiculoComponent implements OnInit {
  private renave = inject(RenaveService);
  private toast = inject(ToastService);
  private nfe = inject(NfeService);
  private cep = inject(CepService);

  @Input({ required: true }) veiculo!: Veiculo;
  @Output() fechar = new EventEmitter<void>();
  @Output() situacaoChange = new EventEmitter<RenaveSituacao>();

  config: RenaveConfig | null = null;
  situacao: RenaveSituacao | null = null;
  carregando = true;
  enviando = false;

  // Aptidao + entrada
  aptidao: RenaveAptidao | null = null;
  entrada = {
    tipoCrv: 'DIGITAL' as TipoCrv, numeroCrv: '', codigoSegurancaCrv: '',
    documentoVendedor: '', emailVendedor: '', dataCompra: this.hoje(), valorCompra: 0, quilometragemHodometro: 0
  };

  // Assinatura do vendedor
  tipoAssinatura: 'crv-papel' | 'atpve-impresso' = 'crv-papel';
  fotoAssinatura: File | null = null;

  // Notas
  chaveNota = '';

  // Saida
  saida = {
    codigoSegurancaCrv: '', numeroCrv: '', documentoComprador: '', nomeComprador: '', emailComprador: '',
    uf: 'SP', cep: '', logradouro: '', numero: '', complemento: '', bairro: '',
    dataVenda: this.hoje(), valorVenda: 0, chaveNotaFiscal: ''
  };
  buscaMunicipio = '';
  municipios: RenaveMunicipio[] = [];
  municipio: RenaveMunicipio | null = null;
  /** De onde veio o preenchimento automatico do formulario atual, ou null. */
  origemDados: string | null = null;
  private preenchido: string | null = null;

  readonly ufs = ['AC', 'AL', 'AP', 'AM', 'BA', 'CE', 'DF', 'ES', 'GO', 'MA', 'MT', 'MS', 'MG', 'PA', 'PB', 'PR',
    'PE', 'PI', 'RJ', 'RN', 'RS', 'RO', 'RR', 'SP', 'SC', 'SE', 'TO'];

  ngOnInit(): void {
    this.entrada.valorCompra = this.veiculo.veiPrecoCompra || 0;
    this.entrada.quilometragemHodometro = this.veiculo.veiKm || 0;
    this.saida.valorVenda = this.veiculo.veiPreco || 0;
    this.renave.obterConfig().subscribe({ next: c => this.config = c, error: () => {} });
    this.renave.situacao(this.veiculo.veiId).subscribe({
      next: s => { this.aplicar(s); this.carregando = false; },
      error: err => { this.carregando = false; this.toast.error(erroRenave(err)); }
    });
  }

  private hoje(): string {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  private aplicar(s: RenaveSituacao): void {
    this.situacao = s;
    this.chaveNota = '';
    this.situacaoChange.emit(s);
    this.preencher();
  }

  /**
   * Entrada e saida pedem o mesmo vendedor/comprador da NF-e: traz o que o
   * sistema ja sabe (nota emitida, venda registrada) para a loja so conferir.
   */
  private preencher(): void {
    const passo = this.passo;
    if ((passo !== 'entrada' && passo !== 'saida') || this.preenchido === passo) return;
    this.preenchido = passo;
    this.origemDados = null;
    this.nfe.sugestao(this.veiculo.veiId, passo === 'entrada' ? 'ENTRADA' : 'SAIDA').subscribe({
      next: s => {
        // O que vem do proprio Renave ja esta nele; so serve o que veio de fora.
        if (!s.origem || s.origem === 'renave') return;
        const d = s.dados;
        if (passo === 'entrada') {
          this.entrada.documentoVendedor = this.entrada.documentoVendedor || d.documento || '';
          this.entrada.emailVendedor = this.entrada.emailVendedor || d.email || '';
          if (s.origem === 'nota' && d.valor) this.entrada.valorCompra = d.valor;
        } else {
          const e = d.endereco;
          const sd = this.saida;
          sd.documentoComprador = sd.documentoComprador || d.documento || '';
          sd.nomeComprador = sd.nomeComprador || d.nome || '';
          sd.emailComprador = sd.emailComprador || d.email || '';
          if (d.valor) sd.valorVenda = d.valor;
          if (e) {
            sd.cep = e.cep || sd.cep;
            sd.logradouro = e.logradouro || sd.logradouro;
            sd.numero = e.numero || sd.numero;
            sd.complemento = e.complemento || sd.complemento;
            sd.bairro = e.bairro || sd.bairro;
            sd.uf = e.uf || sd.uf;
            if (e.municipio) this.acharMunicipio(e.municipio);
          }
        }
        this.origemDados = ORIGEM_SUGESTAO[s.origem] || null;
      },
      error: () => {}
    });
  }

  /** Escolhe o municipio do Renave pelo nome; sem acerto exato, deixa a lista para a loja. */
  private acharMunicipio(nome: string): void {
    this.municipio = null;
    this.buscaMunicipio = nome;
    this.renave.municipios(this.saida.uf, nome).subscribe({
      next: m => {
        this.municipios = m;
        this.municipio = m.find(x => mesmaCidade(x.nome, nome)) || (m.length === 1 ? m[0] : null);
      },
      error: () => {}
    });
  }

  /** Preenche rua, bairro, UF e municipio do comprador pelo CEP. */
  buscarCep(): void {
    const sd = this.saida;
    if ((sd.cep || '').replace(/\D/g, '').length !== 8) return;
    this.cep.buscar(sd.cep).subscribe(r => {
      if (!r) { this.toast.warning('CEP não encontrado.'); return; }
      sd.cep = r.cep;
      if (r.logradouro) sd.logradouro = r.logradouro;
      if (r.bairro) sd.bairro = r.bairro;
      sd.uf = r.uf || sd.uf;
      if (r.cidade) this.acharMunicipio(r.cidade);
    });
  }

  get emTestes(): boolean {
    return this.config?.ambiente !== 'producao';
  }

  get zeroKm(): boolean {
    return !!this.situacao?.zeroKm;
  }

  get etapas(): Etapa[] {
    const s = this.situacao;
    if (s?.zeroKm) {
      return [
        { titulo: 'Entrada (zero km)', feita: !!s.idEstoque, detalhe: s.dataEntrada },
        { titulo: 'Saída (venda)', feita: !!s.dataSaida, detalhe: s.dataSaida },
        { titulo: 'NF-e de venda', feita: !!s.chaveNfSaida }
      ];
    }
    return [
      { titulo: 'Entrada no estoque', feita: !!s?.idEstoque, detalhe: s?.dataEntrada },
      { titulo: 'Assinatura do vendedor', feita: !!s?.dataAssinaturaVendedor, detalhe: s?.dataAssinaturaVendedor },
      { titulo: 'NF-e de compra', feita: !!s?.chaveNfEntrada },
      { titulo: 'Confirmado pelo Detran', feita: s?.estado === 'CONFIRMADO' || !!s?.dataSaida },
      { titulo: 'Saída (venda)', feita: !!s?.dataSaida, detalhe: s?.dataSaida },
      { titulo: 'NF-e de venda', feita: !!s?.chaveNfSaida }
    ];
  }

  /** Qual formulario mostrar agora. */
  get passo(): 'entrada' | 'assinatura' | 'nota-compra' | 'detran' | 'saida' | 'nota-venda' | 'concluido' {
    const s = this.situacao;
    if (!s?.idEstoque) return 'entrada';
    // Zero km: sem assinatura de vendedor, nota de compra na entrada e sem espera do Detran.
    if (s.zeroKm && !s.dataSaida) return 'saida';
    if (!s.dataSaida) {
      if (!s.dataAssinaturaVendedor) return 'assinatura';
      if (!s.chaveNfEntrada) return 'nota-compra';
      if (s.estado !== 'CONFIRMADO') return 'detran';
      return 'saida';
    }
    return s.chaveNfSaida ? 'concluido' : 'nota-venda';
  }

  // ---------------- Acoes ----------------

  private executar(obs: ReturnType<RenaveService['situacao']>, sucesso: string): void {
    this.enviando = true;
    obs.subscribe({
      next: s => { this.aplicar(s); this.enviando = false; this.toast.success(sucesso); },
      error: err => {
        this.enviando = false;
        this.toast.error(erroRenave(err));
        // A recusa fica gravada no veiculo: recarrega para mostrar.
        this.renave.situacao(this.veiculo.veiId).subscribe(s => this.situacao = s);
      }
    });
  }

  atualizar(): void {
    this.executar(this.renave.atualizar(this.veiculo.veiId), 'Situação atualizada.');
  }

  consultarAptidao(): void {
    this.enviando = true;
    this.aptidao = null;
    this.renave.aptidao(this.veiculo.veiId, this.entrada.tipoCrv, this.entrada.numeroCrv || null).subscribe({
      next: a => { this.aptidao = a; this.enviando = false; },
      error: err => { this.enviando = false; this.toast.error(erroRenave(err)); }
    });
  }

  registrarEntrada(): void {
    this.executar(this.renave.entrada(this.veiculo.veiId, {
      ...this.entrada,
      numeroCrv: this.entrada.numeroCrv || null
    }), 'Entrada registrada no Renave.');
  }

  escolherFoto(event: Event): void {
    this.fotoAssinatura = (event.target as HTMLInputElement).files?.[0] ?? null;
  }

  enviarAssinatura(): void {
    if (!this.fotoAssinatura) return;
    this.executar(this.renave.assinaturaVendedor(this.veiculo.veiId, this.tipoAssinatura, this.fotoAssinatura),
      'Assinatura do vendedor enviada.');
  }

  enviarNota(evento: 'COMPRA' | 'VENDA'): void {
    this.executar(this.renave.notaFiscal(this.veiculo.veiId, evento, this.chaveNota),
      evento === 'COMPRA' ? 'NF-e de compra informada.' : 'NF-e de venda informada.');
  }

  simularDetran(): void {
    this.executar(this.renave.simularDetran(this.veiculo.veiId), 'Detran simulado: entrada confirmada.');
  }

  buscarMunicipios(): void {
    const nome = this.buscaMunicipio.trim();
    if (nome.length < 3) return;
    this.renave.municipios(this.saida.uf, nome).subscribe({
      next: m => { this.municipios = m; if (m.length === 1) this.municipio = m[0]; },
      error: err => this.toast.error(erroRenave(err))
    });
  }

  registrarSaida(): void {
    if (!this.municipio) { this.toast.error('Escolha o município do comprador.'); return; }
    const s = this.saida;
    this.executar(this.renave.saida(this.veiculo.veiId, {
      codigoSegurancaCrv: s.codigoSegurancaCrv,
      numeroCrv: s.numeroCrv || null,
      documentoComprador: s.documentoComprador,
      nomeComprador: s.nomeComprador || null,
      emailComprador: s.emailComprador || null,
      codigoMunicipio: this.municipio.id,
      cep: s.cep || null,
      logradouro: s.logradouro || null,
      numero: s.numero || null,
      complemento: s.complemento || null,
      bairro: s.bairro || null,
      dataVenda: s.dataVenda,
      valorVenda: s.valorVenda,
      chaveNotaFiscal: this.zeroKm ? s.chaveNotaFiscal : null
    }), this.zeroKm ? 'Saída do zero km registrada no Renave.' : 'Saída registrada. O ATPVe do comprador já pode ser baixado.');
  }

  /** O Renave so emite termo para usado; o de entrada, depois da assinatura do vendedor. */
  get podeTermoEntrada(): boolean {
    const s = this.situacao;
    return !!s && !s.zeroKm && !!s.idEstoque && !!s.dataAssinaturaVendedor;
  }

  get podeTermoSaida(): boolean {
    const s = this.situacao;
    return !!s && !s.zeroKm && !!s.dataSaida;
  }

  baixarTermo(tipo: 'entrada' | 'saida'): void {
    this.enviando = true;
    baixarPdfRenave(this.renave.termo(this.veiculo.veiId, tipo), `Termo-${tipo}-${this.veiculo.veiPlaca}.pdf`, erro => {
      this.enviando = false;
      if (erro) this.toast.error(erro);
    });
  }

  baixarAtpv(): void {
    this.enviando = true;
    this.renave.atpv(this.veiculo.veiId).subscribe({
      next: blob => {
        this.enviando = false;
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `ATPVe-${this.veiculo.veiPlaca}.pdf`;
        a.click();
        URL.revokeObjectURL(url);
        this.renave.situacao(this.veiculo.veiId).subscribe(s => this.aplicar(s));
      },
      error: async err => {
        this.enviando = false;
        // Com responseType blob, o JSON de erro chega como Blob.
        let msg = 'Não foi possível baixar o ATPVe.';
        try { msg = JSON.parse(await (err.error as Blob).text()).error || msg; } catch { /* mantem a padrao */ }
        this.toast.error(msg);
      }
    });
  }
}
