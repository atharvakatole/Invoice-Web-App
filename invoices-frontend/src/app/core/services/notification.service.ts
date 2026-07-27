import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface AppNotification {
  id: string;
  type: string;
  title: string;
  message: string;
  isRead: boolean;
  linkPath?: string;
  relatedEntityId?: string;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private base = `${environment.apiUrl}/notifications`;

  unreadCount = signal(0);

  constructor(private http: HttpClient) {}

  loadUnreadCount() {
    this.http.get<{ count: number }>(`${this.base}/unread-count`).subscribe({
      next: (r) => this.unreadCount.set(r.count),
      error: () => {}
    });
  }

  getNotifications(unreadOnly = false): Observable<AppNotification[]> {
    return this.http.get<AppNotification[]>(this.base, {
      params: unreadOnly ? { unreadOnly: 'true' } : {}
    });
  }

  markRead(id: string): Observable<any> {
    return this.http.put(`${this.base}/${id}/read`, {}).pipe(
      tap(() => this.unreadCount.update(c => Math.max(0, c - 1)))
    );
  }

  markAllRead(): Observable<any> {
    return this.http.put(`${this.base}/read-all`, {}).pipe(
      tap(() => this.unreadCount.set(0))
    );
  }

  delete(id: string): Observable<any> {
    return this.http.delete(`${this.base}/${id}`);
  }

  clearAll(): Observable<any> {
    return this.http.delete(`${this.base}/clear-all`).pipe(
      tap(() => this.unreadCount.set(0))
    );
  }
}
