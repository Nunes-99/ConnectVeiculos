import { Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { Veiculo } from '../../../core/models';
import { ToastService } from '../../../core/services/toast.service';
import {
  WebmotorsAnuncioDados, WebmotorsItem, WebmotorsService, WebmotorsVeiculoInfo, WebmotorsVersao, erroWebmotors
} from '../../../core/services/webmotors.service';

/**
 * Janela "Webmotors" de um veiculo: confere os codigos que o sistema
 * sugeriu a partir do cadastro (marca, modelo, versao, cor...) e anuncia,
 * atualiza ou tira do ar.
 */
@Component({
  selector: 'app-webmotors-veiculo',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './webmotors-veiculo.component.html',
  styleUrl: './webmotors.scss'
})
export class WebmotorsVeiculoComponent implements OnInit {
  private webmotors = inject(WebmotorsService);
  private toast = inject(ToastService);

  @Input({ required: true }) veiculo!: Veiculo;
  @Output() fechar = new EventEmitter<void>();

  info: WebmotorsVeiculoInfo | null = null;
  dados: WebmotorsAnuncioDados | null = null;
  erroCarga: string | null = null;
  ocupado = false;

  marcas: WebmotorsItem[] = [];
  modelos: WebmotorsItem[] = [];
  versoes: WebmotorsVersao[] = [];
  cores: WebmotorsItem[] = [];
  cambios: WebmotorsItem[] = [];
  combustiveis: WebmotorsItem[] = [];
  portas: WebmotorsItem[] = [];
  opcionais: WebmotorsItem[] = [];

  readonly caracteristicas: { campo: keyof WebmotorsAnuncioDados; rotulo: string }[] = [
    { campo: 'unicoDono', rotulo: 'Único dono' },
    { campo: 'ipvaPago', rotulo: 'IPVA pago' },
    { campo: 'licenciado', rotulo: 'Licenciado' },
    { campo: 'revisoesEmConcessionaria', rotulo: 'Revisões em concessionária' },
    { campo: 'garantiaDeFabrica', rotulo: 'Garantia de fábrica' },
    { campo: 'blindado', rotulo: 'Blindado' },
    { campo: 'alienado', rotulo: 'Alienado (financiado)' },
    { campo: 'adaptadoPcd', rotulo: 'Adaptado para PcD' },
    { campo: 'naoAceitaTroca', rotulo: 'Não aceita troca' }
  ];

  get disponivel(): boolean { return this.veiculo.veiSts === 'D'; }

  ngOnInit(): void {
    forkJoin({
      info: this.webmotors.veiculo(this.veiculo.veiId),
      marcas: this.webmotors.tabela('marcas'),
      cores: this.webmotors.tabela('cores'),
      cambios: this.webmotors.tabela('cambios'),
      combustiveis: this.webmotors.tabela('combustiveis'),
      portas: this.webmotors.tabela('portas'),
      opcionais: this.webmotors.tabela('opcionais')
    }).subscribe({
      next: r => {
        Object.assign(this, { marcas: r.marcas, cores: r.cores, cambios: r.cambios, combustiveis: r.combustiveis, opcionais: r.opcionais });
        this.portas = [...r.portas].sort((a, b) => a.codigo - b.codigo);
        this.aplicar(r.info);
        if (this.dados?.marcaId) this.carregarModelos(false);
      },
      error: err => this.erroCarga = erroWebmotors(err, 'Não foi possível falar com a Webmotors.')
    });
  }

  marcaMudou(): void {
    if (!this.dados) return;
    this.dados.modeloId = null;
    this.dados.versaoId = null;
    this.modelos = [];
    this.versoes = [];
    if (this.dados.marcaId) this.carregarModelos(true);
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

  temOpcional(codigo: number): boolean {
    return !!this.dados?.opcionais.includes(codigo);
  }

  alternarOpcional(codigo: number): void {
    if (!this.dados) return;
    this.dados.opcionais = this.temOpcional(codigo)
      ? this.dados.opcionais.filter(o => o !== codigo)
      : [...this.dados.opcionais, codigo];
  }

  marcar(campo: keyof WebmotorsAnuncioDados, valor: boolean): void {
    if (this.dados) (this.dados as any)[campo] = valor;
  }

  valor(campo: keyof WebmotorsAnuncioDados): boolean {
    return !!this.dados && !!(this.dados as any)[campo];
  }

  publicar(): void {
    if (!this.dados) return;
    this.ocupado = true;
    this.webmotors.publicar(this.veiculo.veiId, this.dados).subscribe({
      next: info => {
        this.ocupado = false;
        this.aplicar(info);
        if (info.erro) this.toast.warning(info.erro);
        else this.toast.success(info.publicado ? 'Anúncio enviado para a Webmotors.' : 'Dados salvos.');
      },
      error: err => { this.ocupado = false; this.toast.error(erroWebmotors(err, 'A Webmotors não aceitou o anúncio.')); this.recarregarInfo(); }
    });
  }

  remover(): void {
    if (!confirm('Tirar este veículo da Webmotors?')) return;
    this.ocupado = true;
    this.webmotors.remover(this.veiculo.veiId).subscribe({
      next: info => { this.ocupado = false; this.aplicar(info); this.toast.success('Anúncio removido da Webmotors.'); },
      error: err => { this.ocupado = false; this.toast.error(erroWebmotors(err, 'Não foi possível remover.')); }
    });
  }

  private aplicar(info: WebmotorsVeiculoInfo): void {
    this.info = info;
    this.dados = { ...info.dados, opcionais: [...(info.dados.opcionais || [])] };
  }

  private recarregarInfo(): void {
    this.webmotors.veiculo(this.veiculo.veiId).subscribe({ next: i => this.info = i, error: () => {} });
  }

  private carregarModelos(limparVersao: boolean): void {
    this.webmotors.modelos(this.dados!.marcaId!).subscribe({
      next: m => { this.modelos = m; if (!limparVersao && this.dados?.modeloId) this.carregarVersoes(); },
      error: err => this.toast.error(erroWebmotors(err, 'Não foi possível carregar os modelos.'))
    });
  }

  private carregarVersoes(): void {
    this.webmotors.versoes(this.dados!.modeloId!, this.dados!.anoModelo).subscribe({
      next: v => {
        this.versoes = v;
        if (this.dados?.versaoId && !v.some(x => x.codigo === this.dados!.versaoId)) this.dados.versaoId = null;
      },
      error: err => this.toast.error(erroWebmotors(err, 'Não foi possível carregar as versões.'))
    });
  }
}
