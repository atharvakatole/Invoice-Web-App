import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NotificationService, AppNotification } from '../../core/services/notification.service';
import { ToastService } from '../../core/services/toast.service';

const ICONS: Record<string, string> = {
  bill_return_due: '📦',
  bill_return_overdue: '🚨',
  invoice_overdue: '⚠️',
  invoice_due_soon: '🔔',
  assistant_unpaid: '💸',
  upcoming_project: '📅'
};

const COLORS: Record<string, string> = {
  bill_return_overdue: 'notif-red',
  invoice_overdue: 'notif-red',
  bill_return_due: 'notif-gold',
  invoice_due_soon: 'notif-gold',
  assistant_unpaid: 'notif-gold',
  upcoming_project: 'notif-blue'
};

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [CommonModule, RouterLink, DatePipe],
  templateUrl: './notifications.component.html',
  styleUrl: './notifications.component.scss'
})
export class NotificationsComponent implements OnInit {
  loading = signal(true);
  notifications = signal<AppNotification[]>([]);
  showUnreadOnly = signal(false);

  filtered = computed(() => {
    const list = this.notifications();
    return this.showUnreadOnly() ? list.filter(n => !n.isRead) : list;
  });

  unread = computed(() => this.notifications().filter(n => !n.isRead).length);

  constructor(
    private notifService: NotificationService,
    private toast: ToastService
  ) {}

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.notifService.getNotifications().subscribe({
      next: (n) => { this.notifications.set(n); this.loading.set(false); },
      error: () => { this.loading.set(false); }
    });
  }

  icon(type: string): string {
    return ICONS[type] ?? '🔔';
  }

  colorClass(type: string): string {
    return COLORS[type] ?? 'notif-blue';
  }

  markRead(n: AppNotification) {
    if (n.isRead) return;
    this.notifService.markRead(n.id).subscribe({
      next: () => this.notifications.update(list =>
        list.map(x => x.id === n.id ? { ...x, isRead: true } : x)
      )
    });
  }

  markAllRead() {
    this.notifService.markAllRead().subscribe({
      next: () => {
        this.notifications.update(list => list.map(x => ({ ...x, isRead: true })));
        this.toast.success('All marked as read');
      }
    });
  }

  delete(n: AppNotification) {
    this.notifService.delete(n.id).subscribe({
      next: () => {
        this.notifications.update(list => list.filter(x => x.id !== n.id));
        if (!n.isRead) this.notifService.unreadCount.update(c => Math.max(0, c - 1));
      }
    });
  }

  clearAll() {
    this.notifService.clearAll().subscribe({
      next: () => { this.notifications.set([]); this.toast.success('Cleared'); }
    });
  }
}
