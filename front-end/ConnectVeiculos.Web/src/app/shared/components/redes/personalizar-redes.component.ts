import { Component, ElementRef, EventEmitter, Input, OnChanges, OnDestroy, OnInit, Output, SimpleChanges, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subject, Subscription, debounceTime, switchMap, of, catchError } from 'rxjs';
import { IntegracaoService, RedeSocial, RedesPersonalizacao, TipoArte } from '../../../core/services/integracao.service';
import { ToastService } from '../../../core/services';
import { LayoutStoryPickerComponent } from './layout-story-picker.component';

/**
 * Integracoes > Meta: o que a loja personaliza nas publicacoes — o modelo da
 * legenda de cada rede, a capa (primeira imagem) do post e do story e o
 * layout da arte do story. Tudo vazio = o padrao do sistema.
 *
 * O layout e' gravado junto com o resto da config de stories, entao sai por
 * (layoutChange) para a tela de Integracoes salvar.
 */
@Component({
  selector: 'app-personalizar-redes',
  standalone: true,
  imports: [FormsModule, LayoutStoryPickerComponent],
  templateUrl: './personalizar-redes.component.html',
  styleUrl: './personalizar-redes.component.scss'
})
export class PersonalizarRedesComponent implements OnInit, OnChanges, OnDestroy {
  private api = inject(IntegracaoService);
  private toast = inject(ToastService);

  @Input() layout = 1;
  @Input() cor: string | null | undefined;
  @Output() layoutChange = new EventEmitter<number>();

  @ViewChild('campoLegenda') campoLegenda?: ElementRef<HTMLTextAreaElement>;

  readonly tiposCapa: TipoArte[] = ['post', 'story'];
  info: RedesPersonalizacao | null = null;
  rede: RedeSocial = 'instagram';
  textos: Record<RedeSocial, string> = { instagram: '', facebook: '' };
  private salvos: Record<RedeSocial, string> = { instagram: '', facebook: '' };
  previaTexto = '';
  salvandoLegendas = false;

  enviando: Record<TipoArte, boolean> = { post: false, story: false };
  imagem: Record<'post' | 'story' | 'layout', string | null> = { post: null, story: null, layout: null };
  carregandoImagem: Record<'post' | 'story' | 'layout', boolean> = { post: false, story: false, layout: false };
  semCarro = false;

  private previa$ = new Subject<void>();
  private subs = new Subscription();

  ngOnInit(): void {
    this.subs.add(this.previa$.pipe(
      debounceTime(350),
      switchMap(() => this.api.previaLegenda(this.rede, this.textos[this.rede]).pipe(catchError(() => of({ texto: '' }))))
    ).subscribe(r => this.previaTexto = r.texto));

    this.api.getPersonalizacaoRedes().subscribe({
      next: info => {
        this.aplicar(info);
        this.carregarImagem('post');
        this.carregarImagem('story');
        this.carregarImagem('layout');
      },
      error: () => { /* o interceptor ja avisou */ }
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['layout'] && !changes['layout'].firstChange) this.carregarImagem('layout');
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
    Object.values(this.imagem).forEach(u => u && URL.revokeObjectURL(u));
  }

  private aplicar(info: RedesPersonalizacao): void {
    this.info = info;
    // Loja sem modelo proprio edita a partir do padrao: e' o texto que ja sai hoje.
    this.salvos = {
      instagram: info.legendaInstagram || info.legendaPadraoInstagram,
      facebook: info.legendaFacebook || info.legendaPadraoFacebook
    };
    this.textos = { ...this.salvos };
    this.previa$.next();
  }

  // ---------------- Legenda ----------------

  trocarRede(rede: RedeSocial): void {
    this.rede = rede;
    this.previa$.next();
  }

  aoDigitar(): void {
    this.previa$.next();
  }

  get alterado(): boolean {
    return this.textos.instagram !== this.salvos.instagram || this.textos.facebook !== this.salvos.facebook;
  }

  get ehPadrao(): boolean {
    return !!this.info && this.textos[this.rede] === this.padrao(this.rede);
  }

  get tamanho(): number {
    return this.textos[this.rede].length;
  }

  private padrao(rede: RedeSocial): string {
    return rede === 'instagram' ? this.info!.legendaPadraoInstagram : this.info!.legendaPadraoFacebook;
  }

  /** Coloca {variavel} onde o cursor esta, sem perder o resto do texto. */
  inserirVariavel(nome: string): void {
    const campo = this.campoLegenda?.nativeElement;
    const atual = this.textos[this.rede];
    const marca = `{${nome}}`;
    const inicio = campo?.selectionStart ?? atual.length;
    const fim = campo?.selectionEnd ?? atual.length;
    this.textos[this.rede] = atual.slice(0, inicio) + marca + atual.slice(fim);
    this.aoDigitar();
    setTimeout(() => {
      campo?.focus();
      campo?.setSelectionRange(inicio + marca.length, inicio + marca.length);
    });
  }

  voltarAoPadrao(): void {
    if (!this.info) return;
    this.textos[this.rede] = this.padrao(this.rede);
    this.aoDigitar();
  }

  salvarLegendas(): void {
    if (this.salvandoLegendas) return;
    this.salvandoLegendas = true;
    this.api.salvarLegendasRedes(this.textos.instagram, this.textos.facebook).subscribe({
      next: info => {
        this.salvandoLegendas = false;
        this.aplicar(info);
        this.toast.success('Legendas salvas. Os próximos posts já saem com elas.');
      },
      error: () => this.salvandoLegendas = false
    });
  }

  // ---------------- Capas ----------------

  escolherCapa(tipo: TipoArte, evento: Event): void {
    const input = evento.target as HTMLInputElement;
    const arquivo = input.files?.[0];
    input.value = '';
    if (!arquivo) return;
    if (arquivo.size > 10 * 1024 * 1024) {
      this.toast.error('A imagem passa de 10 MB. Envie uma menor.');
      return;
    }
    this.enviando[tipo] = true;
    this.api.enviarCapaRedes(tipo, arquivo).subscribe({
      next: r => {
        this.enviando[tipo] = false;
        if (this.info) this.info = { ...this.info, [tipo === 'post' ? 'capaPost' : 'capaStory']: r.caminho };
        this.carregarImagem(tipo);
        this.toast.success(tipo === 'post' ? 'Capa dos posts atualizada.' : 'Capa dos stories atualizada.');
      },
      error: () => this.enviando[tipo] = false
    });
  }

  removerCapa(tipo: TipoArte): void {
    this.enviando[tipo] = true;
    this.api.removerCapaRedes(tipo).subscribe({
      next: () => {
        this.enviando[tipo] = false;
        if (this.info) this.info = { ...this.info, [tipo === 'post' ? 'capaPost' : 'capaStory']: null };
        this.carregarImagem(tipo);
        this.toast.success('Capa removida: volta a nossa arte padrão.');
      },
      error: () => this.enviando[tipo] = false
    });
  }

  temCapa(tipo: TipoArte): boolean {
    return !!(tipo === 'post' ? this.info?.capaPost : this.info?.capaStory);
  }

  // ---------------- Layout ----------------

  escolherLayout(id: number): void {
    if (id === this.layout) return;
    this.layoutChange.emit(id);
  }

  get layoutAtual() {
    return this.info?.layouts.find(l => l.id === this.layout);
  }

  /**
   * post: capa da loja ou a arte padrao 4:5. story: so a capa (sem ela nao ha
   * imagem extra). layout: a arte com preco no layout escolhido.
   */
  private carregarImagem(qual: 'post' | 'story' | 'layout'): void {
    if (qual === 'story' && !this.temCapa('story')) {
      this.trocarImagem(qual, null);
      return;
    }
    this.carregandoImagem[qual] = true;
    const req = qual === 'post' ? this.api.previaArteRedes('post')
      : qual === 'story' ? this.api.previaArteRedes('story')
      : this.api.previaArteRedes('story', this.layout);
    req.subscribe({
      next: blob => {
        this.carregandoImagem[qual] = false;
        this.trocarImagem(qual, URL.createObjectURL(blob));
        if (qual !== 'story') this.semCarro = false;
      },
      error: () => {
        this.carregandoImagem[qual] = false;
        this.trocarImagem(qual, null);
        if (qual !== 'story') this.semCarro = true;
      }
    });
  }

  private trocarImagem(qual: 'post' | 'story' | 'layout', url: string | null): void {
    const antiga = this.imagem[qual];
    if (antiga) URL.revokeObjectURL(antiga);
    this.imagem[qual] = url;
  }
}
