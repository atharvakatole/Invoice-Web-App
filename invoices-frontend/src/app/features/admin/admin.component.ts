import { Component, OnInit, signal } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { AdminService } from '../../core/services/admin.service';
import { ToastService } from '../../core/services/toast.service';
import { AdminDashboard } from '../../core/models/models';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [CommonModule, CurrencyPipe],
  templateUrl: './admin.component.html',
  styleUrl: './admin.component.scss'
})
export class AdminComponent implements OnInit {
  loading = signal(true);
  data = signal<AdminDashboard | null>(null);

  constructor(private adminService: AdminService, private toast: ToastService) {}

  ngOnInit() {
    this.adminService.getDashboard().subscribe({
      next: (d) => { this.data.set(d); this.loading.set(false); },
      error: () => { this.loading.set(false); this.toast.error('Could not load admin dashboard'); }
    });
  }
}
