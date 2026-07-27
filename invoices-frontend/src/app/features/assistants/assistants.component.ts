import { Component, DestroyRef, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AssistantService, Assistant, Assignment } from '../../core/services/assistant.service';
import { ProjectDropdownService } from '../../core/services/project-dropdown.service';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-assistants',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, CurrencyPipe, DatePipe],
  templateUrl: './assistants.component.html',
  styleUrl: './assistants.component.scss'
})
export class AssistantsComponent implements OnInit {
  loading = signal(true);
  assistants = signal<Assistant[]>([]);
  assignments = signal<Assignment[]>([]);
  submitting = signal(false);

  // form state
  assistantMode = signal<'existing' | 'new'>('new');
  selectedAssistantId = signal<string>('');
  newAssistantName = signal('');
  newAssistantPhone = signal('');
  newAssistantEmail = signal('');

  // project selection — dropdown + optional custom entry
  selectedProject = signal('');
  customProjectName = signal('');
  fee = signal<number>(0);
  isPaid = signal(false);
  notes = signal('');
  workDates = signal<string[]>([]);
  newDate = signal<string>('');

  filterPaid = signal<'all' | 'paid' | 'unpaid'>('all');
  filterName = signal('');

  constructor(
    private assistantService: AssistantService,
    public projectDropdown: ProjectDropdownService,
    private toast: ToastService,
    private route: ActivatedRoute,
    private destroyRef: DestroyRef
  ) {}

  ngOnInit() {
    this.projectDropdown.load();
    this.refresh();

    // Pre-fill project from query param (when navigating from Projects page)
    this.route.queryParams.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      if (params['projectId']) {
        // projectId is a GUID — we need to find the project name
        // The dropdown loads project names; the Projects page also passes projectId
        // We store it and resolve after dropdown loads
        this._pendingProjectId = params['projectId'];
        this._resolvePendingProject();
      }
    });
  }

  private _pendingProjectId: string | null = null;

  private _resolvePendingProject() {
    if (!this._pendingProjectId) return;
    // Try to find the name from already-loaded projects
    // We can't directly map GUID → name from bills/projects endpoint (names only).
    // Simplest: set the custom project field with the project ID and let user select.
    // Better: the project link from Projects page should pass projectName too.
    // We handle this via queryParam projectName instead.
    const projectName = this.route.snapshot.queryParamMap.get('projectName');
    if (projectName) {
      this.selectedProject.set(projectName);
    }
  }

  refresh() {
    this.loading.set(true);
    this.assistantService.getAssistants().subscribe({
      next: (a) => this.assistants.set(a),
      error: () => this.toast.error('Could not load assistants')
    });
    this.assistantService.getAssignments().subscribe({
      next: (a) => { this.assignments.set(a); this.loading.set(false); },
      error: () => { this.loading.set(false); this.toast.error('Could not load assignments'); }
    });
  }

  filteredAssignments() {
    const f = this.filterPaid();
    const name = this.filterName().trim().toLowerCase();
    return this.assignments().filter(a => {
      const matchesPaid = f === 'all' || (f === 'paid') === a.isPaid;
      const matchesName = !name ||
        a.assistantName.toLowerCase().includes(name) ||
        a.projectName.toLowerCase().includes(name);
      return matchesPaid && matchesName;
    });
  }

  resolvedProjectName(): string {
    return this.selectedProject() === '__custom__'
      ? this.customProjectName().trim()
      : this.selectedProject();
  }

  addDate() {
    const d = this.newDate();
    if (!d) return;
    if (!this.workDates().includes(d)) {
      this.workDates.update(dates => [...dates, d].sort());
    }
    this.newDate.set('');
  }

  removeDate(d: string) {
    this.workDates.update(dates => dates.filter(x => x !== d));
  }

  submit() {
    const projectName = this.resolvedProjectName();
    if (!projectName) {
      this.toast.error('Select or enter a project name');
      return;
    }
    if (this.workDates().length === 0) {
      this.toast.error('Add at least one work date');
      return;
    }
    if (this.assistantMode() === 'existing' && !this.selectedAssistantId()) {
      this.toast.error('Select an assistant');
      return;
    }
    if (this.assistantMode() === 'new' && !this.newAssistantName().trim()) {
      this.toast.error('Enter the assistant\'s name');
      return;
    }

    this.submitting.set(true);
    this.assistantService.createAssignment({
      assistantId: this.assistantMode() === 'existing' ? this.selectedAssistantId() : null,
      newAssistantName: this.assistantMode() === 'new' ? this.newAssistantName().trim() : undefined,
      newAssistantPhone: this.assistantMode() === 'new' ? this.newAssistantPhone().trim() : undefined,
      newAssistantEmail: this.assistantMode() === 'new' ? this.newAssistantEmail().trim() : undefined,
      projectName,
      workDates: this.workDates().map(d => new Date(d).toISOString()),
      fee: Number(this.fee()) || 0,
      isPaid: this.isPaid(),
      notes: this.notes().trim()
    }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.toast.success('Assignment added');
        this.resetForm();
        this.refresh();
        this.projectDropdown.refresh();
      },
      error: (err) => {
        this.submitting.set(false);
        const _em = err?.error || '';
        if (_em.includes('limit') || _em.includes('Premium')) { this.toast.upgrade(_em); } else { this.toast.error(_em || 'Could not save assignment'); }
      }
    });
  }

  resetForm() {
    this.assistantMode.set('new');
    this.selectedAssistantId.set('');
    this.newAssistantName.set('');
    this.newAssistantPhone.set('');
    this.newAssistantEmail.set('');
    this.selectedProject.set('');
    this.customProjectName.set('');
    this.fee.set(0);
    this.isPaid.set(false);
    this.notes.set('');
    this.workDates.set([]);
    this.newDate.set('');
  }

  togglePaid(a: Assignment) {
    const newVal = !a.isPaid;
    this.assistantService.setPaid(a.id, newVal).subscribe({
      next: () => {
        this.assignments.update(list => list.map(x => x.id === a.id ? { ...x, isPaid: newVal } : x));
      },
      error: () => this.toast.error('Could not update payment status')
    });
  }

  deleteAssignment(a: Assignment) {
    this.assistantService.deleteAssignment(a.id).subscribe({
      next: () => {
        this.assignments.update(list => list.filter(x => x.id !== a.id));
        this.toast.success('Assignment removed');
      },
      error: () => this.toast.error('Could not remove assignment')
    });
  }
}
