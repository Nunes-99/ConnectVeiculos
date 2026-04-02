import { Directive, ElementRef, HostListener, Input } from '@angular/core';

@Directive({
  selector: '[appMask]',
  standalone: true
})
export class MaskDirective {
  @Input('appMask') maskType: 'cpf' | 'cnpj' | 'telefone' | 'celular' | 'cep' | 'placa' | 'dinheiro' | 'km' = 'cpf';

  constructor(private el: ElementRef<HTMLInputElement>) {}

  @HostListener('input', ['$event'])
  onInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    let value = input.value.replace(/\D/g, '');

    switch (this.maskType) {
      case 'cpf':
        value = this.applyCpfMask(value);
        break;
      case 'cnpj':
        value = this.applyCnpjMask(value);
        break;
      case 'telefone':
        value = this.applyTelefoneMask(value);
        break;
      case 'celular':
        value = this.applyCelularMask(value);
        break;
      case 'cep':
        value = this.applyCepMask(value);
        break;
      case 'placa':
        value = this.applyPlacaMask(input.value);
        break;
      case 'dinheiro':
        value = this.applyDinheiroMask(value);
        break;
      case 'km':
        value = this.applyKmMask(value);
        break;
    }

    input.value = value;
  }

  private applyCpfMask(value: string): string {
    if (value.length > 11) value = value.substring(0, 11);

    if (value.length > 9) {
      return value.replace(/(\d{3})(\d{3})(\d{3})(\d{1,2})/, '$1.$2.$3-$4');
    } else if (value.length > 6) {
      return value.replace(/(\d{3})(\d{3})(\d{1,3})/, '$1.$2.$3');
    } else if (value.length > 3) {
      return value.replace(/(\d{3})(\d{1,3})/, '$1.$2');
    }
    return value;
  }

  private applyCnpjMask(value: string): string {
    if (value.length > 14) value = value.substring(0, 14);

    if (value.length > 12) {
      return value.replace(/(\d{2})(\d{3})(\d{3})(\d{4})(\d{1,2})/, '$1.$2.$3/$4-$5');
    } else if (value.length > 8) {
      return value.replace(/(\d{2})(\d{3})(\d{3})(\d{1,4})/, '$1.$2.$3/$4');
    } else if (value.length > 5) {
      return value.replace(/(\d{2})(\d{3})(\d{1,3})/, '$1.$2.$3');
    } else if (value.length > 2) {
      return value.replace(/(\d{2})(\d{1,3})/, '$1.$2');
    }
    return value;
  }

  private applyTelefoneMask(value: string): string {
    if (value.length > 10) value = value.substring(0, 10);

    if (value.length > 6) {
      return value.replace(/(\d{2})(\d{4})(\d{1,4})/, '($1) $2-$3');
    } else if (value.length > 2) {
      return value.replace(/(\d{2})(\d{1,4})/, '($1) $2');
    }
    return value;
  }

  private applyCelularMask(value: string): string {
    if (value.length > 11) value = value.substring(0, 11);

    if (value.length > 7) {
      return value.replace(/(\d{2})(\d{5})(\d{1,4})/, '($1) $2-$3');
    } else if (value.length > 2) {
      return value.replace(/(\d{2})(\d{1,5})/, '($1) $2');
    }
    return value;
  }

  private applyCepMask(value: string): string {
    if (value.length > 8) value = value.substring(0, 8);

    if (value.length > 5) {
      return value.replace(/(\d{5})(\d{1,3})/, '$1-$2');
    }
    return value;
  }

  private applyPlacaMask(value: string): string {
    // Remove tudo exceto letras e numeros
    value = value.toUpperCase().replace(/[^A-Z0-9]/g, '');
    if (value.length > 7) value = value.substring(0, 7);

    // Formato antigo: AAA-0000
    // Formato Mercosul: AAA0A00
    if (value.length > 3) {
      const letras = value.substring(0, 3);
      const resto = value.substring(3);

      // Verifica se e formato Mercosul (4o caractere e numero, 5o e letra)
      if (resto.length >= 2 && /\d/.test(resto[0]) && /[A-Z]/.test(resto[1])) {
        return letras + resto; // Mercosul sem hifen
      }
      return letras + '-' + resto; // Formato antigo com hifen
    }
    return value;
  }

  private applyKmMask(value: string): string {
    if (!value) return '';
    const numero = parseInt(value, 10);
    return numero.toLocaleString('pt-BR');
  }

  private applyDinheiroMask(value: string): string {
    if (!value) return '';

    // Converte para numero e formata
    const numero = parseInt(value, 10) / 100;
    return numero.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }
}
