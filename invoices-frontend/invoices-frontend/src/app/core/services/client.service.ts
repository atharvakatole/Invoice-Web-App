import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface ClientSummary {
  id: string;
  clientName: string;
  clientEmail: string;
  clientPhone: string;
  clientAddress: string;
  invoiceCount: number;
  totalRevenue: number;
  pendingAmount: number;
  lastInvoiceDate: string;
}

export interface LastInvoiceItem {
  expenseName: string;
  projectName?: string;
  amount: number;
  quantity: number;
}

export interface UpdateClientPayload {
  clientName: string;
  clientEmail?: string;
  clientPhone?: string;
  clientAddress?: string;
}

@Injectable({ providedIn: 'root' })
export class ClientService {
  private base = `${environment.apiUrl}/clients`;

  constructor(private http: HttpClient) {}

  getClients(search?: string): Observable<ClientSummary[]> {
    const params = search ? { search } : undefined;
    return this.http.get<ClientSummary[]>(this.base, { params });
  }

  getClient(id: string): Observable<ClientSummary> {
    return this.http.get<ClientSummary>(`${this.base}/${id}`);
  }

  createClient(payload: UpdateClientPayload): Observable<ClientSummary> {
    return this.http.post<ClientSummary>(this.base, payload);
  }

  updateClient(id: string, payload: UpdateClientPayload): Observable<ClientSummary> {
    return this.http.put<ClientSummary>(`${this.base}/${id}`, payload);
  }

  deleteClient(id: string): Observable<any> {
    return this.http.delete(`${this.base}/${id}`);
  }

  getLastItems(clientId: string): Observable<LastInvoiceItem[]> {
    return this.http.get<LastInvoiceItem[]>(`${this.base}/${clientId}/last-items`);
  }
}
