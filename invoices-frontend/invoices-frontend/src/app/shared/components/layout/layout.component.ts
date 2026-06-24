import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { BillingService } from '../../../core/services/billing.service';
import { UserRole } from '../../../core/models/models';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './layout.component.html',
  styleUrl: './layout.component.scss'
})
export class LayoutComponent implements OnInit {
  sidebarOpen = signal(false);
  UserRole = UserRole;

  planName = signal<string>('');
  isPremium = signal(false);

  constructor(
    public auth: AuthService,
    public notifService: NotificationService,
    private billingService: BillingService
  ) {}

  ngOnInit() {
    if (this.auth.isAuthenticated()) {
      this.notifService.loadUnreadCount();
      this.billingService.getSubscriptionStatus().subscribe({
        next: (s) => {
          this.planName.set(s.plan || 'Free');
          this.isPremium.set(!!s.isPremium);
        },
        error: () => {}
      });
    }
  }

  toggleSidebar() {
    this.sidebarOpen.update(v => !v);
  }

  closeSidebar() {
    this.sidebarOpen.set(false);
  }

  initials(): string {
    const name = this.auth.user()?.fullName || 'U';
    return name.split(' ').map((p: string) => p[0]).join('').slice(0, 2).toUpperCase();
  }

  logout() {
    this.auth.logout();
  }
}
