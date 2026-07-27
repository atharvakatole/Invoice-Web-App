import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface AssistantAssignment {
  id: string;
  businessId: string;
  businessName: string;
  projectId?: string;
  projectName: string;
  workDates: string[];
  fee: number;
  isPaid: boolean;
  notes?: string;
  status: 'Pending' | 'Accepted' | 'Rejected' | 'Completed';
  createdAt: string;
}

export interface ReturnRequest {
  id: string;
  quantityToReturn: number;
  notes?: string;
  managerNotes?: string;
  status: 'Pending' | 'Approved' | 'Rejected';
  createdAt: string;
  resolvedAt?: string;
  itemName?: string;
  brandName?: string;
  projectName?: string;
}

@Injectable({ providedIn: 'root' })
export class AssistantPortalService {
  private base = `${environment.apiUrl}/assistant`;
  private managerBase = `${environment.apiUrl}/manager`;

  constructor(private http: HttpClient) {}

  // Assistant endpoints
  getMe(): Observable<any> {
    return this.http.get(`${this.base}/me`);
  }

  getAssignments(status?: string): Observable<AssistantAssignment[]> {
    const params = status ? { status } : undefined;
    return this.http.get<AssistantAssignment[]>(`${this.base}/assignments`, { params });
  }

  respondToAssignment(id: string, response: 'accept' | 'reject', reason?: string): Observable<any> {
    return this.http.put(`${this.base}/assignments/${id}/respond`, { response, reason });
  }

  getBills(): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/bills`);
  }

  addBill(payload: any): Observable<any> {
    return this.http.post(`${this.base}/bills`, payload);
  }

  submitReturnRequest(payload: { billItemId: string; assignmentId: string; quantityToReturn: number; notes?: string }): Observable<any> {
    return this.http.post(`${this.base}/return-requests`, payload);
  }

  getMyReturnRequests(): Observable<ReturnRequest[]> {
    return this.http.get<ReturnRequest[]>(`${this.base}/return-requests`);
  }

  // Manager endpoints
  inviteAssistant(payload: { name: string; email: string; phone?: string }): Observable<any> {
    return this.http.post(`${this.managerBase}/invite-assistant`, payload);
  }

  getReturnRequests(status?: string): Observable<any[]> {
    const params = status ? { status } : undefined;
    return this.http.get<any[]>(`${this.managerBase}/return-requests`, { params });
  }

  resolveReturnRequest(id: string, resolution: 'approve' | 'reject', managerNotes?: string): Observable<any> {
    return this.http.put(`${this.managerBase}/return-requests/${id}/resolve`, { resolution, managerNotes });
  }

  getPendingReturnCount(): Observable<{ count: number }> {
    return this.http.get<{ count: number }>(`${this.managerBase}/pending-return-count`);
  }
}
