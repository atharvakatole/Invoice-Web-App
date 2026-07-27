import { Injectable, signal } from '@angular/core';

export interface Toast {
  id: number;
  message: string;
  type: 'success' | 'error' | 'info' | 'upgrade';
  link?: string;
  linkLabel?: string;
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  toasts = signal<Toast[]>([]);
  private nextId = 0;

  show(message: string, type: Toast['type'] = 'info', duration = 4000, link?: string, linkLabel?: string) {
    const id = this.nextId++;
    this.toasts.update(t => [...t, { id, message, type, link, linkLabel }]);
    setTimeout(() => this.dismiss(id), duration);
  }

  upgrade(message: string) {
    this.show(message, 'upgrade', 8000, '/app/billing', 'Upgrade to Premium →');
  }

  success(message: string) { this.show(message, 'success'); }
  error(message: string) { this.show(message, 'error'); }
  info(message: string) { this.show(message, 'info'); }

  dismiss(id: number) {
    this.toasts.update(t => t.filter(x => x.id !== id));
  }
}
