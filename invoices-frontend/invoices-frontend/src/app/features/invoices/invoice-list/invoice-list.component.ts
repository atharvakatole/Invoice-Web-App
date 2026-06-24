import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { InvoiceService } from '../../../core/services/invoice.service';
import { ToastService } from '../../../core/services/toast.service';
import { Invoice, PaymentStatus, PaymentStatusLabel, PaymentStatusBadge } from '../../../core/models/models';

@Component({
  selector: 'app-invoice-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, CurrencyPipe, DatePipe],
  templateUrl: './invoice-list.component.html',
  styleUrl: './invoice-list.component.scss'
})
export class InvoiceListComponent implements OnInit {
  loading = signal(true);
  invoices = signal<Invoice[]>([]);
  search = signal('');
  statusFilter = signal<number | 'all'>('all');

  PaymentStatusLabel = PaymentStatusLabel;
  PaymentStatusBadge = PaymentStatusBadge;
  PaymentStatus = PaymentStatus;
  statusOptions = [
    { value: 'all', label: 'All Statuses' },
    { value: PaymentStatus.Pending, label: 'Pending' },
    { value: PaymentStatus.PartiallyPaid, label: 'Partially Paid' },
    { value: PaymentStatus.Paid, label: 'Paid' },
    { value: PaymentStatus.Overdue, label: 'Overdue' },
    { value: PaymentStatus.Cancelled, label: 'Cancelled' },
  ];

  payModalInvoice = signal<Invoice | null>(null);
  payAmount = signal<number>(0);
  paySubmitting = signal(false);

  filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    const status = this.statusFilter();
    return this.invoices().filter(inv => {
      const matchesSearch = !term || inv.invoiceNumber.toLowerCase().includes(term);
      const matchesStatus = status === 'all' || inv.paymentStatus === status;
      return matchesSearch && matchesStatus;
    });
  });

  totals = computed(() => {
    const list = this.invoices();
    return {
      count: list.length,
      total: list.reduce((s, i) => s + i.totalAmount, 0),
      paid: list.reduce((s, i) => s + i.amountPaid, 0),
      pending: list.reduce((s, i) => s + i.remainingAmount, 0),
    };
  });

  constructor(private invoiceService: InvoiceService, private toast: ToastService) {}

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.invoiceService.getInvoices().subscribe({
      next: (inv) => { this.invoices.set(inv); this.loading.set(false); },
      error: () => { this.loading.set(false); this.toast.error('Could not load invoices'); }
    });
  }

  openPayModal(inv: Invoice) {
    this.payModalInvoice.set(inv);
    this.payAmount.set(Number(inv.remainingAmount.toFixed(2)));
  }

  closePayModal() {
    this.payModalInvoice.set(null);
  }

  submitPayment() {
    const inv = this.payModalInvoice();
    if (!inv) return;
    const amount = this.payAmount();
    if (!amount || amount <= 0) {
      this.toast.error('Enter a valid amount');
      return;
    }
    this.paySubmitting.set(true);
    this.invoiceService.updatePayment(inv.id, amount).subscribe({
      next: () => {
        this.paySubmitting.set(false);
        this.toast.success('Payment recorded successfully');
        this.closePayModal();
        this.load();
      },
      error: (err) => {
        this.paySubmitting.set(false);
        this.toast.error(err?.error?.message || err?.error || 'Failed to record payment');
      }
    });
  }

  view(inv: Invoice) {
    this.invoiceService.getFile(this.invoiceService.previewPdfUrl(inv.id)).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        window.open(url, '_blank');
      },
      error: () => this.toast.error('Could not open invoice preview')
    });
  }

  download(inv: Invoice) {
    this.invoiceService.getFile(this.invoiceService.downloadPdfUrl(inv.id)).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${inv.invoiceNumber}.pdf`;
        a.click();
        window.URL.revokeObjectURL(url);
      },
      error: () => this.toast.error('Could not download invoice')
    });
  }
}
