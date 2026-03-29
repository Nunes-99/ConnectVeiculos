import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-confirm-modal',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './confirm-modal.component.html',
  styleUrl: './confirm-modal.component.scss'
})
export class ConfirmModalComponent {
  @Input() show = false;
  @Input() titulo = 'Confirmar';
  @Input() mensagem = 'Deseja realmente realizar esta acao?';
  @Input() textoBotaoConfirmar = 'Confirmar';
  @Input() textoBotaoCancelar = 'Cancelar';
  @Input() tipo: 'danger' | 'warning' | 'info' = 'danger';

  @Output() confirmar = new EventEmitter<void>();
  @Output() cancelar = new EventEmitter<void>();

  onConfirmar(): void {
    this.confirmar.emit();
  }

  onCancelar(): void {
    this.cancelar.emit();
  }

  onOverlayClick(event: Event): void {
    if (event.target === event.currentTarget) {
      this.onCancelar();
    }
  }
}
