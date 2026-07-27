import { Component, OnInit, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { AssistantPortalService, ReturnRequest } from '../../../core/services/assistant-portal.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-assistant-returns',
  standalone: true,
  imports: [CommonModule, DatePipe],
  template: `
    <div class="fade-in">
      <div class="page-header">
        <div><h1>Return Requests</h1><p class="text-dim">Track all your return requests and their approval status.</p></div>
      </div>
      <div *ngIf="loading()" class="skeleton" style="height:300px;width:100%;"></div>
      <div class="card" *ngIf="!loading()">
        <div *ngIf="!requests().length" class="empty">
          <div class="empty-icon">🔄</div>
          <h3>No return requests</h3>
          <p>Return requests you submit from the Bills page will appear here.</p>
        </div>
        <div class="return-list" *ngIf="requests().length">
          <div class="return-card" *ngFor="let r of requests()">
            <div class="return-header">
              <div>
                <strong>{{ r.itemName }}</strong>
                <span class="text-dim ml-8">{{ r.brandName }} · {{ r.projectName }}</span>
              </div>
              <span class="badge"
                [ngClass]="r.status === 'Approved' ? 'badge-paid' : r.status === 'Rejected' ? 'badge-overdue' : 'badge-pending'">
                {{ r.status }}
              </span>
            </div>
            <div class="return-details">
              <span>Qty: <strong>{{ r.quantityToReturn }}</strong></span>
              <span>Submitted: <strong>{{ r.createdAt | date:'mediumDate' }}</strong></span>
              <span *ngIf="r.resolvedAt">Resolved: <strong>{{ r.resolvedAt | date:'mediumDate' }}</strong></span>
            </div>
            <div class="return-notes" *ngIf="r.notes">
              <span class="info-label">Your notes:</span> {{ r.notes }}
            </div>
            <div class="return-notes manager-notes" *ngIf="r.managerNotes">
              <span class="info-label">Manager notes:</span> {{ r.managerNotes }}
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 20px; }
    .return-list { display: flex; flex-direction: column; gap: 14px; }
    .return-card { border: 1px solid var(--border); border-radius: var(--radius-md); padding: 16px; }
    .return-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 10px; }
    .return-details { display: flex; gap: 20px; font-size: 13px; color: var(--text-dim); margin-bottom: 6px; }
    .return-notes { font-size: 13px; color: var(--text-dim); margin-top: 6px; }
    .manager-notes { color: var(--accent-2); }
    .info-label { font-weight: 600; color: var(--text-faint); margin-right: 4px; }
    .ml-8 { margin-left: 8px; }
  `]
})
export class AssistantReturnsComponent implements OnInit {
  loading = signal(true);
  requests = signal<ReturnRequest[]>([]);

  constructor(private service: AssistantPortalService, private toast: ToastService) {}

  ngOnInit() {
    this.service.getMyReturnRequests().subscribe({
      next: r => { this.requests.set(r); this.loading.set(false); },
      error: () => { this.loading.set(false); this.toast.error('Could not load return requests'); }
    });
  }
}
