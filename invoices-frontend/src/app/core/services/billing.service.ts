import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class BillingService {
  private base = `${environment.apiUrl}/billing`;

  constructor(private http: HttpClient) {}

  createOrder(amount: number): Observable<any> {
    return this.http.post(`${this.base}/create-order`, { amount });
  }

  verifyPayment(payload: any): Observable<any> {
    return this.http.post(`${this.base}/verify-payment`, payload);
  }

  getSubscriptionStatus(): Observable<any> {
    return this.http.get(`${this.base}/subscription-status`);
  }
}
