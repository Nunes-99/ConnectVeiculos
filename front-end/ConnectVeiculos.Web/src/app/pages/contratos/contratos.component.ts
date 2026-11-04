import { Component, ElementRef, OnInit, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService, ToastService, VeiculoService } from '../../core/services';
import { Veiculo } from '../../core/models';
import {
  Contrato, ContratoPreview, ContratoService, ContratoVariavel, ModeloContrato, ROTULO_STATUS_CONTRATO, TIPOS_CONTRATO, TipoContrato,
  erroContrato
} from '../../core/services/contrato.service';
import { ContratoVeiculoComponent } from '../../shared/components/contratos/contrato-veiculo.component';
import { ContratoTextoComponent } from '../../shared/components/contratos/contrato-texto.component';

interface ModeloEdicao {
  id: number | null;
  nome: string;
  descricao: string;
  tipo: TipoContrato;
  conteudo: string;
  ativo: boolean;
  padrao: boolean;
}

/**
 * Contratos da loja: todos os documentos gerados (com a situacao das
 * assinaturas) e os modelos, com editor de texto, variaveis e previa com os
 * dados de um veiculo de verdade.
 */
@Component({
  selector: 'app-contratos',
  standalone: true,
  imports: [CommonModule, FormsModule, ContratoVeiculoComponent, ContratoTextoComponent],
  templateUrl: './contratos.component.html',
  styleUrl: './contratos.component.scss'
})
export class ContratosComponent implements OnInit {
  private api = inject(ContratoService);
  private veiculoService = inject(VeiculoService);
  private toast = inject(ToastService);
  private auth = inject(AuthService);

  @ViewChild('editor') editorRef?: ElementRef<HTMLTextAreaElement>;

  readonly tipos = TIPOS_CONTRATO;
  readonly rotuloStatus = ROTULO_STATUS_CONTRATO;
  readonly gestao = this.auth.isGerente();

  aba: 'documentos' | 'modelos' = 'documentos';

  // Documentos
  carregando = true;
  contratos: Contrato[] = [];
  status = '';
  busca = '';
  abrindo: number | null = null;
  veiculoAberto: Veiculo | null = null;
  contratoAberto: number | null = null;

  // Modelos
  modelos: ModeloContrato[] = [];
  variaveis: ContratoVariavel[] = [];
  edicao: ModeloEdicao | null = null;
  salvando = false;
  veiculos: Veiculo[] = [];
  veiculoPrevia: number | null = null;
  previa: ContratoPreview | null = null;

  ngOnInit(): void {
    this.buscar();
  }

  trocarAba(aba: 'documentos' | 'modelos'): void {
    this.aba = aba;
    if (aba === 'modelos' && !this.modelos.length) this.carregarModelos();
    if (aba === 'documentos') this.buscar();
  }

  // ---------- Documentos ----------

  buscar(): void {
    this.carregando = true;
    this.api.listar({ status: this.status, busca: this.busca }).subscribe({
      next: c => { this.contratos = c; this.carregando = false; },
      error: async err => { this.carregando = false; this.toast.error(await erroContrato(err, 'Não foi possível carregar os documentos.')); }
    });
  }

  pendentes(c: Contrato): string {
    const falta = c.signatarios.filter(s => s.status !== 'ASSINADO').map(s => s.papelRotulo.toLowerCase());
    return falta.length ? 'falta: ' + falta.join(' e ') : 'todos assinaram';
  }

  cliente(c: Contrato): string {
    return c.signatarios.find(s => s.papel === 'CLIENTE')?.nome || '—';
  }

  abrir(c: Contrato): void {
    this.abrindo = c.id;
    this.veiculoService.getById(c.veiculoId).subscribe({
      next: v => { this.abrindo = null; this.contratoAberto = c.id; this.veiculoAberto = v; },
      error: () => { this.abrindo = null; this.toast.error('O veículo deste documento não existe mais.'); }
    });
  }

  fecharVeiculo(): void {
    this.veiculoAberto = null;
    this.contratoAberto = null;
    this.buscar();
  }

  // ---------- Modelos ----------

  carregarModelos(): void {
    this.api.modelos(true).subscribe({
      next: m => this.modelos = m,
      error: async err => this.toast.error(await erroContrato(err, 'Não foi possível carregar os modelos.'))
    });
    if (!this.variaveis.length) this.api.variaveis().subscribe({ next: v => this.variaveis = v, error: () => {} });
    if (!this.veiculos.length) this.veiculoService.getAll().subscribe({ next: v => this.veiculos = v, error: () => {} });
  }

  get gruposVariaveis(): { grupo: string; itens: ContratoVariavel[] }[] {
    const grupos: { grupo: string; itens: ContratoVariavel[] }[] = [];
    for (const v of this.variaveis) {
      let g = grupos.find(x => x.grupo === v.grupo);
      if (!g) grupos.push(g = { grupo: v.grupo, itens: [] });
      g.itens.push(v);
    }
    return grupos;
  }

  nomeTipo(t: string): string {
    return this.tipos.find(x => x.valor === t)?.nome || t;
  }

  novoModelo(base?: ModeloContrato): void {
    this.previa = null;
    this.edicao = base
      ? { id: null, nome: base.nome + ' (cópia)', descricao: base.descricao || '', tipo: base.tipo, conteudo: base.conteudo, ativo: true, padrao: false }
      : { id: null, nome: '', descricao: '', tipo: 'OUTRO', conteudo: '# TÍTULO DO DOCUMENTO\n\nEscreva o texto aqui. Use {{cliente.nome}}, {{veiculo.placa}} e as outras variáveis ao lado.\n', ativo: true, padrao: false };
  }

  editar(m: ModeloContrato): void {
    this.previa = null;
    this.edicao = { id: m.id, nome: m.nome, descricao: m.descricao || '', tipo: m.tipo, conteudo: m.conteudo, ativo: m.ativo, padrao: m.padrao };
  }

  /** Insere a variavel onde esta o cursor do editor. */
  inserir(v: ContratoVariavel): void {
    if (!this.edicao) return;
    const chave = v.chave === 'extra.nome_do_campo' ? 'extra.' + (prompt('Nome do campo livre (ex.: garantia_dias):') || '').trim().toLowerCase().replace(/[^a-z0-9_]+/g, '_') : v.chave;
    if (chave === 'extra.') return;
    const texto = `{{${chave}}}`;
    const ta = this.editorRef?.nativeElement;
    if (!ta) { this.edicao.conteudo += texto; return; }
    const ini = ta.selectionStart ?? this.edicao.conteudo.length;
    const fim = ta.selectionEnd ?? ini;
    this.edicao.conteudo = this.edicao.conteudo.slice(0, ini) + texto + this.edicao.conteudo.slice(fim);
    setTimeout(() => { ta.focus(); ta.selectionStart = ta.selectionEnd = ini + texto.length; });
  }

  verPrevia(): void {
    if (!this.edicao) return;
    this.api.previewModelo(this.edicao.conteudo, this.edicao.tipo, this.veiculoPrevia).subscribe({
      next: p => this.previa = p,
      error: async err => this.toast.error(await erroContrato(err, 'Não foi possível montar a prévia.'))
    });
  }

  salvar(): void {
    if (!this.edicao) return;
    const e = this.edicao;
    this.salvando = true;
    this.api.salvarModelo(e.id, { nome: e.nome, descricao: e.descricao, tipo: e.tipo, conteudo: e.conteudo, ativo: e.ativo }).subscribe({
      next: () => { this.salvando = false; this.edicao = null; this.toast.success('Modelo salvo.'); this.carregarModelos(); },
      error: async err => { this.salvando = false; this.toast.error(await erroContrato(err, 'Não foi possível salvar o modelo.')); }
    });
  }

  excluir(m: ModeloContrato): void {
    const msg = m.padrao ? `Desativar o modelo "${m.nome}"? Ele sai da lista de documentos (dá para reativar editando).` : `Excluir o modelo "${m.nome}"?`;
    if (!confirm(msg)) return;
    this.api.excluirModelo(m.id).subscribe({
      next: () => { this.toast.success(m.padrao ? 'Modelo desativado.' : 'Modelo excluído.'); this.carregarModelos(); },
      error: async err => this.toast.error(await erroContrato(err, 'Não foi possível excluir.'))
    });
  }
}
