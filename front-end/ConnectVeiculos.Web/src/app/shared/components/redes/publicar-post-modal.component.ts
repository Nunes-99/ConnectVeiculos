import { Component, ElementRef, EventEmitter, Input, OnDestroy, OnInit, Output, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subject, Subscription, catchError, debounceTime, of, switchMap } from 'rxjs';
import { IntegracaoService, RedeSocial, VariavelLegenda } from '../../../core/services/integracao.service';
import { ToastService } from '../../../core/services';

/**
 * Janela de "Publicar no Instagram / Facebook" da lista de veiculos: abre com
 * o modelo de legenda da loja para ajustar so neste post, mostra como fica e
 * publica. A capa (primeira imagem) segue a configuracao de Integracoes.
 */
@Component({
  selector: 'app-publicar-post-modal',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="modal-overlay" (click)="aoClicarFora($event)">
      <div class="modal modal-lg" role="dialog" aria-modal="true" [attr.aria-label]="titulo">
        <div class="modal-header">
          <h2>{{ titulo }}</h2>
          <button class="btn-close" type="button" data-esc (click)="fechar.emit()" aria-label="Fechar">
            <span class="material-icons">close</span>
          </button>
        </div>
        <div class="modal-body">
          <p class="carro">{{ veiculoNome }}</p>
          @if (carregando) {
            <p class="ajuda">Carregando a legenda da loja…</p>
          } @else {
            <label class="rotulo" for="legenda-post">Legenda deste post</label>
            <textarea id="legenda-post" #campo rows="9" [maxlength]="maximo"
                      [(ngModel)]="modelo" (ngModelChange)="previa$.next()"></textarea>
            <div class="rodape-campo">
              <small>{{ modelo.length }} / {{ maximo }}</small>
              @if (modelo !== modeloDaLoja) {
                <button type="button" class="link" (click)="restaurar()">Voltar ao modelo da loja</button>
              }
            </div>
            <div class="variaveis">
              @for (v of variaveis; track v.nome) {
                <button type="button" class="chip" [title]="v.descricao" (click)="inserir(v.nome)">{{ '{' + v.nome + '}' }}</button>
              }
            </div>
            <span class="rotulo">Como vai sair</span>
            <pre>{{ previa || '…' }}</pre>
            <small class="ajuda">
              A primeira imagem é a capa definida em Integrações (a sua imagem ou a nossa arte com o preço),
              seguida das fotos do carro.
              @if (rede === 'facebook') { Se o preço mudar ou o carro for vendido, o Facebook atualiza esta legenda sozinho. }
              @else { O Instagram não deixa editar a legenda depois de publicada. }
            </small>
          }
        </div>
        <div class="modal-footer">
          <button type="button" class="btn-secondary" (click)="fechar.emit()">Cancelar</button>
          <button type="button" class="btn-primary" (click)="publicar()" [disabled]="carregando || publicando || !modelo.trim()">
            {{ publicando ? 'Publicando…' : 'Publicar' }}
          </button>
        </div>
      </div>
    </div>
  `,
  styleUrl: './publicar-post-modal.component.scss'
})
export class PublicarPostModalComponent implements OnInit, OnDestroy {
  private api = inject(IntegracaoService);
  private toast = inject(ToastService);

  @Input({ required: true }) veiculoId!: number;
  @Input() veiculoNome = '';
  @Input({ required: true }) rede!: RedeSocial;
  @Output() fechar = new EventEmitter<void>();
  /** O servidor aceitou: a publicacao segue em segundo plano. */
  @Output() agendado = new EventEmitter<string>();

  @ViewChild('campo') campo?: ElementRef<HTMLTextAreaElement>;

  modelo = '';
  modeloDaLoja = '';
  previa = '';
  variaveis: VariavelLegenda[] = [];
  maximo = 2200;
  carregando = true;
  publicando = false;

  previa$ = new Subject<void>();
  private subs = new Subscription();

  get titulo(): string {
    return this.rede === 'instagram' ? 'Publicar no Instagram' : 'Publicar no Facebook';
  }

  ngOnInit(): void {
    this.subs.add(this.previa$.pipe(
      debounceTime(300),
      switchMap(() => this.api.previaLegenda(this.rede, this.modelo, this.veiculoId).pipe(catchError(() => of({ texto: '' }))))
    ).subscribe(r => this.previa = r.texto));

    this.api.getPersonalizacaoRedes().subscribe({
      next: info => {
        this.variaveis = info.variaveis;
        this.maximo = info.tamanhoMaximoLegenda;
      },
      error: () => { /* chips sao ajuda; publicar funciona sem eles */ }
    });

    this.api.getLegendaVeiculo(this.veiculoId, this.rede).subscribe({
      next: r => {
        this.modelo = this.modeloDaLoja = r.modelo;
        this.previa = r.texto;
        this.carregando = false;
      },
      error: () => this.fechar.emit()
    });
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  inserir(nome: string): void {
    const campo = this.campo?.nativeElement;
    const marca = `{${nome}}`;
    const inicio = campo?.selectionStart ?? this.modelo.length;
    const fim = campo?.selectionEnd ?? this.modelo.length;
    this.modelo = this.modelo.slice(0, inicio) + marca + this.modelo.slice(fim);
    this.previa$.next();
    setTimeout(() => {
      campo?.focus();
      campo?.setSelectionRange(inicio + marca.length, inicio + marca.length);
    });
  }

  restaurar(): void {
    this.modelo = this.modeloDaLoja;
    this.previa$.next();
  }

  aoClicarFora(e: MouseEvent): void {
    if (e.target === e.currentTarget && !this.publicando) this.fechar.emit();
  }

  publicar(): void {
    if (this.publicando) return;
    this.publicando = true;
    // Igual ao modelo da loja vai vazio: o servidor guarda so legenda editada.
    const legenda = this.modelo === this.modeloDaLoja ? undefined : this.modelo;
    const req = this.rede === 'instagram'
      ? this.api.publicarVeiculoInstagram(this.veiculoId, legenda)
      : this.api.publicarVeiculoFacebookPage(this.veiculoId, legenda);
    req.subscribe({
      next: r => {
        this.publicando = false;
        this.toast.info(r?.mensagem || 'Publicando… o selo aparece na lista quando terminar.');
        this.agendado.emit(this.rede);
      },
      // O httpErrorInterceptor ja mostra o motivo que o servidor mandou.
      error: () => this.publicando = false
    });
  }
}
