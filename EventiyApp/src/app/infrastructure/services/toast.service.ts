import { Injectable, signal } from '@angular/core';

export interface Toast {
  id: number;
  type: 'success' | 'error' | 'info';
  message: string;
}

let nextId = 0;

@Injectable({ providedIn: 'root' })
export class ToastService {
  readonly toasts = signal<Toast[]>([]);

  private add(type: Toast['type'], message: string): void {
    this.toasts.update(prev => [...prev, { id: nextId++, type, message }]);
  }

  showError(message: string): void { this.add('error', message); }
  showSuccess(message: string): void { this.add('success', message); }
  showInfo(message: string): void { this.add('info', message); }

  dismiss(id: number): void {
    this.toasts.update(prev => prev.filter(t => t.id !== id));
  }
}
