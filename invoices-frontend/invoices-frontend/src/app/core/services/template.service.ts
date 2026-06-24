import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface TemplateStatus {
  hasTemplate: boolean;
  fileName?: string;
  updatedAt?: string;
  detectedFields: string[];
  missingFields: string[];
}

@Injectable({ providedIn: 'root' })
export class TemplateService {
  private base = `${environment.apiUrl}/invoice-template`;

  constructor(private http: HttpClient) {}

  getStatus(): Observable<TemplateStatus> {
    return this.http.get<TemplateStatus>(`${this.base}/status`);
  }

  upload(file: File): Observable<TemplateStatus> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.http.post<TemplateStatus>(`${this.base}/upload`, formData);
  }

  delete(): Observable<any> {
    return this.http.delete(this.base);
  }

  previewUrl(): string {
    return `${this.base}/preview`;
  }

  getFile(url: string): Observable<Blob> {
    return this.http.get(url, { responseType: 'blob' });
  }
}
