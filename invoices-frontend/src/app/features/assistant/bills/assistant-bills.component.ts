import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule, DatePipe, CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AssistantPortalService, AssistantAssignment } from '../../../core/services/assistant-portal.service';
import { ToastService } from '../../../core/services/toast.service';

interface BillFormItem { itemName: string; quantity: number; pricePerItem: number; isRefundable: boolean; returnByDate: string; }

@Component({
  selector: 'app-assistant-bills',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePipe, CurrencyPipe],
  templateUrl: './assistant-bills.component.html',
  styleUrl: './assistant-bills.component.scss'
})
export class AssistantBillsComponent implements OnInit {
  loading = signal(true);
  bills = signal<any[]>([]);
  assignments = signal<AssistantAssignment[]>([]);
  showForm = signal(false);
  selectedAssignmentId = signal('');
  saving = signal(false);
  returnModal = signal<{ bill: any; item: any } | null>(null);
  returnQty = signal(1);
  returnNotes = signal('');
  submittingReturn = signal(false);

  // Bill form
  form = signal({ projectId: '', projectName: '', brandName: '', billDate: '', paidWith: 'Cash', notes: '' });
  items = signal<BillFormItem[]>([{ itemName: '', quantity: 1, pricePerItem: 0, isRefundable: false, returnByDate: '' }]);

  activeAssignments = computed(() => this.assignments().filter(a => a.status === 'Accepted'));

  constructor(private service: AssistantPortalService, private toast: ToastService) {}

  ngOnInit() {
    this.service.getBills().subscribe({ next: b => this.bills.set(b), error: () => {} });
    this.service.getAssignments('Accepted').subscribe({
      next: a => { this.assignments.set(a); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  setForm(field: string, value: any) { this.form.update(f => ({ ...f, [field]: value })); }

  onProjectChange(assignmentId: string) {
    this.selectedAssignmentId.set(assignmentId);
    const a = this.assignments().find(x => x.id === assignmentId);
    if (a) this.form.update(f => ({ ...f, projectId: a.projectId ?? '', projectName: a.projectName }));
  }

  addItem() { this.items.update(l => [...l, { itemName: '', quantity: 1, pricePerItem: 0, isRefundable: false, returnByDate: '' }]); }
  removeItem(i: number) { this.items.update(l => l.filter((_, idx) => idx !== i)); }
  setItem(i: number, field: string, value: any) {
    this.items.update(l => l.map((item, idx) => idx === i ? { ...item, [field]: value } : item));
  }

  saveBill() {
    const f = this.form();
    if (!f.projectName) { this.toast.error('Select a project'); return; }
    if (!f.brandName) { this.toast.error('Enter brand name'); return; }
    if (!f.billDate) { this.toast.error('Select date'); return; }
    if (!this.items().every(i => i.itemName && i.quantity > 0)) { this.toast.error('Fill all item details'); return; }

    this.saving.set(true);
    this.service.addBill({
      projectId: f.projectId || undefined,
      projectName: f.projectName,
      brandName: f.brandName,
      billDate: f.billDate,
      paidWith: f.paidWith,
      notes: f.notes,
      items: this.items().map(i => ({
        itemName: i.itemName,
        quantity: i.quantity,
        pricePerItem: i.pricePerItem,
        isRefundable: i.isRefundable,
        returnByDate: i.returnByDate || undefined
      }))
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.toast.success('Bill added!');
        this.service.getBills().subscribe({ next: b => this.bills.set(b) });
      },
      error: err => { this.saving.set(false); this.toast.error(err?.error || 'Failed to save'); }
    });
  }

  openReturn(bill: any, item: any) {
    this.returnModal.set({ bill, item });
    this.returnQty.set(1);
    this.returnNotes.set('');
  }

  submitReturn() {
    const m = this.returnModal();
    if (!m) return;
    if (this.returnQty() < 1 || this.returnQty() > m.item.quantityPending) {
      this.toast.error(`Enter quantity between 1 and ${m.item.quantityPending}`);
      return;
    }
    // Find assignmentId for this bill's project
    const assignment = this.assignments().find(a => a.projectName === m.bill.projectName);
    if (!assignment) { this.toast.error('Assignment not found for this project'); return; }

    this.submittingReturn.set(true);
    this.service.submitReturnRequest({
      billItemId: m.item.id,
      assignmentId: assignment.id,
      quantityToReturn: this.returnQty(),
      notes: this.returnNotes()
    }).subscribe({
      next: () => {
        this.submittingReturn.set(false);
        this.returnModal.set(null);
        this.toast.success('Return request sent to manager!');
      },
      error: err => { this.submittingReturn.set(false); this.toast.error(err?.error || 'Failed'); }
    });
  }
}
