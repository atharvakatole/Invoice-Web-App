import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-toast',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="toast-host">
      <div class="toast" [ngClass]="t.type" *ngFor="let t of toast.toasts()">
        <div class="toast-body">
          <span class="toast-icon" *ngIf="t.type === 'upgrade'">⭐</span>
          <span>{{ t.message }}</span>
        </div>
        <a *ngIf="t.link" [routerLink]="t.link" class="toast-link" (click)="toast.dismiss(t.id)">
          {{ t.linkLabel || 'View' }}
        </a>
        <button class="toast-close" (click)="toast.dismiss(t.id)">✕</button>
      </div>
    </div>
  `
})
export class ToastComponent {
  constructor(public toast: ToastService) {}
}
