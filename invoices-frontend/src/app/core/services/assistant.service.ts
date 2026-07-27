import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Assistant {
  id: string;
  name: string;
  phone?: string;
  totalAssignments: number;
  totalUnpaid: number;
}

export interface Assignment {
  id: string;
  assistantId: string;
  assistantName: string;
  projectName: string;
  workDates: string[];
  fee: number;
  isPaid: boolean;
  notes?: string;
}

export interface CreateAssignmentPayload {
  assistantId?: string | null;
  newAssistantName?: string;
  newAssistantPhone?: string;
  newAssistantEmail?: string;
  projectName: string;
  workDates: string[];
  fee: number;
  isPaid: boolean;
  notes?: string;
}

@Injectable({ providedIn: 'root' })
export class AssistantService {
  private base = `${environment.apiUrl}/assistants`;

  constructor(private http: HttpClient) {}

  getAssistants(): Observable<Assistant[]> {
    return this.http.get<Assistant[]>(this.base);
  }

  createAssistant(name: string, phone?: string, email?: string): Observable<Assistant> {
    return this.http.post<Assistant>(this.base, { name, phone, email });
  }

  deleteAssistant(id: string): Observable<any> {
    return this.http.delete(`${this.base}/${id}`);
  }

  getAssignments(): Observable<Assignment[]> {
    return this.http.get<Assignment[]>(`${this.base}/assignments`);
  }

  createAssignment(payload: CreateAssignmentPayload): Observable<Assignment> {
    return this.http.post<Assignment>(`${this.base}/assignments`, payload);
  }

  setPaid(id: string, isPaid: boolean): Observable<any> {
    return this.http.put(`${this.base}/assignments/${id}/paid`, isPaid);
  }

  deleteAssignment(id: string): Observable<any> {
    return this.http.delete(`${this.base}/assignments/${id}`);
  }
}
