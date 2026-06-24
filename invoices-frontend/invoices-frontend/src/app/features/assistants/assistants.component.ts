import { Component, OnInit, signal } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AssistantService, Assistant, Assignment } from '../../core/services/assistant.service';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-assistants',
  standalone: true,
  imports: [CommonModule, FormsModule, CurrencyPipe, DatePipe],
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
  projectName = signal('');
  fee = signal<number>(0);
  isPaid = signal(false);
  notes = signal('');
  workDates = signal<string[]>([]);
  newDate = signal<string>('');

  filterPaid = signal<'all' | 'paid' | 'unpaid'>('all');
  filterName = signal('');

  constructor(private assistantService: AssistantService, private toast: ToastService) {}

  ngOnInit() {
    this.refresh();
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
    if (!this.projectName().trim()) {
      this.toast.error('Enter a project name');
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
      projectName: this.projectName().trim(),
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
      },
      error: (err) => {
        this.submitting.set(false);
        this.toast.error(err?.error || 'Could not save assignment');
      }
    });
  }

  resetForm() {
    this.assistantMode.set('new');
    this.selectedAssistantId.set('');
    this.newAssistantName.set('');
    this.newAssistantPhone.set('');
    this.projectName.set('');
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
