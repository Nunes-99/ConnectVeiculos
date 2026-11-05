import { Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { Veiculo } from '../../../core/models';
import { ToastService } from '../../../core/services/toast.service';
import {
  NapistaAnuncioDados, NapistaAtributos, NapistaItem, NapistaService, NapistaVeiculoInfo, erroNapista
} from '../../../core/services/napista.service';

/**
 * Janela "NaPista" de um veiculo: confere a versao e os valores que o sistema
 * sugeriu a partir do cadastro (marca, modelo, versao, cor...) e anuncia,
 * atualiza ou tira do ar. Depois de anunciado, editar, vender ou estornar
 * atualiza o anuncio sozinho.
 */
@Component({
  selector: 'app-napista-veiculo',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './napista-veiculo.component.html',
  styleUrl: './napista.scss'
})
export class NapistaVeiculoComponent implements OnInit {
  private napista = inject(NapistaService);
  private toast = inject(ToastService);

  @Input({ required: true }) veiculo!: Veiculo;
  @Output() fechar = new EventEmitter<void>();

  info: NapistaVeiculoInfo | null = null;
  dados: NapistaAnuncioDados | null = null;
  erroCarga: string | null = null;
  ocupado = false;
  confirmarPausa = false;

  marcas: NapistaItem[] = [];
  modelos: NapistaItem[] = [];
  versoes: NapistaItem[] = [];
  atributos: NapistaAtributos = { cores: [], combustiveis: [], cambios: [], opcionais: [] };
  buscaOpcional = '';

  readonly categorias = [
    { id: 'CAR', nome: 'Carro' },
    { id: 'MOTORCYCLE', nome: 'Moto' },
    { id: 'TRUCK', nome: 'Caminhão' }
  ];

  get disponivel(): boolean { return this.veiculo.veiSts === 'D'; }

  get opcionaisVisiveis(): NapistaItem[] {
    const b = this.buscaOpcional.trim().toLowerCase();
    return b ? this.atributos.opcionais.filter(o => o.nome.toLowerCase().includes(b)) : this.atributos.opcionais;
  }

  ngOnInit(): void {
    this.napista.veiculo(this.veiculo.veiId).subscribe({
      next: info => {
        this.aplicar(info);
        this.carregarCatalogo(true);
      },
      error: err => this.erroCarga = erroNapista(err)
    });
  }

  categoriaMudou(): void {
    if (!this.dados) return;
    Object.assign(this.dados, { marcaId: null, modeloId: null, versaoId: null, cor: null, opcionais: [] });
    this.modelos = [];
    this.versoes = [];
    this.carregarCatalogo(false);
  }

  marcaMudou(): void {
    if (!this.dados) return;
    this.dados.modeloId = null;
    this.dados.versaoId = null;
    this.modelos = [];
    this.versoes = [];
    if (this.dados.marcaId) this.carregarModelos(false);
  }

  modeloMudou(): void {
    if (!this.dados) return;
    this.dados.versaoId = null;
    this.versoes = [];
    if (this.dados.modeloId) this.carregarVersoes();
  }

  anoMudou(): void {
    if (this.dados?.modeloId) this.carregarVersoes();
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
    this.napista.publicar(this.veiculo.veiId, this.dados).subscribe({
      next: info => {
        this.ocupado = false;
        this.aplicar(info);
        if (info.erro) this.toast.warning(info.erro);
        else this.toast.success('Anúncio enviado para o NaPista.');
      },
      error: err => { this.ocupado = false; this.toast.error(erroNapista(err, 'O NaPista não aceitou o anúncio.')); this.recarregarInfo(); }
    });
  }

  /** Primeiro clique pede confirmacao no proprio botao; o segundo tira do ar. */
  pausar(): void {
    if (!this.confirmarPausa) { this.confirmarPausa = true; return; }
    this.confirmarPausa = false;
    this.ocupado = true;
    this.napista.pausar(this.veiculo.veiId).subscribe({
      next: info => { this.ocupado = false; this.aplicar(info); this.toast.success('Anúncio tirado do ar no NaPista.'); },
      error: err => { this.ocupado = false; this.toast.error(erroNapista(err, 'Não foi possível tirar do ar.')); }
    });
  }

  private aplicar(info: NapistaVeiculoInfo): void {
    this.info = info;
    this.dados = { ...info.dados, opcionais: [...(info.dados.opcionais || [])] };
  }

  private recarregarInfo(): void {
    this.napista.veiculo(this.veiculo.veiId).subscribe({ next: i => this.info = i, error: () => {} });
  }

  private carregarCatalogo(manterEscolhas: boolean): void {
    const cat = this.dados!.categoria || 'CAR';
    forkJoin({ marcas: this.napista.marcas(cat), atributos: this.napista.atributos(cat) }).subscribe({
      next: r => {
        this.marcas = r.marcas;
        this.atributos = r.atributos;
        if (manterEscolhas && this.dados?.marcaId) this.carregarModelos(true);
      },
      error: err => this.erroCarga = erroNapista(err, 'Não foi possível carregar o catálogo do NaPista.')
    });
  }

  private carregarModelos(manterVersao: boolean): void {
    this.napista.modelos(this.dados!.categoria, this.dados!.marcaId!, null).subscribe({
      next: m => { this.modelos = m; if (manterVersao && this.dados?.modeloId) this.carregarVersoes(); },
      error: err => this.toast.error(erroNapista(err, 'Não foi possível carregar os modelos.'))
    });
  }

  private carregarVersoes(): void {
    this.napista.versoes(this.dados!.categoria, this.dados!.modeloId!, this.dados!.anoModelo).subscribe({
      next: v => {
        this.versoes = v;
        if (this.dados?.versaoId && !v.some(x => x.id === this.dados!.versaoId)) this.dados.versaoId = null;
      },
      error: err => this.toast.error(erroNapista(err, 'Não foi possível carregar as versões.'))
    });
  }
}
