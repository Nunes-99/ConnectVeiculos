import { Component, Input } from '@angular/core';
import { ContratoBloco } from '../../../core/services/contrato.service';

/** Mostra o texto do contrato (os mesmos blocos que vao para o PDF). */
@Component({
  selector: 'app-contrato-texto',
  standalone: true,
  template: `
    <div class="ctr-folha">
      @for (b of blocos; track $index) {
        @switch (b.tipo) {
          @case ('titulo') { <h3 class="ctr-titulo">@for (t of b.trechos; track $index) {<span [class.ctr-falta]="t.faltando">{{ t.texto }}</span>}</h3> }
          @case ('secao') { <h4 class="ctr-secao">@for (t of b.trechos; track $index) {<span [class.ctr-falta]="t.faltando">{{ t.texto }}</span>}</h4> }
          @default {
            <p>@for (t of b.trechos; track $index) {@if (t.negrito) {<strong [class.ctr-falta]="t.faltando">{{ t.texto }}</strong>} @else {<span [class.ctr-falta]="t.faltando">{{ t.texto }}</span>}}</p>
          }
        }
      }
    </div>
  `,
  styles: [`
    .ctr-folha { background: #fff; color: #222; border: 1px solid #e0e0e0; border-radius: 8px; padding: 1.5rem 1.75rem;
      font-family: Georgia, 'Times New Roman', serif; font-size: 0.92rem; line-height: 1.55; }
    .ctr-titulo { text-align: center; font-size: 1.05rem; margin: 0 0 1rem; }
    .ctr-secao { font-size: 0.95rem; margin: 1rem 0 0.35rem; }
    p { margin: 0 0 0.6rem; text-align: justify; }
    .ctr-falta { background: #fff3cd; color: #8d6e00; border-radius: 3px; }
    @media (max-width: 600px) { .ctr-folha { padding: 1rem; font-size: 0.88rem; } }
  `]
})
export class ContratoTextoComponent {
  @Input() blocos: ContratoBloco[] = [];
}
