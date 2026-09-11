import { Directive, ElementRef, Input, Renderer2 } from '@angular/core';

/**
 * Tranca um formulario ou modal enquanto a gravacao acontece.
 *
 * Sem isso o usuario clicava em Salvar, o botao continuava escrito "Salvar" e a
 * tela seguia editavel: dava pra mexer nos campos durante o envio, clicar de
 * novo e criar registro duplicado, ou achar que travou e fechar o modal no meio.
 *
 * Usa o atributo nativo `inert`, que alem de ignorar cliques tira tudo que esta
 * dentro da navegacao por teclado e do leitor de tela — `pointer-events: none`
 * sozinho ainda deixaria chegar nos campos pelo Tab.
 *
 * Uso:
 *   <div class="modal" [appSalvando]="salvando">
 */
@Directive({
  selector: '[appSalvando]',
  standalone: true
})
export class SalvandoDirective {
  private ativo = false;

  constructor(
    private readonly el: ElementRef<HTMLElement>,
    private readonly renderer: Renderer2
  ) {}

  @Input({ alias: 'appSalvando' })
  set salvando(valor: boolean | null | undefined) {
    const ligado = !!valor;
    if (ligado === this.ativo) return;
    this.ativo = ligado;

    const alvo = this.el.nativeElement;

    if (ligado) {
      this.renderer.addClass(alvo, 'esta-salvando');
      this.renderer.setAttribute(alvo, 'inert', '');
      this.renderer.setAttribute(alvo, 'aria-busy', 'true');
      return;
    }

    this.renderer.removeClass(alvo, 'esta-salvando');
    this.renderer.removeAttribute(alvo, 'inert');
    this.renderer.removeAttribute(alvo, 'aria-busy');
  }
}
