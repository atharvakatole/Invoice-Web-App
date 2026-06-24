import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, FormGroup, Validators } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { InvoiceService } from '../../../core/services/invoice.service';
import { ClientService, ClientSummary } from '../../../core/services/client.service';
import { ProjectService } from '../../../core/services/project.service';
import { ToastService } from '../../../core/services/toast.service';
import { debounceTime, distinctUntilChanged, switchMap, of, catchError } from 'rxjs';

@Component({
  selector: 'app-invoice-create',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, CurrencyPipe],
  templateUrl: './invoice-create.component.html',
  styleUrl: './invoice-create.component.scss'
})
export class InvoiceCreateComponent implements OnInit {
  private fb = inject(FormBuilder);
  submitting = signal(false);
  expenseSuggestions = signal<string[]>([]);
  activeSuggestionRow = signal<number | null>(null);
  projectSuggestions = signal<string[]>([]);
  activeProjectRow = signal<number | null>(null);

  // ---- Frequent clients ----
  clients = signal<ClientSummary[]>([]);
  clientSuggestions = signal<ClientSummary[]>([]);
  showClientSuggestions = signal(false);
  selectedClientId = signal<string | null>(null);
  prefilledProjectId = signal<string | null>(null);

  form = this.fb.group({
    clientName: ['', Validators.required],
    clientEmail: ['', [Validators.required, Validators.email]],
    clientPhone: [''],
    clientAddress: [''],
    dueDate: ['', Validators.required],
    gstIncluded: [false],
    gstPercentage: [18],
    notes: [''],
    items: this.fb.array([this.createItem()])
  });

  get items(): FormArray<FormGroup> {
    return this.form.get('items') as FormArray<FormGroup>;
  }

  // recompute on every change-detection via getters
  get subtotal(): number {
    return this.items.controls.reduce((sum, ctrl) => {
      const amount = Number(ctrl.get('amount')?.value) || 0;
      const qty = Number(ctrl.get('quantity')?.value) || 0;
      return sum + amount * qty;
    }, 0);
  }

  get gstAmount(): number {
    if (!this.form.get('gstIncluded')?.value) return 0;
    const pct = Number(this.form.get('gstPercentage')?.value) || 0;
    return (this.subtotal * pct) / 100;
  }

  get total(): number {
    return this.subtotal + this.gstAmount;
  }

  constructor(
    private invoiceService: InvoiceService,
    private clientService: ClientService,
    private projectService: ProjectService,
    private toast: ToastService,
    private router: Router,
    private route: ActivatedRoute
  ) {}

  ngOnInit() {
    this.clientService.getClients().subscribe({
      next: (clients) => {
        this.clients.set(clients);
        this.checkProjectParam();
      },
      error: () => this.checkProjectParam()
    });
  }

  private checkProjectParam() {
    const projectId = this.route.snapshot.queryParamMap.get('projectId');
    if (!projectId) return;
    this.prefilledProjectId.set(projectId);
    this.projectService.getProject(projectId).subscribe({
      next: (data) => {
        const project = data.project;
        const matchedClient = this.clients().find((c: any) => c.id === project.clientId);
        if (matchedClient) {
          this.selectClient(matchedClient);
        }
        const items = this.form.get('items') as any;
        if (items && items.length > 0) {
          items.at(0).get('projectName')?.setValue(project.name);
        }
      },
      error: () => {}
    });
  }

  // ===================== Frequent clients =====================

  onClientNameInput(value: string) {
    this.selectedClientId.set(null);
    if (!value) {
      this.clientSuggestions.set([]);
      this.showClientSuggestions.set(false);
      return;
    }
    const term = value.toLowerCase();
    this.clientSuggestions.set(
      this.clients().filter(c => c.clientName.toLowerCase().includes(term)).slice(0, 6)
    );
    this.showClientSuggestions.set(true);
  }

  showAllClients() {
    if (!this.clients().length) return;
    this.clientSuggestions.set(this.clients().slice(0, 8));
    this.showClientSuggestions.set(true);
  }

  hideClientSuggestions() {
    setTimeout(() => this.showClientSuggestions.set(false), 150);
  }

  selectClient(client: ClientSummary) {
    this.form.patchValue({
      clientName: client.clientName,
      clientEmail: client.clientEmail || '',
      clientPhone: client.clientPhone || '',
      clientAddress: client.clientAddress || ''
    });
    this.selectedClientId.set(client.id);
    this.showClientSuggestions.set(false);
    this.toast.info(`Filled details for ${client.clientName}. You can copy their last invoice's items below.`);
  }

  copyLastItems() {
    const clientId = this.selectedClientId();
    if (!clientId) return;

    this.clientService.getLastItems(clientId).subscribe({
      next: (items) => {
        if (!items.length) {
          this.toast.info('No previous items found for this client');
          return;
        }
        // Replace items with the ones from the last invoice
        while (this.items.length) this.items.removeAt(0);
        for (const it of items) {
          const group = this.createItem();
          group.patchValue({
            expenseName: it.expenseName,
            projectName: it.projectName || '',
            amount: it.amount,
            quantity: it.quantity
          });
          this.items.push(group);
        }
        this.toast.success(`Copied ${items.length} item(s) from their last invoice`);
      },
      error: () => this.toast.error('Could not load previous items')
    });
  }

  createItem(): FormGroup {
    return this.fb.group({
      expenseName: ['', Validators.required],
      projectName: [''],
      itemDate: [this.todayIso(), Validators.required],
      amount: [0, [Validators.required, Validators.min(0.01)]],
      quantity: [1, [Validators.required, Validators.min(1)]]
    });
  }

  private todayIso(): string {
    return new Date().toISOString().slice(0, 10);
  }

  addItem() {
    this.items.push(this.createItem());
  }

  removeItem(i: number) {
    if (this.items.length > 1) this.items.removeAt(i);
  }

  onExpenseInput(i: number, value: string) {
    this.activeSuggestionRow.set(i);
    if (!value || value.length < 1) {
      this.expenseSuggestions.set([]);
      return;
    }
    this.invoiceService.getExpenseSuggestions(value).pipe(
      catchError(() => of([] as string[]))
    ).subscribe(suggestions => this.expenseSuggestions.set(suggestions));
  }

  selectSuggestion(i: number, value: string) {
    this.items.at(i).get('expenseName')?.setValue(value);
    this.expenseSuggestions.set([]);
    this.activeSuggestionRow.set(null);
  }

  hideSuggestions() {
    setTimeout(() => this.activeSuggestionRow.set(null), 150);
  }

  // ===================== Recent project suggestions =====================
  // Backend returns project names used in the last 4 months, most recent first.

  onProjectInput(i: number, value: string) {
    this.activeProjectRow.set(i);
    this.invoiceService.getProjectSuggestions(value || undefined).pipe(
      catchError(() => of([] as { name: string; lastUsed: string }[]))
    ).subscribe(suggestions => this.projectSuggestions.set(suggestions.map(s => s.name)));
  }

  selectProjectSuggestion(i: number, value: string) {
    this.items.at(i).get('projectName')?.setValue(value);
    this.projectSuggestions.set([]);
    this.activeProjectRow.set(null);
  }

  hideProjectSuggestions() {
    setTimeout(() => this.activeProjectRow.set(null), 150);
  }

  submit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.items.controls.forEach(c => c.markAllAsTouched());
      this.toast.error('Please fill in all required fields');
      return;
    }

    const raw = this.form.getRawValue();
    const payload = {
      projectId: this.prefilledProjectId() || undefined,
      clientName: raw.clientName!,
      clientEmail: raw.clientEmail!,
      clientPhone: raw.clientPhone || '',
      clientAddress: raw.clientAddress || '',
      dueDate: new Date(raw.dueDate!).toISOString(),
      gstIncluded: !!raw.gstIncluded,
      gstPercentage: Number(raw.gstPercentage) || 0,
      notes: raw.notes || '',
      items: (raw.items || []).map((it: any) => ({
        expenseName: it.expenseName,
        projectName: it.projectName || '',
        itemDate: new Date(it.itemDate).toISOString(),
        amount: Number(it.amount),
        quantity: Number(it.quantity)
      }))
    };

    this.submitting.set(true);
    this.invoiceService.createInvoice(payload).subscribe({
      next: (res) => {
        this.submitting.set(false);
        this.toast.success(res?.Message || 'Invoice created successfully');
        this.router.navigate(['/app/invoices']);
      },
      error: (err) => {
        this.submitting.set(false);
        this.toast.error(err?.error || 'Failed to create invoice');
      }
    });
  }
}
