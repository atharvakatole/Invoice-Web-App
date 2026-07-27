import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { InvoiceService } from '../../core/services/invoice.service';
import { ToastService } from '../../core/services/toast.service';
import { DashboardSummary, RecentInvoice, PaymentStatusLabel, PaymentStatusBadge } from '../../core/models/models';

const MONTH_NAMES = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, CurrencyPipe, DatePipe],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  loading = signal(true);
  summary = signal<DashboardSummary | null>(null);
  PaymentStatusLabel = PaymentStatusLabel;
  PaymentStatusBadge = PaymentStatusBadge;

  recentInvoices = computed(() => this.summary()?.recentInvoices ?? []);

  chartBars = computed(() => {
    const data = this.summary()?.monthlyRevenue ?? [];
    const max = Math.max(1, ...data.map(d => d.revenue));
    const map = new Map(data.map(d => [d.month, d.revenue]));
    return MONTH_NAMES.map((name, i) => {
      const value = map.get(i + 1) ?? 0;
      return { name, value, pct: Math.round((value / max) * 100) };
    });
  });

  statusBars = computed(() => {
    const data = this.summary()?.statusDistribution ?? [];
    const total = data.reduce((s, d) => s + d.count, 0) || 1;
    const colorMap: Record<string, string> = {
      Paid: 'badge-paid',
      Pending: 'badge-pending',
      PartiallyPaid: 'badge-partial',
      Overdue: 'badge-overdue',
      Cancelled: 'badge-cancelled'
    };
    return data.map(d => ({
      ...d,
      pct: Math.round((d.count / total) * 100),
      badgeClass: colorMap[d.status] ?? 'badge-pending'
    }));
  });

  statusLabel(status: string): string {
    const labels: Record<string, string> = {
      Paid: 'Paid',
      Pending: 'Pending',
      PartiallyPaid: 'Partially Paid',
      Overdue: 'Overdue',
      Cancelled: 'Cancelled'
    };
    return labels[status] ?? status;
  }

  constructor(private invoiceService: InvoiceService, private toast: ToastService) {}

  ngOnInit() {
    this.invoiceService.getDashboardSummary().subscribe({
      next: (s) => { this.summary.set(s); this.loading.set(false); },
      error: () => { this.loading.set(false); this.toast.error('Could not load dashboard'); }
    });
  }
}
