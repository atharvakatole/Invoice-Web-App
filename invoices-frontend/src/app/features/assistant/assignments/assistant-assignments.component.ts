import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule, DatePipe, CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AssistantPortalService, AssistantAssignment } from '../../../core/services/assistant-portal.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-assistant-assignments',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePipe, CurrencyPipe],
  templateUrl: './assistant-assignments.component.html',
  styleUrl: './assistant-assignments.component.scss'
})
export class AssistantAssignmentsComponent implements OnInit {
  loading = signal(true);
  assignments = signal<AssistantAssignment[]>([]);
  filterStatus = signal<string>('all');
  rejectModal = signal<AssistantAssignment | null>(null);
  rejectReason = signal('');

  filtered = computed(() => {
    const f = this.filterStatus();
    if (f === 'all') return this.assignments();
    return this.assignments().filter(a => a.status.toLowerCase() === f.toLowerCase());
  });

  constructor(private service: AssistantPortalService, private toast: ToastService) {}

  ngOnInit() {
    this.service.getAssignments().subscribe({
      next: a => { this.assignments.set(a); this.loading.set(false); },
      error: () => { this.loading.set(false); this.toast.error('Could not load assignments'); }
    });
  }

  accept(a: AssistantAssignment) {
    this.service.respondToAssignment(a.id, 'accept').subscribe({
      next: () => {
        this.assignments.update(l => l.map(x => x.id === a.id ? { ...x, status: 'Accepted' as any } : x));
        this.toast.success('Assignment accepted!');
      },
      error: err => this.toast.error(err?.error || 'Failed')
    });
  }

  openReject(a: AssistantAssignment) {
    this.rejectModal.set(a);
    this.rejectReason.set('');
  }

  confirmReject() {
    const a = this.rejectModal();
    if (!a) return;
    this.service.respondToAssignment(a.id, 'reject', this.rejectReason()).subscribe({
      next: () => {
        this.assignments.update(l => l.map(x => x.id === a.id ? { ...x, status: 'Rejected' as any } : x));
        this.rejectModal.set(null);
        this.toast.success('Assignment declined');
      },
      error: err => this.toast.error(err?.error || 'Failed')
    });
  }

  statusClass(s: string) {
    if (s === 'Accepted') return 'badge-paid';
    if (s === 'Rejected') return 'badge-overdue';
    if (s === 'Completed') return 'badge-partial';
    return 'badge-pending';
  }
}
