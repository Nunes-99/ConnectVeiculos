import { Injectable } from '@angular/core';

export interface Toast {
  id: number;
  message: string;
  type: 'success' | 'error' | 'warning' | 'info';
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  toasts: Toast[] = [];
  private nextId = 0;

  show(message: string, type: Toast['type'] = 'info', duration = 4000): void {
    const id = this.nextId++;
    this.toasts.push({ id, message, type });
    if (duration > 0) {
      setTimeout(() => this.remove(id), duration);
    }
  }

  success(message: string): void { this.show(message, 'success'); }
  error(message: string): void { this.show(message, 'error', 6000); }
  warning(message: string): void { this.show(message, 'warning', 5000); }
  info(message: string): void { this.show(message, 'info'); }

  remove(id: number): void {
    this.toasts = this.toasts.filter(t => t.id !== id);
  }
}
