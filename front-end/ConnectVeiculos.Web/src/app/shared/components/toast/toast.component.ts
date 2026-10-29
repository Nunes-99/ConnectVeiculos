import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastService, Toast } from '../../../core/services/toast.service';

@Component({
  selector: 'app-toast',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="toast-container">
      @for (toast of toastService.toasts; track toast.id) {
        <div class="toast" [class]="'toast-' + toast.type" (click)="toastService.remove(toast.id)">
          <span class="material-icons toast-icon">{{ getIcon(toast.type) }}</span>
          <span class="toast-message">{{ toast.message }}</span>
          @if (toast.acao) {
            <button class="toast-acao" (click)="toast.acao.executar(); toastService.remove(toast.id); $event.stopPropagation()">
              {{ toast.acao.rotulo }}
            </button>
          }
          <button class="toast-close" (click)="toastService.remove(toast.id); $event.stopPropagation()">
            <span class="material-icons">close</span>
          </button>
        </div>
      }
    </div>
  `,
  styles: [`
    .toast-container {
      position: fixed;
      top: 20px;
      right: 20px;
      z-index: 9999;
      display: flex;
      flex-direction: column;
      gap: 8px;
      max-width: 400px;
    }

    .toast {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 14px 16px;
      border-radius: 10px;
      color: #fff;
      font-size: 14px;
      box-shadow: 0 4px 16px rgba(0,0,0,0.15);
      animation: slideIn 0.3s ease;
      cursor: pointer;
      min-width: 280px;
    }

    .toast-success { background: #2e7d32; }
    .toast-error { background: #c62828; }
    .toast-warning { background: #e65100; }
    .toast-info { background: #1565c0; }

    .toast-icon { font-size: 20px; flex-shrink: 0; }
    .toast-message { flex: 1; line-height: 1.4; }

    .toast-acao {
      background: rgba(255,255,255,0.18); border: 1px solid rgba(255,255,255,0.5);
      color: #fff; font-weight: 600; font-size: 13px; padding: 6px 12px;
      border-radius: 6px; cursor: pointer; flex-shrink: 0;
      &:hover { background: rgba(255,255,255,0.3); }
    }

    .toast-close {
      background: none; border: none; color: rgba(255,255,255,0.7);
      cursor: pointer; padding: 2px; flex-shrink: 0;
      .material-icons { font-size: 16px; }
      &:hover { color: #fff; }
    }

    @keyframes slideIn {
      from { transform: translateX(100%); opacity: 0; }
      to { transform: translateX(0); opacity: 1; }
    }
  `]
})
export class ToastComponent {
  toastService = inject(ToastService);

  getIcon(type: string): string {
    const icons: Record<string, string> = {
      success: 'check_circle',
      error: 'error',
      warning: 'warning',
      info: 'info'
    };
    return icons[type] || 'info';
  }
}
