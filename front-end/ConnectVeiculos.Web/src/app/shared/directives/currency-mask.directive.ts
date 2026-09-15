import { Directive, ElementRef, HostListener, OnInit, forwardRef } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

@Directive({
  selector: '[appCurrencyMask]',
  standalone: true,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => CurrencyMaskDirective),
      multi: true
    }
  ]
})
export class CurrencyMaskDirective implements ControlValueAccessor, OnInit {
  private onChange: (value: number) => void = () => {};
  private onTouched: () => void = () => {};
  private cents: number = 0;

  constructor(private el: ElementRef<HTMLInputElement>) {}

  ngOnInit(): void {
    this.el.nativeElement.type = 'tel';
    this.el.nativeElement.inputMode = 'numeric';
  }

  writeValue(value: number): void {
    if (value === null || value === undefined || isNaN(value)) {
      this.cents = 0;
    } else {
      this.cents = Math.round(value * 100);
    }
    this.updateDisplay();
  }

  registerOnChange(fn: (value: number) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  @HostListener('blur')
  onBlur(): void {
    this.onTouched();
  }

  // Campo de dinheiro costuma chegar preenchido — o valor da venda nasce com o
  // preco do veiculo, por exemplo. Como o mask reparseia TODOS os digitos do
  // campo, digitar por cima anexava ao que ja estava: um R$ 129.500,00 com
  // "12950000" digitado virou R$ 12.950.001.295,00, e a venda foi gravada assim.
  // Selecionar tudo ao focar faz a digitacao substituir, que e o que se espera.
  @HostListener('focus')
  onFocus(): void {
    this.el.nativeElement.select();
  }

  @HostListener('input')
  onInput(): void {
    // Teto de digitos: R$ 9.999.999.999,99. Nenhum valor real do sistema chega
    // perto, e sem limite o campo aceita um numero de tamanho arbitrario.
    const digits = this.el.nativeElement.value.replace(/\D/g, '').slice(0, 12);
    this.cents = parseInt(digits, 10) || 0;
    this.updateDisplay();
    this.onChange(this.cents / 100);
  }

  @HostListener('keydown', ['$event'])
  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'ArrowUp' || event.key === 'ArrowDown') {
      event.preventDefault();
    }
  }

  private updateDisplay(): void {
    if (this.cents === 0) {
      this.el.nativeElement.value = 'R$ 0,00';
      return;
    }
    const value = this.cents / 100;
    this.el.nativeElement.value = 'R$ ' + value.toLocaleString('pt-BR', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    });
  }
}
