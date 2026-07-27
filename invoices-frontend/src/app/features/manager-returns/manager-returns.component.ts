import { Component, OnInit, signal } from '@angular/core';
import { CommonModule, DatePipe, CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AssistantPortalService } from '../../core/services/assistant-portal.service';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-manager-returns',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePipe, CurrencyPipe],
  templateUrl: './manager-returns.component.html',
  styleUrl: './manager-returns.component.scss'
})
export class ManagerReturnsComponent implements OnInit {
  loading = signal(true);
  requests = signal<any[]>([]);
  filterStatus = signal('Pending');
  rejectModal = signal<any>(null);
  managerNotes = signal('');
  resolving = signal<string | null>(null);

  constructor(private service: AssistantPortalService, private toast: ToastService) {}

  ngOnInit() { this.load(); }

  load() {
    const s = this.filterStatus() === 'All' ? undefined : this.filterStatus();
    this.service.getReturnRequests(s).subscribe({
      next: r => { this.requests.set(r); this.loading.set(false); },
      error: () => { this.loading.set(false); this.toast.error('Could not load return requests'); }
    });
  }

  approve(r: any) {
    this.resolving.set(r.id);
    this.service.resolveReturnRequest(r.id, 'approve').subscribe({
      next: () => { this.resolving.set(null); this.toast.success('Return approved!'); this.load(); },
      error: err => { this.resolving.set(null); this.toast.error(err?.error || 'Failed'); }
    });
  }

  openReject(r: any) { this.rejectModal.set(r); this.managerNotes.set(''); }

  confirmReject() {
    const r = this.rejectModal();
    if (!r) return;
    this.resolving.set(r.id);
    this.service.resolveReturnRequest(r.id, 'reject', this.managerNotes()).subscribe({
      next: () => {
        this.resolving.set(null);
        this.rejectModal.set(null);
        this.toast.success('Return request rejected');
        this.load();
      },
      error: err => { this.resolving.set(null); this.toast.error(err?.error || 'Failed'); }
    });
  }
}
