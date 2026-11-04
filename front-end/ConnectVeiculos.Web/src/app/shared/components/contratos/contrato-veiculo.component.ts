import { Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { Veiculo } from '../../../core/models';
import {
  Contrato, ContratoParte, ContratoPreview, ContratoService, ContratoSignatario, ContratoSugestao, ModeloContrato,
  ROTULO_STATUS_CONTRATO, baixarBlob, erroContrato, linkAssinatura
} from '../../../core/services/contrato.service';
import { AssinaturaPadComponent } from './assinatura-pad.component';
import { ContratoTextoComponent } from './contrato-texto.component';

type Tela = 'lista' | 'novo' | 'detalhe';
type Coleta = 'cliente' | 'loja' | null;

/**
 * Janela de contratos de um veiculo: lista, gera um contrato a partir de um
 * modelo (com o cliente ja preenchido pela venda ou pelo dono) e cuida das
 * assinaturas (link para o cliente, assinatura na loja e pela loja, PDF).
 */
@Component({
  selector: 'app-contrato-veiculo',
  standalone: true,
  imports: [CommonModule, FormsModule, AssinaturaPadComponent, ContratoTextoComponent],
  templateUrl: './contrato-veiculo.component.html',
  styleUrl: './contratos.scss'
})
export class ContratoVeiculoComponent implements OnInit {
  private api = inject(ContratoService);
  private toast = inject(ToastService);
  private auth = inject(AuthService);

  @Input({ required: true }) veiculo!: Veiculo;
  /** Abre direto num contrato (vindo da tela Contratos). */
  @Input() contratoId: number | null = null;
  @Output() fechar = new EventEmitter<void>();

  readonly rotuloStatus = ROTULO_STATUS_CONTRATO;
  readonly gestao = this.auth.isGerente();
  private readonly tenant = this.auth.getTenantSlug() || 'default';

  tela: Tela = 'lista';
  carregando = true;
  ocupado = false;
  contratos: Contrato[] = [];
  modelos: ModeloContrato[] = [];

  // Novo contrato
  modeloId: number | null = null;
  sugestao: ContratoSugestao | null = null;
  parte: ContratoParte = this.parteVazia();
  valor: number | null = null;
  formaPagamento = '';
  extras: Record<string, string> = {};
  preview: ContratoPreview | null = null;

  // Detalhe
  contrato: Contrato | null = null;
  texto: ContratoPreview | null = null;
  mostrarTexto = false;
  coleta: Coleta = null;
  assinaNome = '';
  assinaDocumento = '';
  assinaImagem: string | null = null;
  assinaAceite = false;

  ngOnInit(): void {
    this.carregar();
    this.api.modelos().subscribe({ next: m => this.modelos = m, error: () => {} });
  }

  get titulo(): string {
    return `${this.veiculo.veiMarca} ${this.veiculo.veiModelo} ${this.veiculo.veiAno}${this.veiculo.veiPlaca ? ' · ' + this.veiculo.veiPlaca : ''}`;
  }

  carregar(): void {
    this.carregando = true;
    this.api.listar({ veiculoId: this.veiculo.veiId }).subscribe({
      next: c => {
        this.contratos = c;
        this.carregando = false;
        if (this.contratoId) {
          const alvo = c.find(x => x.id === this.contratoId);
          this.contratoId = null;
          if (alvo) this.abrir(alvo);
        }
      },
      error: async err => { this.carregando = false; this.toast.error(await erroContrato(err, 'Não foi possível carregar os contratos.')); }
    });
  }

  // ---------- Novo ----------

  novo(): void {
    this.tela = 'novo';
    this.modeloId = null;
    this.sugestao = null;
    this.preview = null;
    this.parte = this.parteVazia();
    this.valor = null;
    this.formaPagamento = '';
    this.extras = {};
  }

  escolherModelo(id: number | null): void {
    this.modeloId = id;
    this.preview = null;
    if (!id) { this.sugestao = null; return; }
    this.api.sugestao(this.veiculo.veiId, id).subscribe({
      next: s => {
        this.sugestao = s;
        this.parte = { ...this.parteVazia(), ...s.parte };
        this.valor = s.valor;
        this.formaPagamento = s.formaPagamento || '';
        const extras: Record<string, string> = {};
        for (const e of s.extras) extras[e.chave] = this.extras[e.chave] || '';
        this.extras = extras;
      },
      error: async err => this.toast.error(await erroContrato(err, 'Não foi possível preparar o contrato.'))
    });
  }

  verPrevia(): void {
    if (!this.modeloId) return;
    this.ocupado = true;
    this.api.preview(this.dadosGerar()).subscribe({
      next: p => { this.ocupado = false; this.preview = p; },
      error: async err => { this.ocupado = false; this.toast.error(await erroContrato(err, 'Não foi possível montar a prévia.')); }
    });
  }

  gerar(): void {
    if (!this.modeloId) return;
    this.ocupado = true;
    this.api.gerar(this.dadosGerar()).subscribe({
      next: c => {
        this.ocupado = false;
        this.toast.success('Documento gerado. Agora é só colher as assinaturas.');
        this.contratos = [c, ...this.contratos];
        this.abrir(c);
      },
      error: async err => { this.ocupado = false; this.toast.error(await erroContrato(err, 'Não foi possível gerar o documento.')); }
    });
  }

  rotuloFaltando(chave: string): string {
    const nomes: Record<string, string> = {
      'cliente.documento': 'CPF/CNPJ do cliente', 'cliente.endereco': 'endereço do cliente', 'cliente.email': 'e-mail do cliente',
      'cliente.telefone': 'telefone do cliente', 'veiculo.renavam': 'Renavam', 'veiculo.chassi': 'chassi', 'veiculo.placa': 'placa',
      'contrato.forma_pagamento': 'forma de pagamento', 'contrato.valor': 'valor', 'loja.cnpj': 'CNPJ da loja', 'loja.endereco': 'endereço da loja'
    };
    return nomes[chave] || chave.replace('extra.', '').replace(/_/g, ' ');
  }

  // ---------- Detalhe ----------

  abrir(c: Contrato): void {
    this.contrato = c;
    this.tela = 'detalhe';
    this.texto = null;
    this.mostrarTexto = false;
    this.coleta = null;
  }

  voltar(): void {
    this.tela = 'lista';
    this.contrato = null;
    this.carregar();
  }

  verTexto(): void {
    if (!this.contrato) return;
    this.mostrarTexto = !this.mostrarTexto;
    if (this.mostrarTexto && !this.texto) {
      this.api.texto(this.contrato.id).subscribe({ next: t => this.texto = t, error: () => {} });
    }
  }

  get cliente(): ContratoSignatario | undefined { return this.contrato?.signatarios.find(s => s.papel === 'CLIENTE'); }
  get loja(): ContratoSignatario | undefined { return this.contrato?.signatarios.find(s => s.papel === 'LOJA'); }

  link(s: ContratoSignatario): string {
    return s.token ? linkAssinatura(this.tenant, s.token) : '';
  }

  async copiarLink(s: ContratoSignatario): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.link(s));
      this.toast.success('Link copiado.');
    } catch {
      this.toast.error('Não foi possível copiar. Selecione o link e copie.');
    }
  }

  whatsapp(s: ContratoSignatario): void {
    const tel = (s.telefone || '').replace(/\D/g, '');
    const numero = tel.length >= 10 && !tel.startsWith('55') ? '55' + tel : tel;
    const msg = `Olá, ${s.nome.split(' ')[0]}! Segue o documento "${this.contrato?.titulo}" para você conferir e assinar: ${this.link(s)}`;
    window.open(`https://wa.me/${numero}?text=${encodeURIComponent(msg)}`, '_blank', 'noopener');
  }

  enviarEmail(): void {
    if (!this.contrato) return;
    this.ocupado = true;
    this.api.enviarEmail(this.contrato.id).subscribe({
      next: () => { this.ocupado = false; this.toast.success(`Link enviado para ${this.cliente?.email}.`); },
      error: async err => { this.ocupado = false; this.toast.error(await erroContrato(err, 'Não foi possível enviar o e-mail.')); }
    });
  }

  colher(quem: Coleta): void {
    this.coleta = quem;
    this.assinaImagem = null;
    this.assinaAceite = false;
    const s = quem === 'cliente' ? this.cliente : null;
    this.assinaNome = s?.nome || (quem === 'loja' ? this.auth.currentUser()?.usuNome || '' : '');
    this.assinaDocumento = s?.documento || '';
  }

  confirmarAssinatura(): void {
    if (!this.contrato || !this.coleta || !this.assinaImagem) return;
    const dados = { nome: this.assinaNome, documento: this.assinaDocumento || null, imagem: this.assinaImagem, aceite: this.assinaAceite };
    const req = this.coleta === 'loja' ? this.api.assinarLoja(this.contrato.id, { ...dados, aceite: true }) : this.api.assinarPresencial(this.contrato.id, dados);
    this.ocupado = true;
    req.subscribe({
      next: c => {
        this.ocupado = false;
        this.coleta = null;
        this.contrato = c;
        this.toast.success(c.status === 'ASSINADO' ? 'Assinado por todos. O PDF final está pronto.' : 'Assinatura registrada.');
      },
      error: async err => { this.ocupado = false; this.toast.error(await erroContrato(err, 'Não foi possível registrar a assinatura.')); }
    });
  }

  baixarPdf(): void {
    if (!this.contrato) return;
    const c = this.contrato;
    this.api.pdf(c.id).subscribe({
      next: b => baixarBlob(b, `contrato-${c.id}.pdf`),
      error: async err => this.toast.error(await erroContrato(err, 'Não foi possível gerar o PDF.'))
    });
  }

  cancelar(): void {
    if (!this.contrato || !confirm('Cancelar este documento? O link de assinatura deixa de funcionar.')) return;
    this.ocupado = true;
    this.api.cancelar(this.contrato.id).subscribe({
      next: c => { this.ocupado = false; this.contrato = c; this.toast.success('Documento cancelado.'); },
      error: async err => { this.ocupado = false; this.toast.error(await erroContrato(err, 'Não foi possível cancelar.')); }
    });
  }

  rotuloMetodo(m: string | null): string {
    return ({ CODIGO_EMAIL: 'pelo link, com código por e-mail', PRESENCIAL: 'na loja', USUARIO_SISTEMA: 'no sistema' } as Record<string, string>)[m || ''] || '';
  }

  private dadosGerar() {
    return { modeloId: this.modeloId!, veiculoId: this.veiculo.veiId, parte: this.parte, valor: this.valor, formaPagamento: this.formaPagamento || null, extras: this.extras };
  }

  private parteVazia(): ContratoParte {
    return { nome: '', documento: null, email: null, telefone: null, endereco: null };
  }
}
