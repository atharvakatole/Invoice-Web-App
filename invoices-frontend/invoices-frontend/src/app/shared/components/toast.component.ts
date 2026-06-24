import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-toast',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="toast-host">
      <div class="toast" [ngClass]="t.type" *ngFor="let t of toast.toasts()">
        <span>{{ t.message }}</span>
      </div>
    </div>
  `
})
export class ToastComponent {
  constructor(public toast: ToastService) {}
}
