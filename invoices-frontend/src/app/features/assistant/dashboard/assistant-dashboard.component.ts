import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule, DatePipe, CurrencyPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AssistantPortalService, AssistantAssignment } from '../../../core/services/assistant-portal.service';
import { ToastService } from '../../../core/services/toast.service';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-assistant-dashboard',
  standalone: true,
  imports: [CommonModule, DatePipe, CurrencyPipe, RouterLink],
  templateUrl: './assistant-dashboard.component.html',
  styleUrl: './assistant-dashboard.component.scss'
})
export class AssistantDashboardComponent implements OnInit {
  loading = signal(true);
  assignments = signal<AssistantAssignment[]>([]);
  returnRequests = signal<any[]>([]);

  pending = computed(() => this.assignments().filter(a => a.status === 'Pending'));
  active = computed(() => this.assignments().filter(a => a.status === 'Accepted'));
  totalEarnings = computed(() => this.assignments().filter(a => a.isPaid).reduce((s, a) => s + a.fee, 0));
  pendingPay = computed(() => this.assignments().filter(a => !a.isPaid && a.status === 'Accepted').reduce((s, a) => s + a.fee, 0));
  pendingReturns = computed(() => this.returnRequests().filter(r => r.status === 'Pending').length);

  upcomingDates = computed(() => {
    const today = new Date(); today.setHours(0,0,0,0);
    return this.active()
      .flatMap(a => a.workDates.map(d => ({
        date: new Date(d), project: a.projectName, business: a.businessName
      })))
      .filter(d => d.date >= today)
      .sort((a,b) => a.date.getTime() - b.date.getTime())
      .slice(0, 5);
  });

  constructor(
    private service: AssistantPortalService,
    private toast: ToastService,
    public auth: AuthService
  ) {}

  ngOnInit() {
    this.service.getAssignments().subscribe({
      next: a => this.assignments.set(a),
      error: () => this.toast.error('Could not load assignments')
    });
    this.service.getMyReturnRequests().subscribe({
      next: r => { this.returnRequests.set(r); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  respond(a: AssistantAssignment, response: 'accept' | 'reject') {
    this.service.respondToAssignment(a.id, response).subscribe({
      next: () => {
        this.assignments.update(list => list.map(x =>
          x.id === a.id ? { ...x, status: response === 'accept' ? 'Accepted' : 'Rejected' } : x
        ));
        this.toast.success(response === 'accept' ? 'Assignment accepted!' : 'Assignment declined');
      },
      error: err => this.toast.error(err?.error || 'Could not respond')
    });
  }
}
