import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export interface ProjectDropdownItem {
  id?: string;       // set for Projects table items
  name: string;
}

/**
 * Shared service that loads the unified project list for dropdowns.
 * Used by Assistants, Bills, Calendar, and any other module that needs
 * to let the user pick a project. Calls the bills/projects endpoint
 * which aggregates Projects table + invoice items + calendar events + bills.
 */
@Injectable({ providedIn: 'root' })
export class ProjectDropdownService {
  projects = signal<string[]>([]);
  loaded = signal(false);

  constructor(private http: HttpClient) {}

  load() {
    this.http.get<string[]>(`${environment.apiUrl}/bills/projects`).subscribe({
      next: (p) => { this.projects.set(p); this.loaded.set(true); },
      error: () => {}
    });
  }

  refresh() {
    this.loaded.set(false);
    this.load();
  }
}
