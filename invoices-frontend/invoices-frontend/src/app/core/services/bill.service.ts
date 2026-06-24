import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface BillItem {
  id: string;
  itemName: string;
  quantity: number;
  pricePerItem: number;
  totalCost: number;
  isRefundable: boolean;
  returnByDate?: string | null;
  quantityReturned: number;
  amountRefunded: number;
  quantityBoughtByClient: number;
  amountBoughtByClient: number;
  boughtByClientName?: string;
  boughtByClientId?: string;
  draftInvoiceId?: string;
  quantityPending: number;
  hasImage: boolean;
  notes?: string;
}

export interface Bill {
  id: string;
  projectName: string;
  brandName: string;
  billDate: string;
  paidWith: string;
  notes?: string;
  items: BillItem[];
  totalCost: number;
  totalRefunded: number;
  totalBought: number;
  totalPending: number;
}

export interface BillItemPayload {
  itemName: string;
  quantity: number;
  pricePerItem: number;
  isRefundable: boolean;
  returnByDate?: string | null;
  notes?: string;
}

export interface CreateBillPayload {
  projectId?: string;
  projectName: string;
  brandName: string;
  billDate: string;
  paidWith: string;
  notes?: string;
  items: BillItemPayload[];
}

export const PAYMENT_METHODS = ['UPI', 'Card', 'Cash', 'Bank Transfer', 'Net Banking', 'Other'];

@Injectable({ providedIn: 'root' })
export class BillService {
  private base = `${environment.apiUrl}/bills`;

  constructor(private http: HttpClient) {}

  getBills(project?: string): Observable<Bill[]> {
    const params = project ? { project } : undefined;
    return this.http.get<Bill[]>(this.base, { params });
  }

  getProjects(): Observable<string[]> {
    return this.http.get<string[]>(`${this.base}/projects`);
  }

  createBill(payload: CreateBillPayload): Observable<Bill> {
    return this.http.post<Bill>(this.base, payload);
  }

  updateBill(id: string, payload: CreateBillPayload): Observable<Bill> {
    return this.http.put<Bill>(`${this.base}/${id}`, payload);
  }

  deleteBill(id: string): Observable<any> {
    return this.http.delete(`${this.base}/${id}`);
  }

  addItem(billId: string, payload: BillItemPayload): Observable<BillItem> {
    return this.http.post<BillItem>(`${this.base}/${billId}/items`, payload);
  }

  deleteItem(billId: string, itemId: string): Observable<any> {
    return this.http.delete(`${this.base}/${billId}/items/${itemId}`);
  }

  returnItems(billId: string, itemId: string, quantityToReturn: number): Observable<BillItem> {
    return this.http.put<BillItem>(`${this.base}/${billId}/items/${itemId}/return`, { quantityToReturn });
  }

  sellToClient(billId: string, itemId: string, payload: { quantityToSell: number; clientName: string; clientId?: string }): Observable<BillItem> {
    return this.http.put<BillItem>(`${this.base}/${billId}/items/${itemId}/sell`, payload);
  }

  uploadItemImage(billId: string, itemId: string, file: File): Observable<any> {
    const fd = new FormData();
    fd.append('image', file, file.name);
    return this.http.post(`${this.base}/${billId}/items/${itemId}/image`, fd);
  }

  deleteItemImage(billId: string, itemId: string): Observable<any> {
    return this.http.delete(`${this.base}/${billId}/items/${itemId}/image`);
  }

  itemImageUrl(billId: string, itemId: string): string {
    return `${this.base}/${billId}/items/${itemId}/image`;
  }
}
