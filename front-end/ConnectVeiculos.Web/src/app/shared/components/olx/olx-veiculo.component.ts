import { Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { Veiculo } from '../../../core/models';
import { ToastService } from '../../../core/services/toast.service';
import {
  OlxAnuncioDados, OlxAtributos, OlxItem, OlxService, OlxVeiculoInfo, erroOlx
} from '../../../core/services/olx.service';

/**
 * Janela "OLX" de um veiculo: confere marca/modelo/versao do catalogo da OLX
 * e os codigos que o sistema sugeriu a partir do cadastro, e anuncia,
 * atualiza ou tira do ar. A OLX processa o envio em segundo plano: o
 * resultado (no ar, recusado, link) aparece ao abrir a janela de novo.
 */
@Component({
  selector: 'app-olx-veiculo',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './olx-veiculo.component.html',
  styleUrl: '../napista/napista.scss'
})
export class OlxVeiculoComponent implements OnInit {
  private olx = inject(OlxService);
  private toast = inject(ToastService);

  @Input({ required: true }) veiculo!: Veiculo;
  @Output() fechar = new EventEmitter<void>();

  info: OlxVeiculoInfo | null = null;
  dados: OlxAnuncioDados | null = null;
  erroCarga: string | null = null;
  ocupado = false;
  confirmarPausa = false;

  marcas: OlxItem[] = [];
  modelos: OlxItem[] = [];
  versoes: OlxItem[] = [];
  atributos: OlxAtributos = { combustiveis: [], cambios: [], cores: [], portas: [], tipos: [], direcoes: [], potencias: [], opcionais: [] };

  get disponivel(): boolean { return this.veiculo.veiSts === 'D'; }

  /** Texto da situacao do ultimo envio na OLX. */
  get situacao(): string {
    switch (this.info?.situacao) {
      case 'pending': case 'queued': return 'A OLX está processando o envio (pode levar alguns minutos). Abra esta janela de novo para ver o resultado.';
      case 'accepted': return this.info?.publicado ? 'No ar na OLX.' : 'Retirado da OLX.';
      case 'refused': case 'error': return 'A OLX recusou o último envio.';
      default: return '';
    }
  }

  ngOnInit(): void {
    forkJoin({ info: this.olx.veiculo(this.veiculo.veiId), atributos: this.olx.atributos() }).subscribe({
      next: r => {
        this.atributos = r.atributos;
        this.aplicar(r.info);
        this.carregarMarcas();
      },
      error: err => this.erroCarga = erroOlx(err)
    });
  }

  marcaMudou(): void {
    if (!this.dados) return;
    this.dados.marcaNome = this.nome(this.marcas, this.dados.marcaId);
    Object.assign(this.dados, { modeloId: null, modeloNome: null, versaoId: null, versaoNome: null });
    this.modelos = [];
    this.versoes = [];
    if (this.dados.marcaId) this.carregarModelos(false);
  }

  modeloMudou(): void {
    if (!this.dados) return;
    this.dados.modeloNome = this.nome(this.modelos, this.dados.modeloId);
    Object.assign(this.dados, { versaoId: null, versaoNome: null });
    this.versoes = [];
    if (this.dados.modeloId) this.carregarVersoes();
  }

  versaoMudou(): void {
    if (this.dados) this.dados.versaoNome = this.nome(this.versoes, this.dados.versaoId);
  }

  temOpcional(id: string): boolean {
    return !!this.dados?.opcionais.includes(id);
  }

  alternarOpcional(id: string): void {
    if (!this.dados) return;
    this.dados.opcionais = this.temOpcional(id) ? this.dados.opcionais.filter(o => o !== id) : [...this.dados.opcionais, id];
  }

  publicar(): void {
    if (!this.dados) return;
    this.ocupado = true;
    this.olx.publicar(this.veiculo.veiId, this.dados).subscribe({
      next: info => {
        this.ocupado = false;
        this.aplicar(info);
        this.toast.success('Anúncio enviado para a OLX. Ela processa em alguns minutos.');
      },
      error: err => { this.ocupado = false; this.toast.error(erroOlx(err, 'A OLX não aceitou o anúncio.')); this.recarregarInfo(); }
    });
  }

  /** Primeiro clique pede confirmacao no proprio botao; o segundo tira do ar. */
  pausar(): void {
    if (!this.confirmarPausa) { this.confirmarPausa = true; return; }
    this.confirmarPausa = false;
    this.ocupado = true;
    this.olx.pausar(this.veiculo.veiId).subscribe({
      next: info => { this.ocupado = false; this.aplicar(info); this.toast.success('Pedido enviado: o anúncio sai da OLX em alguns minutos.'); },
      error: err => { this.ocupado = false; this.toast.error(erroOlx(err, 'Não foi possível tirar do ar.')); }
    });
  }

  private nome(lista: OlxItem[], id: string | null): string | null {
    return lista.find(i => i.id === id)?.nome ?? null;
  }

  private aplicar(info: OlxVeiculoInfo): void {
    this.info = info;
    this.dados = { ...info.dados, opcionais: [...(info.dados.opcionais || [])] };
  }

  private recarregarInfo(): void {
    this.olx.veiculo(this.veiculo.veiId).subscribe({ next: i => this.info = i, error: () => {} });
  }

  private carregarMarcas(): void {
    this.olx.marcas().subscribe({
      next: m => { this.marcas = m; if (this.dados?.marcaId) this.carregarModelos(true); },
      error: err => this.erroCarga = erroOlx(err, 'Não foi possível carregar o catálogo da OLX.')
    });
  }

  private carregarModelos(manterVersao: boolean): void {
    this.olx.modelos(this.dados!.marcaId!).subscribe({
      next: m => { this.modelos = m; if (manterVersao && this.dados?.modeloId) this.carregarVersoes(); },
      error: err => this.toast.error(erroOlx(err, 'Não foi possível carregar os modelos.'))
    });
  }

  private carregarVersoes(): void {
    this.olx.versoes(this.dados!.marcaId!, this.dados!.modeloId!).subscribe({
      next: v => {
        this.versoes = v;
        if (this.dados?.versaoId && !v.some(x => x.id === this.dados!.versaoId)) this.dados.versaoId = null;
      },
      error: err => this.toast.error(erroOlx(err, 'Não foi possível carregar as versões.'))
    });
  }
}
