import { Pipe, PipeTransform } from '@angular/core';

/**
 * Formata telefone para exibicao: (11) 3333-4444 ou (11) 99999-8888.
 *
 * A MaskDirective ja fazia isso, mas so em <input> enquanto o usuario digita.
 * O catalogo publico mostra o telefone da loja como texto, e vinha cru do banco
 * ("1133334444") no cabecalho e no rodape.
 *
 * Aceita numero com ou sem DDI 55. Se nao reconhecer o formato, devolve o valor
 * original — melhor mostrar o numero cru do que esconder o contato da loja.
 */
@Pipe({
  name: 'telefone',
  standalone: true
})
export class TelefonePipe implements PipeTransform {
  transform(valor: string | null | undefined): string {
    if (!valor) return '';

    let digitos = valor.replace(/\D/g, '');

    // DDI do Brasil: 5511999998888 -> 11999998888
    if (digitos.length > 11 && digitos.startsWith('55')) {
      digitos = digitos.substring(2);
    }

    if (digitos.length === 11) {
      return digitos.replace(/(\d{2})(\d{5})(\d{4})/, '($1) $2-$3');
    }

    if (digitos.length === 10) {
      return digitos.replace(/(\d{2})(\d{4})(\d{4})/, '($1) $2-$3');
    }

    return valor;
  }
}
