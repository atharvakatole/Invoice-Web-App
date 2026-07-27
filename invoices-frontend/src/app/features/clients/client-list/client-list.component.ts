import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ClientService, ClientSummary } from '../../../core/services/client.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-client-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, CurrencyPipe, DatePipe],
  templateUrl: './client-list.component.html',
  styleUrl: './client-list.component.scss'
})
export class ClientListComponent implements OnInit {
  loading = signal(true);
  clients = signal<ClientSummary[]>([]);
  search = signal('');

  editTarget = signal<ClientSummary | null>(null);
  isCreating = signal(false);
  editForm = signal({ clientName: '', clientEmail: '', clientPhone: '', clientAddress: '' });
  saving = signal(false);

  openCreate() {
    this.isCreating.set(true);
    this.editTarget.set(null);
    this.editForm.set({ clientName: '', clientEmail: '', clientPhone: '', clientAddress: '' });
  }

  filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    if (!term) return this.clients();
    return this.clients().filter(c =>
      c.clientName.toLowerCase().includes(term) ||
      (c.clientEmail || '').toLowerCase().includes(term) ||
      (c.clientPhone || '').toLowerCase().includes(term)
    );
  });

  totals = computed(() => {
    const list = this.clients();
    return {
      count: list.length,
      revenue: list.reduce((s, c) => s + c.totalRevenue, 0),
      pending: list.reduce((s, c) => s + c.pendingAmount, 0),
    };
  });

  constructor(private clientService: ClientService, private toast: ToastService) {}

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.clientService.getClients().subscribe({
      next: (clients) => { this.clients.set(clients); this.loading.set(false); },
      error: () => { this.loading.set(false); this.toast.error('Could not load clients'); }
    });
  }

  initials(name: string): string {
    return name.split(' ').map(p => p[0]).join('').slice(0, 2).toUpperCase() || '?';
  }

  openEdit(c: ClientSummary) {
    this.isCreating.set(false);
    this.editTarget.set(c);
    this.editForm.set({
      clientName: c.clientName,
      clientEmail: c.clientEmail || '',
      clientPhone: c.clientPhone || '',
      clientAddress: c.clientAddress || ''
    });
  }

  closeEdit() {
    this.editTarget.set(null);
    this.isCreating.set(false);
  }

  setField(field: 'clientName' | 'clientEmail' | 'clientPhone' | 'clientAddress', value: string) {
    this.editForm.update(f => ({ ...f, [field]: value }));
  }

  saveEdit() {
    const form = this.editForm();
    if (!form.clientName.trim()) {
      this.toast.error('Client name is required');
      return;
    }

    this.saving.set(true);

    if (this.isCreating()) {
      this.clientService.createClient(form).subscribe({
        next: (created) => {
          this.saving.set(false);
          this.clients.update(list => [created, ...list]);
          this.toast.success('Client created');
          this.closeEdit();
        },
        error: (err) => {
          this.saving.set(false);
          const _em = err?.error || '';
        if (_em.includes('limit') || _em.includes('Premium')) { this.toast.upgrade(_em); } else { this.toast.error(_em || 'Could not create client'); }
        }
      });
    } else {
      const target = this.editTarget();
      if (!target) return;

      this.clientService.updateClient(target.id, form).subscribe({
        next: (updated) => {
          this.saving.set(false);
          this.clients.update(list => list.map(c => c.id === updated.id ? updated : c));
          this.toast.success('Client updated');
          this.closeEdit();
        },
        error: (err) => {
          this.saving.set(false);
          this.toast.error(err?.error || 'Could not update client');
        }
      });
    }
  }

  deleteClient(c: ClientSummary) {
    if (c.invoiceCount > 0) {
      this.toast.error('This client has invoices and cannot be deleted. View their ledger instead.');
      return;
    }

    this.clientService.deleteClient(c.id).subscribe({
      next: () => {
        this.clients.update(list => list.filter(x => x.id !== c.id));
        this.toast.success('Client removed');
      },
      error: (err) => this.toast.error(err?.error || 'Could not remove client')
    });
  }
}
