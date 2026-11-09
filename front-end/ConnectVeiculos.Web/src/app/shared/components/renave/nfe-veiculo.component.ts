import { Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Veiculo } from '../../../core/models';
import { ToastService } from '../../../core/services/toast.service';
import { CepService } from '../../../core/services/cep.service';
import {
  MEIOS_PAGAMENTO_NFE, NfeConfig, NfeEmissao, NfeMunicipio, NfeResumo, NfeService, ORIGEM_SUGESTAO, baixarArquivo, erroApi
} from '../../../core/services/nfe.service';

/**
 * Notas fiscais de um veiculo: lista (com DANFE, XML, reenvio e cancelamento)
 * e o formulario para emitir a NF-e de compra ou a de venda.
 */
@Component({
  selector: 'app-nfe-veiculo',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './nfe-veiculo.component.html',
  styleUrl: './renave.scss'
})
export class NfeVeiculoComponent implements OnInit {
  private nfe = inject(NfeService);
  private toast = inject(ToastService);
  private cep = inject(CepService);

  @Input({ required: true }) veiculo!: Veiculo;
  @Output() fechar = new EventEmitter<void>();

  readonly meios = MEIOS_PAGAMENTO_NFE;
  readonly ufs = ['AC', 'AL', 'AP', 'AM', 'BA', 'CE', 'DF', 'ES', 'GO', 'MA', 'MT', 'MS', 'MG', 'PA', 'PB', 'PR',
    'PE', 'PI', 'RJ', 'RN', 'RS', 'RO', 'RR', 'SP', 'SC', 'SE', 'TO'];

  config: NfeConfig | null = null;
  notas: NfeResumo[] = [];
  carregando = true;
  enviando = false;

  mostrarFormulario = false;
  /** De onde veio o preenchimento automatico (texto para a tela), ou null. */
  origemDados: string | null = null;
  buscandoCep = false;
  form: NfeEmissao = this.formVazio('SAIDA');
  buscaMunicipio = '';
  municipios: NfeMunicipio[] = [];

  cancelando: NfeResumo | null = null;
  justificativa = '';

  corrigindo: NfeResumo | null = null;
  devolvendo: NfeResumo | null = null;
  enviandoEmail: NfeResumo | null = null;
  emailDestino = '';
  correcao = '';

  ngOnInit(): void {
    this.nfe.obterConfig().subscribe({
      next: c => { this.config = c; this.form.endereco.uf = c.uf || 'SP'; },
      error: () => {}
    });
    this.recarregar();
  }

  private formVazio(tipo: 'ENTRADA' | 'SAIDA'): NfeEmissao {
    return {
      tipo,
      documento: '', nome: '', inscricaoEstadual: null, email: null, telefone: null,
      endereco: { logradouro: '', numero: '', complemento: '', bairro: '', cep: '', codigoMunicipio: 0, municipio: '', uf: this.config?.uf || 'SP' },
      valor: tipo === 'SAIDA' ? (this.veiculo?.veiPreco || 0) : (this.veiculo?.veiPrecoCompra || 0),
      ncm: null,
      meioPagamento: tipo === 'SAIDA' ? '17' : '90',
      informacoesComplementares: null,
      indicadorIe: null,
      cest: null,
      aliquotaInternaDestino: null,
      percentualFcpDestino: null,
      veiculoNovo: null
    };
  }

  /** Cores da tabela da Denatran (grupo veicProd da NF-e). */
  readonly coresDenatran = [
    { codigo: '01', nome: 'Amarelo' }, { codigo: '02', nome: 'Azul' }, { codigo: '03', nome: 'Bege' },
    { codigo: '04', nome: 'Branca' }, { codigo: '05', nome: 'Cinza' }, { codigo: '06', nome: 'Dourada' },
    { codigo: '07', nome: 'Grená' }, { codigo: '08', nome: 'Laranja' }, { codigo: '09', nome: 'Marrom' },
    { codigo: '10', nome: 'Prata' }, { codigo: '11', nome: 'Preta' }, { codigo: '12', nome: 'Rosa' },
    { codigo: '13', nome: 'Roxa' }, { codigo: '14', nome: 'Verde' }, { codigo: '15', nome: 'Vermelha' },
    { codigo: '16', nome: 'Fantasia' }
  ];

  /** Situação no ICMS que vai na nota: a escolhida ou, sem escolha, pela inscrição. */
  get situacaoIe(): 'contribuinte' | 'isento' | 'nao_contribuinte' {
    return this.form.indicadorIe || (this.form.inscricaoEstadual?.trim() ? 'contribuinte' : 'nao_contribuinte');
  }

  /**
   * DIFAL (EC 87/2015): venda para consumidor final não contribuinte de outro
   * estado, por loja do regime normal. Sem a alíquota interna do destino a
   * Sefaz rejeita a nota.
   */
  get precisaDifal(): boolean {
    return this.form.tipo === 'SAIDA' && !!this.config && (this.config.crt === 2 || this.config.crt === 3)
      && !!this.form.endereco.uf && this.form.endereco.uf !== this.config.uf
      && this.situacaoIe === 'nao_contribuinte';
  }

  /** Zero km: a Sefaz exige o detalhamento do veículo, com os dados da nota da montadora. */
  alternarVeiculoNovo(ligado: boolean): void {
    if (!ligado) { this.form.veiculoNovo = null; return; }
    const v = this.veiculo;
    const ano = Number(v?.veiAno) || new Date().getFullYear();
    this.form.veiculoNovo = {
      tipoOperacao: 1, chassi: v?.veiChassi || '', codigoCor: '', descricaoCor: v?.veiCor || '',
      potencia: '', cilindradas: '', pesoLiquido: '', pesoBruto: '', numeroSerie: '', tipoCombustivel: '',
      numeroMotor: '', capacidadeTracao: '', distanciaEixos: '', anoModelo: ano, anoFabricacao: ano,
      tipoPintura: '', tipoVeiculo: '06', especieVeiculo: '1', vinRemarcado: false, condicao: 1,
      codigoMarcaModelo: 0, corDenatran: '', lotacao: 5, restricao: 0
    };
  }

  recarregar(): void {
    this.nfe.doVeiculo(this.veiculo.veiId).subscribe({
      next: n => { this.notas = n; this.carregando = false; },
      error: async err => { this.carregando = false; this.toast.error(await erroApi(err, 'Não foi possível carregar as notas.')); }
    });
  }

  tem(tipo: 'ENTRADA' | 'SAIDA'): boolean {
    return this.notas.some(n => n.tipo === tipo && n.status === 'AUTORIZADA');
  }

  novaNota(tipo: 'ENTRADA' | 'SAIDA'): void {
    this.form = this.formVazio(tipo);
    this.municipios = [];
    this.buscaMunicipio = '';
    this.origemDados = null;
    this.mostrarFormulario = true;

    // Traz o que o sistema ja sabe (nota anterior, venda registrada, Renave).
    this.nfe.sugestao(this.veiculo.veiId, tipo).subscribe({
      next: s => {
        if (!s.origem) return;
        const vazio = this.formVazio(tipo);
        const d = s.dados;
        const e = d.endereco || vazio.endereco;
        this.form = {
          ...vazio,
          documento: d.documento || '',
          nome: d.nome || '',
          inscricaoEstadual: d.inscricaoEstadual || null,
          email: d.email || null,
          telefone: d.telefone || null,
          valor: d.valor || vazio.valor,
          ncm: d.ncm || null,
          meioPagamento: d.meioPagamento || vazio.meioPagamento,
          informacoesComplementares: d.informacoesComplementares || null,
          indicadorIe: d.indicadorIe || null,
          cest: d.cest || null,
          aliquotaInternaDestino: d.aliquotaInternaDestino ?? null,
          percentualFcpDestino: d.percentualFcpDestino ?? null,
          veiculoNovo: d.veiculoNovo || null,
          endereco: {
            logradouro: e.logradouro || '', numero: e.numero || '', complemento: e.complemento || '',
            bairro: e.bairro || '', cep: e.cep || '', codigoMunicipio: e.codigoMunicipio || 0,
            municipio: e.municipio || '', uf: e.uf || vazio.endereco.uf
          }
        };
        // Cidade sem codigo IBGE: deixa o nome na busca para a loja so confirmar.
        if (!this.form.endereco.codigoMunicipio && this.form.endereco.municipio) {
          this.buscaMunicipio = this.form.endereco.municipio;
          this.buscarMunicipios();
        }
        this.origemDados = ORIGEM_SUGESTAO[s.origem] || null;
      },
      error: () => {}
    });
  }

  /** Preenche rua, bairro, UF e municipio pelo CEP. */
  buscarCep(): void {
    const e = this.form.endereco;
    if ((e.cep || '').replace(/\D/g, '').length !== 8) return;
    this.buscandoCep = true;
    this.cep.buscar(e.cep).subscribe(r => {
      this.buscandoCep = false;
      if (!r) { this.toast.warning('CEP não encontrado.'); return; }
      e.cep = r.cep;
      if (r.logradouro) e.logradouro = r.logradouro;
      if (r.bairro) e.bairro = r.bairro;
      e.uf = r.uf || e.uf;
      e.codigoMunicipio = r.codigoMunicipio;
      e.municipio = r.cidade;
    });
  }

  buscarMunicipios(): void {
    const nome = this.buscaMunicipio.trim();
    if (nome.length < 3) return;
    this.nfe.municipios(this.form.endereco.uf, nome).subscribe({
      next: m => { this.municipios = m; if (m.length === 1) this.escolherMunicipio(m[0]); },
      error: async err => this.toast.error(await erroApi(err, 'Não foi possível buscar o município.'))
    });
  }

  escolherMunicipio(m: NfeMunicipio | undefined): void {
    if (!m) return;
    this.form.endereco.codigoMunicipio = m.codigo;
    this.form.endereco.municipio = m.nome;
    this.municipios = [];
    this.buscaMunicipio = '';
  }

  emitir(): void {
    if (!this.form.endereco.codigoMunicipio) { this.toast.error('Escolha o município.'); return; }
    this.enviando = true;
    this.nfe.emitir(this.veiculo.veiId, this.form).subscribe({
      next: n => {
        this.enviando = false;
        this.mostrarFormulario = false;
        this.toast.success(`NF-e nº ${n.numero} autorizada.`);
        if (n.motivo?.includes('Renave')) this.toast.error(n.motivo);
        this.recarregar();
      },
      error: async err => {
        this.enviando = false;
        this.toast.error(await erroApi(err, 'Não foi possível emitir a nota.'));
        this.recarregar();
      }
    });
  }

  reenviar(n: NfeResumo): void {
    this.enviando = true;
    this.nfe.reenviar(n.id, null).subscribe({
      next: r => { this.enviando = false; this.toast.success(`NF-e nº ${r.numero} autorizada.`); this.recarregar(); },
      error: async err => { this.enviando = false; this.toast.error(await erroApi(err, 'A nota não foi autorizada.')); this.recarregar(); }
    });
  }

  consultar(n: NfeResumo): void {
    this.enviando = true;
    this.nfe.consultar(n.id).subscribe({
      next: r => {
        this.enviando = false;
        if (r.status === 'AUTORIZADA') this.toast.success(`A Sefaz já tinha autorizado a NF-e nº ${r.numero}. XML e DANFE prontos.`);
        else this.toast.info('A nota não chegou à Sefaz. Use "Enviar de novo".');
        this.recarregar();
      },
      error: async err => { this.enviando = false; this.toast.error(await erroApi(err, 'Não foi possível consultar a Sefaz.')); this.recarregar(); }
    });
  }

  rotuloTipo(n: NfeResumo): string {
    return n.tipo === 'ENTRADA' ? 'Compra' : n.tipo === 'DEVOLUCAO' ? 'Devolução' : 'Venda';
  }

  abrirDevolucao(n: NfeResumo): void {
    this.devolvendo = n;
    this.cancelando = null;
    this.corrigindo = null;
    this.enviandoEmail = null;
  }

  confirmarDevolucao(): void {
    if (!this.devolvendo) return;
    this.enviando = true;
    this.nfe.devolver(this.devolvendo.id).subscribe({
      next: r => {
        this.enviando = false;
        this.devolvendo = null;
        this.toast.success(`Devolução autorizada (NF-e nº ${r.numero}).`);
        if (r.motivo?.includes('e-mail')) this.toast.warning(r.motivo);
        this.recarregar();
      },
      error: async err => {
        this.enviando = false;
        this.toast.error(await erroApi(err, 'A devolução não foi autorizada.'));
        this.recarregar();
      }
    });
  }

  abrirEmail(n: NfeResumo): void {
    this.enviandoEmail = n;
    this.emailDestino = n.emailEnviadoPara || n.emailCliente || '';
    this.cancelando = null;
    this.corrigindo = null;
    this.devolvendo = null;
  }

  confirmarEmail(): void {
    if (!this.enviandoEmail) return;
    this.enviando = true;
    this.nfe.enviarEmail(this.enviandoEmail.id, this.emailDestino.trim()).subscribe({
      next: r => {
        this.enviando = false;
        this.enviandoEmail = null;
        this.toast.success(`XML e DANFE enviados para ${r.emailEnviadoPara}.`);
        this.recarregar();
      },
      error: async err => { this.enviando = false; this.toast.error(await erroApi(err, 'Não foi possível enviar o e-mail.')); }
    });
  }

  confirmarCancelamento(): void {
    if (!this.cancelando) return;
    this.enviando = true;
    this.nfe.cancelar(this.cancelando.id, this.justificativa).subscribe({
      next: () => {
        this.enviando = false; this.cancelando = null; this.justificativa = '';
        this.toast.success('NF-e cancelada.');
        this.recarregar();
      },
      error: async err => { this.enviando = false; this.toast.error(await erroApi(err, 'Não foi possível cancelar.')); }
    });
  }

  abrirCorrecao(n: NfeResumo): void {
    this.cancelando = null;
    this.corrigindo = n;
    // Cada carta substitui a anterior: comeca pelo texto que ja vale.
    this.correcao = n.cartaCorrecaoTexto || '';
  }

  confirmarCorrecao(): void {
    if (!this.corrigindo) return;
    this.enviando = true;
    this.nfe.corrigir(this.corrigindo.id, this.correcao).subscribe({
      next: r => {
        this.enviando = false; this.corrigindo = null; this.correcao = '';
        this.toast.success(`Carta de correção nº ${r.cartaCorrecaoSequencia} registrada.`);
        this.recarregar();
      },
      error: async err => { this.enviando = false; this.toast.error(await erroApi(err, 'Não foi possível registrar a carta de correção.')); }
    });
  }

  baixarCartaCorrecao(n: NfeResumo): void {
    this.nfe.xmlCartaCorrecao(n.id).subscribe({
      next: b => baixarArquivo(b, `${n.chave}-cce-${n.cartaCorrecaoSequencia}.xml`),
      error: async err => this.toast.error(await erroApi(err, 'Não foi possível baixar a carta de correção.'))
    });
  }

  baixarDanfe(n: NfeResumo): void {
    this.nfe.danfe(n.id).subscribe({
      next: b => baixarArquivo(b, `DANFE-${n.numero}.pdf`),
      error: async err => this.toast.error(await erroApi(err, 'Não foi possível gerar o DANFE.'))
    });
  }

  baixarXml(n: NfeResumo): void {
    this.nfe.xml(n.id).subscribe({
      next: b => baixarArquivo(b, `${n.chave}-procNFe.xml`),
      error: async err => this.toast.error(await erroApi(err, 'Não foi possível baixar o XML.'))
    });
  }

  rotuloStatus(s: string): string {
    return ({ PENDENTE: 'Pendente', AUTORIZADA: 'Autorizada', REJEITADA: 'Rejeitada', CANCELADA: 'Cancelada', INUTILIZADA: 'Inutilizada' } as Record<string, string>)[s] || s;
  }
}
