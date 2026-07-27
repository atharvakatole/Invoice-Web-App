import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ToastService } from '../../core/services/toast.service';

interface Expense {
  id: string;
  description: string;
  category: string;
  amount: number;
  expenseDate: string;
  notes?: string;
  projectId?: string;
  projectName?: string;
}

interface ExpenseSummary {
  totalAllTime: number;
  totalThisMonth: number;
  totalLastMonth: number;
  count: number;
  topCategory: string;
  categories: string[];
}

@Component({
  selector: 'app-personal-expenses',
  standalone: true,
  imports: [CommonModule, FormsModule, CurrencyPipe, DatePipe],
  templateUrl: './personal-expenses.component.html',
  styleUrl: './personal-expenses.component.scss'
})
export class PersonalExpensesComponent implements OnInit {
  private base = `${environment.apiUrl}/personal-expenses`;

  loading = signal(true);
  saving = signal(false);
  showForm = signal(false);

  expenses = signal<Expense[]>([]);
  summary = signal<ExpenseSummary | null>(null);
  byCategory = signal<{ category: string; total: number; count: number }[]>([]);

  filterCategory = signal('All');
  filterFrom = signal('');
  filterTo = signal('');

  editingId = signal<string | null>(null);

  // Form fields
  form = {
    description: '',
    category: 'Other',
    amount: 0,
    expenseDate: new Date().toISOString().slice(0, 10),
    notes: '',
    projectId: '',
    projectName: ''
  };

  categories = signal<string[]>([]);
  projects = signal<{id:string;name:string}[]>([]);

  totalFiltered = computed(() => this.expenses().reduce((s, e) => s + e.amount, 0));

  constructor(private http: HttpClient, private toast: ToastService) {}

  ngOnInit() {
    this.loadSummary();
    this.loadExpenses();
    this.http.get<any[]>(`${this.base}/projects-used`).subscribe({
      next: p => this.projects.set(p),
      error: () => {}
    });
  }

  loadSummary() {
    this.http.get<any>(`${this.base}/summary`).subscribe({
      next: s => {
        this.summary.set(s);
        this.categories.set(s.categories ?? []);
      },
      error: () => {}
    });
  }

  loadExpenses() {
    this.loading.set(true);
    const params: any = {};
    if (this.filterCategory() !== 'All') params['category'] = this.filterCategory();
    if (this.filterFrom()) params['from'] = this.filterFrom();
    if (this.filterTo()) params['to'] = this.filterTo();

    this.http.get<any>(`${this.base}`, { params }).subscribe({
      next: (data) => {
        this.expenses.set(data.items ?? []);
        this.byCategory.set(data.byCategory ?? []);
        this.loading.set(false);
      },
      error: () => { this.loading.set(false); this.toast.error('Could not load expenses'); }
    });
  }

  openCreate() {
    this.editingId.set(null);
    this.form = {
      description: '',
      category: 'Other',
      amount: 0,
      expenseDate: new Date().toISOString().slice(0, 10),
      notes: '',
    projectId: '',
    projectName: ''
    };
    this.showForm.set(true);
  }

  openEdit(e: Expense) {
    this.editingId.set(e.id);
    this.form = {
      description: e.description,
      category: e.category,
      amount: e.amount,
      expenseDate: e.expenseDate.slice(0, 10),
      notes: e.notes ?? '',
      projectId: e.projectId ?? '',
      projectName: e.projectName ?? ''
    };
    this.showForm.set(true);
  }

  save() {
    if (!this.form.description.trim()) { this.toast.error('Description is required'); return; }
    if (!this.form.amount || this.form.amount <= 0) { this.toast.error('Enter a valid amount'); return; }

    this.saving.set(true);
    const payload = { ...this.form, expenseDate: new Date(this.form.expenseDate).toISOString() };
    const req = this.editingId()
      ? this.http.put(`${this.base}/${this.editingId()}`, payload)
      : this.http.post(this.base, payload);

    req.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.toast.success(this.editingId() ? 'Expense updated' : 'Expense added');
        this.loadExpenses();
        this.loadSummary();
      },
      error: err => { this.saving.set(false); this.toast.error(err?.error || 'Failed to save'); }
    });
  }

  delete(e: Expense) {
    if (!confirm(`Delete "${e.description}"?`)) return;
    this.http.delete(`${this.base}/${e.id}`).subscribe({
      next: () => {
        this.expenses.update(list => list.filter(x => x.id !== e.id));
        this.toast.success('Deleted');
        this.loadSummary();
      },
      error: () => this.toast.error('Could not delete')
    });
  }

  onProjectChange(projectId: string) {
    const p = this.projects().find(x => x.id === projectId);
    this.form.projectName = p?.name ?? '';
  }

  categoryIcon(cat: string): string {
    const icons: Record<string, string> = {
      'Travel': '✈️', 'Food & Dining': '🍽️', 'Equipment': '🔧',
      'Software & Tools': '💻', 'Rent & Utilities': '🏠', 'Marketing': '📣',
      'Clothing & Styling': '👗', 'Communication': '📱', 'Health': '💊',
      'Education': '📚', 'Other': '📦'
    };
    return icons[cat] ?? '📦';
  }
}
