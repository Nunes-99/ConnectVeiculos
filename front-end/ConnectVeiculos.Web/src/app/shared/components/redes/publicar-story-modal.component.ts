import { Component, EventEmitter, Input, OnDestroy, OnInit, Output, inject } from '@angular/core';
import { Subject, Subscription, catchError, of, switchMap, debounceTime, map } from 'rxjs';
import { IntegracaoService, LayoutStory } from '../../../core/services/integracao.service';
import { ToastService } from '../../../core/services';
import { LayoutStoryPickerComponent } from './layout-story-picker.component';

/**
 * Janela de "Publicar story" da lista de veiculos: vem marcado o layout da
 * loja, da para trocar so para este carro e ver a arte com as fotos dele.
 */
@Component({
  selector: 'app-publicar-story-modal',
  standalone: true,
  imports: [LayoutStoryPickerComponent],
  template: `
    <div class="modal-overlay" (click)="aoClicarFora($event)">
      <div class="modal modal-lg" role="dialog" aria-modal="true" aria-label="Publicar story">
        <div class="modal-header">
          <h2>Publicar story</h2>
          <button class="btn-close" type="button" data-esc (click)="fechar.emit()" aria-label="Fechar">
            <span class="material-icons">close</span>
          </button>
        </div>
        <div class="modal-body">
          <p class="carro">{{ veiculoNome }}</p>
          <div class="grade">
            <div>
              <span class="rotulo">Layout da arte</span>
              <app-layout-story-picker [layouts]="layouts" [selecionado]="layout" [cor]="cor"
                                       (selecionar)="escolher($event)" />
              @if (layout !== layoutDaLoja) {
                <small class="ajuda">Só para este story. O padrão da loja continua o de Integrações.</small>
              }
            </div>
            <div class="previa">
              <span class="rotulo">Prévia com as fotos deste carro</span>
              <div class="tela">
                @if (carregando) { <span class="aviso">Gerando prévia…</span> }
                @else if (imagem) { <img [src]="imagem" alt="Prévia do story" /> }
                @else { <span class="aviso">Sem prévia: este carro ainda não tem foto. Cadastre as fotos em Imagens para publicar o story.</span> }
              </div>
            </div>
          </div>
          <small class="ajuda">
            Se a loja enviou uma capa de story em Integrações, ela entra antes desta arte. Depois vêm
            as outras fotos do carro, conforme a quantidade configurada.
          </small>
        </div>
        <div class="modal-footer">
          <button type="button" class="btn-secondary" (click)="fechar.emit()">Cancelar</button>
          <button type="button" class="btn-primary" (click)="publicar()" [disabled]="publicando">
            {{ publicando ? 'Enviando…' : 'Publicar story' }}
          </button>
        </div>
      </div>
    </div>
  `,
  styleUrl: './publicar-story-modal.component.scss'
})
export class PublicarStoryModalComponent implements OnInit, OnDestroy {
  private api = inject(IntegracaoService);
  private toast = inject(ToastService);

  @Input({ required: true }) veiculoId!: number;
  @Input() veiculoNome = '';
  @Input() cor: string | null | undefined;
  @Output() fechar = new EventEmitter<void>();
  /** O servidor so conferiu e agendou; a publicacao termina em ~2 min. */
  @Output() agendado = new EventEmitter<string>();

  layouts: LayoutStory[] = [];
  layout = 1;
  layoutDaLoja = 1;
  imagem: string | null = null;
  carregando = true;
  publicando = false;

  private previa$ = new Subject<number>();
  private subs = new Subscription();

  ngOnInit(): void {
    this.subs.add(this.previa$.pipe(
      debounceTime(150),
      switchMap(l => {
        this.carregando = true;
        return this.api.previaArteRedes('story', l, this.veiculoId).pipe(
          map(b => URL.createObjectURL(b)),
          catchError(() => of(null)));
      })
    ).subscribe(url => {
      if (this.imagem) URL.revokeObjectURL(this.imagem);
      this.imagem = url;
      this.carregando = false;
    }));

    this.api.getPersonalizacaoRedes().subscribe({
      next: info => this.layouts = info.layouts,
      error: () => { /* sem a lista, publica no layout da loja */ }
    });
    this.api.getStoriesStatus().subscribe({
      next: c => {
        this.layout = this.layoutDaLoja = c.layout || 1;
        this.previa$.next(this.layout);
      },
      error: () => this.previa$.next(this.layout)
    });
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
    if (this.imagem) URL.revokeObjectURL(this.imagem);
  }

  escolher(id: number): void {
    this.layout = id;
    this.previa$.next(id);
  }

  aoClicarFora(e: MouseEvent): void {
    if (e.target === e.currentTarget && !this.publicando) this.fechar.emit();
  }

  publicar(): void {
    if (this.publicando) return;
    this.publicando = true;
    // Layout da loja vai vazio: o servidor usa o de Integracoes.
    this.api.publicarStory(this.veiculoId, this.layout === this.layoutDaLoja ? undefined : this.layout).subscribe({
      next: r => {
        this.publicando = false;
        this.toast.success(r?.mensagem || 'Publicando o story. Em cerca de 2 minutos estará no ar.');
        this.agendado.emit(r?.mensagem);
      },
      // Mensagem de erro: o httpErrorInterceptor ja mostra.
      error: () => this.publicando = false
    });
  }
}
