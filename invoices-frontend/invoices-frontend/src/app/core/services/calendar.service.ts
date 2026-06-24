import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface CalendarEvent {
  date: string;
  type: 'invoice-item' | 'assistant' | 'project';
  title: string;
  subtitle?: string;
  amount?: number;
  isPaid?: boolean;
  relatedId?: string;
}

@Injectable({ providedIn: 'root' })
export class CalendarService {
  private base = `${environment.apiUrl}/invoices/calendar-events`;

  constructor(private http: HttpClient) {}

  getEvents(year: number, month: number): Observable<CalendarEvent[]> {
    return this.http.get<CalendarEvent[]>(this.base, { params: { year, month } });
  }

  createEvent(title: string, date: string, notes?: string): Observable<CalendarEvent> {
    return this.http.post<CalendarEvent>(this.base, { title, date, notes });
  }

  deleteEvent(id: string): Observable<any> {
    return this.http.delete(`${this.base}/${id}`);
  }
}
