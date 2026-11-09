import { Component, EventEmitter, Input, Output } from '@angular/core';
import { LayoutStory } from '../../../core/services/integracao.service';

/**
 * Os 10 layouts do story como miniaturas desenhadas em CSS. Desenho em vez de
 * imagem gerada: escolher nao pode custar 10 renderizacoes no servidor. A
 * previa de verdade fica a cargo de quem usa o seletor.
 */
@Component({
  selector: 'app-layout-story-picker',
  standalone: true,
  template: `
    <div class="layouts" role="radiogroup" aria-label="Layout do story">
      @for (l of layouts; track l.id) {
        <button type="button" class="layout" role="radio"
                [class.ativo]="l.id === selecionado" [attr.aria-checked]="l.id === selecionado"
                [title]="l.descricao" (click)="selecionar.emit(l.id)">
          <span class="mini" [class]="'mini mini-' + l.id" [style.--cor]="cor">
            @switch (l.id) {
              @case (1) { <i class="foto moldura" style="top:30%;left:12%;width:76%;height:24%"></i> }
              @case (2) { <i class="foto" style="inset:0"></i><i class="sombra"></i> }
              @case (3) { <i class="foto" style="top:0;left:0;width:100%;height:58%"></i><i class="faixa"></i> }
              @case (4) {
                <i class="foto" style="top:0;left:0;width:100%;height:31%"></i>
                <i class="foto" style="top:33%;left:0;width:100%;height:31%"></i>
              }
              @case (5) {
                <i class="foto" style="top:0;left:0;width:100%;height:38%"></i>
                <i class="foto" style="top:40%;left:0;width:48%;height:24%"></i>
                <i class="foto" style="top:40%;left:52%;width:48%;height:24%"></i>
              }
              @case (6) {
                <i class="foto" style="top:0;left:0;width:48%;height:31%"></i>
                <i class="foto" style="top:0;left:52%;width:48%;height:31%"></i>
                <i class="foto" style="top:33%;left:0;width:48%;height:31%"></i>
                <i class="foto" style="top:33%;left:52%;width:48%;height:31%"></i>
              }
              @case (7) { <i class="cartao"></i><i class="foto" style="top:18%;left:14%;width:72%;height:28%"></i> }
              @case (8) { <i class="foto" style="inset:0"></i><i class="circulo"></i> }
              @case (9) { <i class="foto" style="top:22%;left:0;width:100%;height:30%"></i> }
              @case (10) {
                <i class="foto" style="top:0;left:0;width:100%;height:20%"></i>
                <i class="foto" style="top:22%;left:0;width:100%;height:20%"></i>
                <i class="foto" style="top:44%;left:0;width:100%;height:20%"></i>
              }
            }
            <i class="linhas" [class.esquerda]="l.id === 9" [class.escuras]="l.id === 7"></i>
          </span>
          <span class="nome">{{ l.nome }}</span>
          @if (l.fotos > 1) { <span class="qtd">{{ l.fotos }} fotos</span> }
        </button>
      }
    </div>
  `,
  styles: [`
    .layouts {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(84px, 1fr));
      gap: 10px;
    }
    .layout {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 4px;
      padding: 8px 4px;
      background: var(--bg-primary);
      border: 2px solid var(--border-primary);
      border-radius: 10px;
      cursor: pointer;
      color: var(--text-primary);
      transition: border-color .15s, box-shadow .15s;
    }
    .layout:hover { border-color: var(--brand-primary); }
    .layout.ativo {
      border-color: var(--brand-primary);
      box-shadow: 0 0 0 3px var(--brand-primary-light);
    }
    .nome { font-size: 12px; font-weight: 600; text-align: center; line-height: 1.2; }
    .qtd { font-size: 11px; color: var(--text-secondary); }

    .mini {
      --cor: #1a237e;
      position: relative;
      width: 54px;
      height: 96px;
      border-radius: 6px;
      overflow: hidden;
      background: linear-gradient(#3a4250, #1c2028);
    }
    .mini-9 { background: #0f1115; }
    .mini-3 { background: var(--cor); }
    .mini i { position: absolute; display: block; }
    .foto { background: linear-gradient(135deg, #8fb3d9, #4f6f94); }
    .moldura { outline: 2px solid rgba(255,255,255,.9); }
    .sombra { left: 0; right: 0; bottom: 0; height: 55%; background: linear-gradient(transparent, rgba(0,0,0,.85)); }
    .faixa { left: 0; right: 0; top: 52%; height: 12%; background: var(--cor); transform: skewY(-8deg); }
    .cartao { top: 14%; left: 8%; width: 84%; height: 72%; background: #fff; border-radius: 5px; }
    .circulo {
      top: 46%; left: 50%; width: 30px; height: 30px; margin-left: -15px;
      border-radius: 50%; background: var(--cor); outline: 2px solid #fff; outline-offset: -4px;
    }
    .linhas {
      left: 18%; right: 18%; bottom: 16%; height: 22%;
      background:
        linear-gradient(#fff, #fff) center 0 / 100% 3px no-repeat,
        linear-gradient(rgba(255,255,255,.6), rgba(255,255,255,.6)) center 7px / 70% 2px no-repeat,
        linear-gradient(#fff, #fff) center 12px / 80% 4px no-repeat,
        linear-gradient(var(--cor), var(--cor)) center 19px / 30% 2px no-repeat;
    }
    .mini-8 .linhas { bottom: auto; top: 12%; height: 10px; }
    .linhas.esquerda {
      left: 12%; right: 20%;
      background-position: left 0, left 7px, left 12px, left 19px;
      border-left: 2px solid var(--cor);
      padding-left: 2px;
    }
    .linhas.escuras {
      bottom: 18%;
      background:
        linear-gradient(#333, #333) center 0 / 100% 3px no-repeat,
        linear-gradient(#999, #999) center 7px / 70% 2px no-repeat,
        linear-gradient(#333, #333) center 12px / 80% 4px no-repeat,
        linear-gradient(var(--cor), var(--cor)) center 19px / 30% 2px no-repeat;
    }
  `]
})
export class LayoutStoryPickerComponent {
  @Input() layouts: LayoutStory[] = [];
  @Input() selecionado = 1;
  /** Cor da loja, usada nas faixas e no circulo das miniaturas. */
  @Input() cor: string | null | undefined = '#1a237e';
  @Output() selecionar = new EventEmitter<number>();
}
