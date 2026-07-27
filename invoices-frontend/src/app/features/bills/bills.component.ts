import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { BillService, Bill, BillItem, BillItemPayload, PAYMENT_METHODS } from '../../core/services/bill.service';
import { ProjectDropdownService } from '../../core/services/project-dropdown.service';
import { ClientService, ClientSummary } from '../../core/services/client.service';
import { ToastService } from '../../core/services/toast.service';

interface ItemFormRow {
  itemName: string;
  quantity: number;
  pricePerItem: number | null;
  isRefundable: boolean;
  returnByDate: string;
  notes: string;
  pendingFile?: File | null;
  previewUrl?: string | null;
}

@Component({
  selector: 'app-bills',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, CurrencyPipe, DatePipe],
  templateUrl: './bills.component.html',
  styleUrl: './bills.component.scss'
})
export class BillsComponent implements OnInit {
  loading = signal(true);
  bills = signal<Bill[]>([]);
  clients = signal<ClientSummary[]>([]);
  paymentMethods = PAYMENT_METHODS;

  filterProject = signal('');
  filterBrand = signal('');

  // ── Bill form ──
  showBillForm = signal(false);
  editingBillId = signal<string | null>(null);
  saving = signal(false);
  billForm = signal<{ projectName: string; customProject: string; brandName: string; billDate: string; paidWith: string; notes: string }>(this.emptyBillForm());
  itemRows = signal<ItemFormRow[]>([this.emptyItemRow()]);

  // ── Return modal ──
  returnModal = signal<{ bill: Bill; item: BillItem } | null>(null);
  returnQty = signal(1);
  returningItem = signal(false);

  // ── Sell modal ──
  sellModal = signal<{ bill: Bill; item: BillItem } | null>(null);
  sellQty = signal(1);
  sellClientName = signal('');
  sellClientId = signal('');
  sellClientSearch = signal('');
  sellingItem = signal(false);
  clientSuggestions = signal<ClientSummary[]>([]);
  showClientDrop = signal(false);

  filteredSellClients = computed(() => {
    if (this.sellClientId()) return [];
    const term = this.sellClientSearch().trim().toLowerCase();
    if (!term) return this.clients().slice(0, 8);
    return this.clients()
      .filter(c => c.clientName.toLowerCase().includes(term) ||
        (c.clientEmail ?? '').toLowerCase().includes(term))
      .slice(0, 8);
  });

  filtered = computed(() => {
    let list = this.bills();
    if (this.filterProject()) list = list.filter(b => b.projectName === this.filterProject());
    if (this.filterBrand().trim()) {
      const t = this.filterBrand().trim().toLowerCase();
      list = list.filter(b => b.brandName.toLowerCase().includes(t));
    }
    return list;
  });

  totals = computed(() => ({
    spent: this.filtered().reduce((s, b) => s + b.totalCost, 0),
    refunded: this.filtered().reduce((s, b) => s + b.totalRefunded, 0),
    bought: this.filtered().reduce((s, b) => s + b.totalBought, 0),
    pending: this.filtered().reduce((s, b) => s + b.totalPending, 0),
  }));

  constructor(
    private billService: BillService,
    private clientService: ClientService,
    public projectDropdown: ProjectDropdownService,
    private toast: ToastService
  ) {}

  ngOnInit() {
    this.load();
    this.projectDropdown.load();
    this.clientService.getClients().subscribe({ next: c => this.clients.set(c), error: () => {} });
  }

  load() {
    this.loading.set(true);
    this.billService.getBills().subscribe({
      next: b => { this.bills.set(b); this.loading.set(false); },
      error: () => { this.loading.set(false); this.toast.error('Could not load bills'); }
    });
  }

  // ── Bill form ──────────────────────────────────────────────────

  private emptyBillForm() {
    return { projectName: '', customProject: '', brandName: '', billDate: new Date().toISOString().slice(0, 10), paidWith: 'UPI', notes: '' };
  }

  private emptyItemRow(): ItemFormRow {
    return { itemName: '', quantity: 1, pricePerItem: null, isRefundable: false, returnByDate: '', notes: '', pendingFile: null, previewUrl: null };
  }

  setBillField<K extends keyof ReturnType<BillsComponent['emptyBillForm']>>(key: K, value: string) {
    this.billForm.update(f => ({ ...f, [key]: value }));
  }

  openCreate() {
    this.editingBillId.set(null);
    this.billForm.set(this.emptyBillForm());
    this.itemRows.set([this.emptyItemRow()]);
    this.showBillForm.set(true);
  }

  openEdit(bill: Bill) {
    this.editingBillId.set(bill.id);
    const known = this.projectDropdown.projects().includes(bill.projectName);
    this.billForm.set({
      projectName: known ? bill.projectName : '__custom__',
      customProject: known ? '' : bill.projectName,
      brandName: bill.brandName,
      billDate: bill.billDate.slice(0, 10),
      paidWith: bill.paidWith,
      notes: bill.notes || ''
    });
    this.itemRows.set(bill.items.map(i => ({
      itemName: i.itemName,
      quantity: i.quantity,
      pricePerItem: i.pricePerItem,
      isRefundable: i.isRefundable,
      returnByDate: i.returnByDate ? i.returnByDate.slice(0, 10) : '',
      notes: i.notes || '',
      pendingFile: null,
      previewUrl: null
    })));
    this.showBillForm.set(true);
  }

  addItemRow() {
    this.itemRows.update(rows => [...rows, this.emptyItemRow()]);
  }

  removeItemRow(i: number) {
    this.itemRows.update(rows => rows.filter((_, idx) => idx !== i));
  }

  setItemField(i: number, key: keyof ItemFormRow, value: any) {
    this.itemRows.update(rows => rows.map((r, idx) => idx === i ? { ...r, [key]: value } : r));
  }

  onItemImageSelected(i: number, event: Event) {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
      this.toast.error('Image must be JPEG, PNG, or WebP');
      return;
    }
    const url = URL.createObjectURL(file);
    this.setItemField(i, 'pendingFile', file);
    this.setItemField(i, 'previewUrl', url);
  }

  resolvedProject(): string {
    const f = this.billForm();
    return f.projectName === '__custom__' ? f.customProject.trim() : f.projectName;
  }

  saveBill() {
    const f = this.billForm();
    const projectName = this.resolvedProject();
    if (!projectName) { this.toast.error('Select or enter a project'); return; }
    if (!f.brandName.trim()) { this.toast.error('Enter a brand name'); return; }
    const rows = this.itemRows();
    if (!rows.length) { this.toast.error('Add at least one item'); return; }
    for (const r of rows) {
      if (!r.itemName.trim()) { this.toast.error('Each item needs a name'); return; }
      if (!r.pricePerItem || r.pricePerItem < 0) { this.toast.error('Enter a valid price for each item'); return; }
    }

    const payload = {
      projectName,
      brandName: f.brandName.trim(),
      billDate: new Date(f.billDate).toISOString(),
      paidWith: f.paidWith,
      notes: f.notes.trim(),
      items: rows.map(r => ({
        itemName: r.itemName.trim(),
        quantity: r.quantity,
        pricePerItem: Number(r.pricePerItem),
        isRefundable: r.isRefundable,
        returnByDate: r.isRefundable && r.returnByDate ? new Date(r.returnByDate).toISOString() : null,
        notes: r.notes.trim()
      }))
    };

    this.saving.set(true);
    const id = this.editingBillId();
    const obs = id ? this.billService.updateBill(id, payload) : this.billService.createBill(payload);

    obs.subscribe({
      next: (bill) => {
        this.saving.set(false);
        this.showBillForm.set(false);
        this.toast.success(id ? 'Bill updated' : 'Bill added');
        this.load();
        // Refresh projects dropdown so new project names appear immediately
        this.projectDropdown.refresh();
        // Upload pending images
        rows.forEach((r, idx) => {
          if (r.pendingFile && bill.items[idx]) {
            this.billService.uploadItemImage(bill.id, bill.items[idx].id, r.pendingFile).subscribe();
          }
        });
      },
      error: err => { this.saving.set(false); this.toast.error(err?.error || 'Could not save bill'); }
    });
  }

  deleteBill(b: Bill) {
    this.billService.deleteBill(b.id).subscribe({
      next: () => { this.bills.update(list => list.filter(x => x.id !== b.id)); this.toast.success('Bill removed'); },
      error: () => this.toast.error('Could not remove bill')
    });
  }

  // ── Return modal ───────────────────────────────────────────────

  openReturn(bill: Bill, item: BillItem) {
    this.returnModal.set({ bill, item });
    this.returnQty.set(1);
  }

  closeReturn() { this.returnModal.set(null); }

  maxReturnable(): number {
    const m = this.returnModal();
    if (!m) return 0;
    return m.item.quantity - m.item.quantityReturned - m.item.quantityBoughtByClient;
  }

  submitReturn() {
    const m = this.returnModal();
    if (!m) return;
    const qty = this.returnQty();
    if (qty < 1 || qty > this.maxReturnable()) {
      this.toast.error(`Enter a value between 1 and ${this.maxReturnable()}`);
      return;
    }
    this.returningItem.set(true);
    this.billService.returnItems(m.bill.id, m.item.id, qty).subscribe({
      next: updated => {
        this.returningItem.set(false);
        this.returnModal.set(null);
        this.updateItem(m.bill.id, updated);
        this.recalcBillTotals(m.bill.id);
        this.toast.success(`Returned ${qty} item(s). Refund: ₹${(qty * m.item.pricePerItem).toFixed(0)}`);
      },
      error: err => { this.returningItem.set(false); this.toast.error(err?.error || 'Could not process return'); }
    });
  }

  // ── Sell modal ─────────────────────────────────────────────────

  openSell(bill: Bill, item: BillItem) {
    this.sellModal.set({ bill, item });
    this.sellQty.set(1);
    this.sellClientName.set('');
    this.sellClientId.set('');
    this.sellClientSearch.set('');
  }

  closeSell() { this.sellModal.set(null); }

  maxSellable(): number {
    const m = this.sellModal();
    if (!m) return 0;
    return m.item.quantity - m.item.quantityReturned - m.item.quantityBoughtByClient;
  }

  onSellClientSearch(value: string) {
    this.sellClientSearch.set(value);
    this.sellClientId.set(''); // clear selection when typing
    this.sellClientName.set(value);
  }

  clearSellClient() {
    this.sellClientId.set('');
    this.sellClientName.set('');
    this.sellClientSearch.set('');
  }

  onSellClientInput(value: string) {
    this.sellClientName.set(value);
    this.sellClientId.set('');
    const t = value.toLowerCase();
    this.clientSuggestions.set(
      t ? this.clients().filter(c => c.clientName.toLowerCase().includes(t)).slice(0, 5) : []
    );
    this.showClientDrop.set(true);
  }

  selectSellClient(c: ClientSummary) {
    this.sellClientName.set(c.clientName);
    this.sellClientId.set(c.id);
    this.showClientDrop.set(false);
    this.clientSuggestions.set([]);
  }

  submitSell() {
    const m = this.sellModal();
    if (!m) return;
    const qty = this.sellQty();
    if (qty < 1 || qty > this.maxSellable()) {
      this.toast.error(`Enter a value between 1 and ${this.maxSellable()}`);
      return;
    }
    if (!this.sellClientName().trim()) {
      this.toast.error('Enter the client name');
      return;
    }
    this.sellingItem.set(true);
    this.billService.sellToClient(m.bill.id, m.item.id, {
      quantityToSell: qty,
      clientName: this.sellClientName().trim(),
      clientId: this.sellClientId() || undefined
    }).subscribe({
      next: updated => {
        this.sellingItem.set(false);
        this.sellModal.set(null);
        this.updateItem(m.bill.id, updated);
        this.recalcBillTotals(m.bill.id);
        this.toast.success(`${qty} item(s) sold to ${this.sellClientName()}. Draft invoice created/updated.`);
      },
      error: err => { this.sellingItem.set(false); this.toast.error(err?.error || 'Could not process sale'); }
    });
  }

  // ── Image ──────────────────────────────────────────────────────

  onItemImageUpload(bill: Bill, item: BillItem, event: Event) {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
      this.toast.error('Image must be JPEG, PNG, or WebP');
      return;
    }
    this.billService.uploadItemImage(bill.id, item.id, file).subscribe({
      next: () => { this.updateItem(bill.id, { ...item, hasImage: true }); this.toast.success('Photo uploaded'); },
      error: () => this.toast.error('Could not upload image')
    });
  }

  itemImageUrl(billId: string, itemId: string): string {
    return this.billService.itemImageUrl(billId, itemId);
  }

  isOverdue(item: BillItem): boolean {
    if (!item.isRefundable || !item.returnByDate || item.quantityPending <= 0) return false;
    return new Date(item.returnByDate) < new Date(new Date().toDateString());
  }

  private recalcBillTotals(billId: string) {
    this.bills.update(list => list.map(b => {
      if (b.id !== billId) return b;
      const items = b.items;
      return {
        ...b,
        totalCost: items.reduce((s, i) => s + i.totalCost, 0),
        totalRefunded: items.reduce((s, i) => s + i.amountRefunded, 0),
        totalBought: items.reduce((s, i) => s + i.amountBoughtByClient, 0),
        totalPending: items.reduce((s, i) => s + i.quantityPending * i.pricePerItem, 0),
      };
    }));
  }

  private updateItem(billId: string, updated: BillItem) {
    this.bills.update(list => list.map(b =>
      b.id === billId
        ? { ...b, items: b.items.map(i => i.id === updated.id ? updated : i) }
        : b
    ));
  }
}
