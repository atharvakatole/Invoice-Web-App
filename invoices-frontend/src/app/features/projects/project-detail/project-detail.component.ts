import { Component, OnInit, signal } from '@angular/core';
import { CommonModule, CurrencyPipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-project-detail',
  standalone: true,
  imports: [CommonModule, CurrencyPipe, DecimalPipe, RouterLink],
  templateUrl: './project-detail.component.html',
  styleUrl: './project-detail.component.scss'
})
export class ProjectDetailComponent implements OnInit {
  loading = signal(true);
  profit = signal<any>(null);

  constructor(
    private route: ActivatedRoute,
    private http: HttpClient,
    private toast: ToastService
  ) {}

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;
    this.http.get<any>(`${environment.apiUrl}/projects/${id}/profit`).subscribe({
      next: data => { this.profit.set(data); this.loading.set(false); },
      error: () => { this.loading.set(false); this.toast.error('Could not load project analysis'); }
    });
  }

  insightClass(type: string): string {
    const map: Record<string, string> = {
      success: 'insight-success', warning: 'insight-warning',
      danger: 'insight-danger', action: 'insight-action', info: 'insight-info'
    };
    return map[type] ?? 'insight-info';
  }

  get marginColor(): string {
    const m = this.profit()?.profit?.margin ?? 0;
    if (m < 0) return 'text-red';
    if (m < 20) return 'text-gold';
    return 'text-green';
  }
}
