import { Component, OnInit, signal } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { InvoiceService } from '../../../core/services/invoice.service';
import { ToastService } from '../../../core/services/toast.service';
import { ClientLedger, PaymentStatusLabel, PaymentStatusBadge } from '../../../core/models/models';

@Component({
  selector: 'app-client-ledger',
  standalone: true,
  imports: [CommonModule, RouterLink, CurrencyPipe, DatePipe],
  templateUrl: './client-ledger.component.html',
  styleUrl: './client-ledger.component.scss'
})
export class ClientLedgerComponent implements OnInit {
  loading = signal(true);
  ledger = signal<ClientLedger | null>(null);
  PaymentStatusLabel = PaymentStatusLabel;
  PaymentStatusBadge = PaymentStatusBadge;

  constructor(
    private route: ActivatedRoute,
    private invoiceService: InvoiceService,
    private toast: ToastService
  ) {}

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;
    this.invoiceService.getClientLedger(id).subscribe({
      next: (l) => { this.ledger.set(l); this.loading.set(false); },
      error: () => { this.loading.set(false); this.toast.error('Could not load client ledger'); }
    });
  }

  initials(name?: string): string {
    if (!name) return '?';
    return name.split(' ').map(p => p[0]).join('').slice(0, 2).toUpperCase();
  }

  collectionPct(): number {
    const s = this.ledger()?.summary;
    if (!s || !s.totalBilled) return 0;
    return Math.round((s.totalPaid / s.totalBilled) * 100);
  }
}
