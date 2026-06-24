import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateInvoiceRequest, DashboardSummary, GstSummary, Invoice, ClientLedger } from '../models/models';

@Injectable({ providedIn: 'root' })
export class InvoiceService {
  private base = `${environment.apiUrl}/invoices`;

  constructor(private http: HttpClient) {}

  getInvoices(): Observable<Invoice[]> {
    return this.http.get<Invoice[]>(this.base);
  }

  createInvoice(payload: CreateInvoiceRequest): Observable<any> {
    return this.http.post(`${this.base}/create`, payload);
  }

  getExpenseSuggestions(search: string): Observable<string[]> {
    return this.http.get<string[]>(`${this.base}/expense-suggestions`, { params: { search } });
  }

  getProjectSuggestions(search?: string): Observable<{ name: string; lastUsed: string }[]> {
    const params = search ? { q: search } : undefined;
    return this.http.get<{ name: string; lastUsed: string }[]>(`${this.base}/project-suggestions`, { params });
  }

  updatePayment(invoiceId: string, amountPaid: number): Observable<any> {
    return this.http.put(`${this.base}/update-payment/${invoiceId}`, { amountPaid });
  }

  getDashboardSummary(): Observable<DashboardSummary> {
    return this.http.get<DashboardSummary>(`${this.base}/dashboard-summary`);
  }

  getGstSummary(): Observable<GstSummary> {
    return this.http.get<GstSummary>(`${this.base}/gst-summary`);
  }

  getClientLedger(clientId: string): Observable<ClientLedger> {
    return this.http.get<ClientLedger>(`${this.base}/client-ledger/${clientId}`);
  }

  previewPdfUrl(invoiceId: string): string {
    return `${this.base}/preview-pdf/${invoiceId}`;
  }

  downloadPdfUrl(invoiceId: string): string {
    return `${this.base}/download-pdf/${invoiceId}`;
  }

  getFile(url: string): Observable<Blob> {
    return this.http.get(url, { responseType: 'blob' });
  }

  exportFiscalYearExcelUrl(): string {
    return `${this.base}/export-fiscal-year`;
  }

  exportFiscalYearPdfUrl(): string {
    return `${this.base}/export-fiscal-year-pdf`;
  }
}
