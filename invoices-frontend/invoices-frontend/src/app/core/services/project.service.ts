import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Project {
  id: string;
  clientId: string;
  clientName: string;
  name: string;
  description?: string;
  status: 'Active' | 'Completed' | 'Archived';
  startDate?: string;
  endDate?: string;
  budget?: number;
  notes?: string;
  invoiceCount: number;
  assistantCount: number;
  billCount: number;
  totalInvoiced: number;
  createdAt: string;
}

export interface ProjectPayload {
  clientId: string;
  name: string;
  description?: string;
  startDate?: string;
  endDate?: string;
  budget?: number;
  notes?: string;
  status?: string;
}

@Injectable({ providedIn: 'root' })
export class ProjectService {
  private base = `${environment.apiUrl}/projects`;

  constructor(private http: HttpClient) {}

  getProjects(status?: string, clientId?: string): Observable<Project[]> {
    const params: Record<string, string> = {};
    if (status) params['status'] = status;
    if (clientId) params['clientId'] = clientId;
    return this.http.get<Project[]>(this.base, { params });
  }

  getProject(id: string): Observable<any> {
    return this.http.get<any>(`${this.base}/${id}`);
  }

  createProject(payload: ProjectPayload): Observable<Project> {
    return this.http.post<Project>(this.base, payload);
  }

  updateProject(id: string, payload: ProjectPayload): Observable<Project> {
    return this.http.put<Project>(`${this.base}/${id}`, payload);
  }

  deleteProject(id: string): Observable<any> {
    return this.http.delete(`${this.base}/${id}`);
  }
}
