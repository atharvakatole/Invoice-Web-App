import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { InvoiceService } from '../../core/services/invoice.service';
import { ToastService } from '../../core/services/toast.service';
import { GstSummary } from '../../core/models/models';

const MONTH_NAMES = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [CommonModule, CurrencyPipe],
  templateUrl: './reports.component.html',
  styleUrl: './reports.component.scss'
})
export class ReportsComponent implements OnInit {
  loading = signal(true);
  gst = signal<GstSummary | null>(null);
  exportingExcel = signal(false);
  exportingPdf = signal(false);

  rows = computed(() => {
    const data = this.gst()?.monthlySummary ?? [];
    return data.map(d => ({ ...d, name: MONTH_NAMES[d.month - 1] }));
  });

  chartBars = computed(() => {
    const data = this.rows();
    const max = Math.max(1, ...data.map(d => d.gstCollected));
    return data.map(d => ({ ...d, pct: Math.round((d.gstCollected / max) * 100) }));
  });

  constructor(private invoiceService: InvoiceService, private toast: ToastService) {}

  ngOnInit() {
    this.invoiceService.getGstSummary().subscribe({
      next: (g) => { this.gst.set(g); this.loading.set(false); },
      error: () => { this.loading.set(false); this.toast.error('Could not load GST summary'); }
    });
  }

  exportExcel() {
    this.exportingExcel.set(true);
    this.invoiceService.getFile(this.invoiceService.exportFiscalYearExcelUrl()).subscribe({
      next: (blob) => {
        this.downloadBlob(blob, `FiscalYearReport-${new Date().getFullYear()}.xlsx`);
        this.exportingExcel.set(false);
      },
      error: () => { this.exportingExcel.set(false); this.toast.error('Could not export Excel report'); }
    });
  }

  exportPdf() {
    this.exportingPdf.set(true);
    this.invoiceService.getFile(this.invoiceService.exportFiscalYearPdfUrl()).subscribe({
      next: (blob) => {
        this.downloadBlob(blob, `FiscalYearReport-${new Date().getFullYear()}.pdf`);
        this.exportingPdf.set(false);
      },
      error: () => { this.exportingPdf.set(false); this.toast.error('Could not export PDF report'); }
    });
  }

  private downloadBlob(blob: Blob, filename: string) {
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    window.URL.revokeObjectURL(url);
  }
}
