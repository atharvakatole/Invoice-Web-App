import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ProjectService, Project } from '../../core/services/project.service';
import { ClientService, ClientSummary } from '../../core/services/client.service';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-projects',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, CurrencyPipe, DatePipe],
  templateUrl: './projects.component.html',
  styleUrl: './projects.component.scss'
})
export class ProjectsComponent implements OnInit {
  loading = signal(true);
  projects = signal<Project[]>([]);
  clients = signal<ClientSummary[]>([]);
  filterStatus = signal('');
  search = signal('');
  showForm = signal(false);
  editingId = signal<string | null>(null);
  saving = signal(false);

  form = signal<{
    clientId: string; name: string; description: string;
    startDate: string; endDate: string; budget: string; notes: string; status: string;
  }>({ clientId: '', name: '', description: '', startDate: '', endDate: '', budget: '', notes: '', status: 'Active' });

  filtered = computed(() => {
    let list = this.projects();
    if (this.filterStatus()) list = list.filter(p => p.status === this.filterStatus());
    const s = this.search().trim().toLowerCase();
    if (s) list = list.filter(p => p.name.toLowerCase().includes(s) || p.clientName.toLowerCase().includes(s));
    return list;
  });

  showClientForm = signal(false);
  statusOptions = ['Active', 'Completed', 'Archived'];
  savingClient = signal(false);
  newClientForm = signal({ clientName: '', clientEmail: '', clientPhone: '', clientAddress: '' });

  setClientField<K extends keyof ReturnType<ProjectsComponent['emptyClientForm']>>(key: K, value: string) {
    this.newClientForm.update(f => ({ ...f, [key]: value }));
  }

  private emptyClientForm() {
    return { clientName: '', clientEmail: '', clientPhone: '', clientAddress: '' };
  }

  createClientInline() {
    const f = this.newClientForm();
    if (!f.clientName.trim()) { this.toast.error('Client name is required'); return; }

    this.savingClient.set(true);
    this.clientService.createClient(f).subscribe({
      next: (created) => {
        this.savingClient.set(false);
        this.clients.update(list => [created, ...list]);
        this.setField('clientId', created.id);
        this.showClientForm.set(false);
        this.newClientForm.set(this.emptyClientForm());
        this.toast.success(`Client "${created.clientName}" created and selected`);
      },
      error: (err) => {
        this.savingClient.set(false);
        const _em = err?.error || '';
        if (_em.includes('limit') || _em.includes('Premium')) { this.toast.upgrade(_em); } else { this.toast.error(_em || 'Could not create client'); }
      }
    });
  }

  constructor(
    private projectService: ProjectService,
    private clientService: ClientService,
    private toast: ToastService
  ) {}

  ngOnInit() {
    this.load();
    this.clientService.getClients().subscribe({ next: c => this.clients.set(c), error: () => {} });
  }

  load() {
    this.loading.set(true);
    this.projectService.getProjects().subscribe({
      next: p => { this.projects.set(p); this.loading.set(false); },
      error: () => { this.loading.set(false); this.toast.error('Could not load projects'); }
    });
  }

  setField<K extends keyof ReturnType<ProjectsComponent['emptyForm']>>(key: K, value: string) {
    this.form.update(f => ({ ...f, [key]: value }));
  }

  private emptyForm() {
    return { clientId: '', name: '', description: '', startDate: '', endDate: '', budget: '', notes: '', status: 'Active' };
  }

  openCreate() {
    this.editingId.set(null);
    this.form.set(this.emptyForm());
    this.showForm.set(true);
  }

  openEdit(p: Project) {
    this.editingId.set(p.id);
    this.form.set({
      clientId: p.clientId,
      name: p.name,
      description: p.description || '',
      startDate: p.startDate ? p.startDate.slice(0, 10) : '',
      endDate: p.endDate ? p.endDate.slice(0, 10) : '',
      budget: p.budget ? String(p.budget) : '',
      notes: p.notes || '',
      status: p.status
    });
    this.showForm.set(true);
  }

  save() {
    const f = this.form();
    if (!f.clientId || f.clientId === '__new__') { this.toast.error('Select a client (or finish creating the new one above)'); return; }
    if (!f.name.trim()) { this.toast.error('Enter a project name'); return; }

    const payload = {
      clientId: f.clientId,
      name: f.name.trim(),
      description: f.description.trim() || undefined,
      startDate: f.startDate ? new Date(f.startDate).toISOString() : undefined,
      endDate: f.endDate ? new Date(f.endDate).toISOString() : undefined,
      budget: f.budget ? Number(f.budget) : undefined,
      notes: f.notes.trim() || undefined,
      status: f.status
    };

    this.saving.set(true);
    const id = this.editingId();
    const obs = id ? this.projectService.updateProject(id, payload) : this.projectService.createProject(payload);

    obs.subscribe({
      next: () => { this.saving.set(false); this.showForm.set(false); this.toast.success(id ? 'Project updated' : 'Project created'); this.load(); },
      error: err => { this.saving.set(false); this.toast.error(err?.error || 'Could not save project'); }
    });
  }

  archive(p: Project) {
    this.projectService.updateProject(p.id, { ...p, clientId: p.clientId, status: 'Archived' }).subscribe({
      next: () => { this.projects.update(list => list.map(x => x.id === p.id ? { ...x, status: 'Archived' as const } : x)); this.toast.success('Project archived'); },
      error: () => this.toast.error('Could not archive project')
    });
  }

  statusBadge(status: string): string {
    return status === 'Active' ? 'badge-paid' : status === 'Completed' ? 'badge-partial' : 'badge-cancelled';
  }
}
