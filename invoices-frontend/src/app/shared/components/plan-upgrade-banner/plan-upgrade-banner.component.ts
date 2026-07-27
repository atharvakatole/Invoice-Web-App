import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-plan-upgrade-banner',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="upgrade-banner" *ngIf="message">
      <div class="upgrade-icon">⭐</div>
      <div class="upgrade-text">
        <strong>{{ message }}</strong>
        <span>Unlock unlimited access with Premium.</span>
      </div>
      <a routerLink="/app/billing" class="btn btn-primary btn-sm upgrade-btn">Upgrade to Premium</a>
    </div>
  `,
  styles: [`
    .upgrade-banner {
      display: flex; align-items: center; gap: 16px;
      background: linear-gradient(135deg, rgba(245,196,81,0.15), rgba(79,124,255,0.10));
      border: 1px solid rgba(245,196,81,0.4);
      border-radius: var(--radius-md); padding: 16px 20px; margin-bottom: 20px;
    }
    .upgrade-icon { font-size: 28px; flex-shrink: 0; }
    .upgrade-text { flex: 1; display: flex; flex-direction: column; gap: 2px; }
    .upgrade-text strong { font-size: 14px; }
    .upgrade-text span { font-size: 12px; color: var(--text-dim); }
    .upgrade-btn { white-space: nowrap; flex-shrink: 0; }
  `]
})
export class PlanUpgradeBannerComponent {
  @Input() message: string | null = null;
}
