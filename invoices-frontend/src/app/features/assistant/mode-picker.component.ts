import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { AssistantPortalService } from '../../core/services/assistant-portal.service';
import { UserRole } from '../../core/models/models';

@Component({
  selector: 'app-mode-picker',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './mode-picker.component.html',
  styleUrl: './mode-picker.component.scss'
})
export class ModePickerComponent implements OnInit {
  loading = signal(true);
  pendingCount = signal(0);

  constructor(
    public auth: AuthService,
    private router: Router,
    private assistantService: AssistantPortalService
  ) {}

  ngOnInit() {
    const user = this.auth.user();
    if (!user) { this.router.navigate(['/login']); return; }

    // If only AssistantUser — skip picker, go straight to assistant dashboard
    if (user.role === UserRole.AssistantUser) {
      this.router.navigate(['/assistant/dashboard']);
      return;
    }

    // If only BusinessOwner — skip picker, go straight to manager dashboard
    if (user.role !== UserRole.BusinessOwner || !this.auth.isAssistant()) {
      this.router.navigate(['/app/dashboard']);
      return;
    }

    // Dual-role: load pending assignment count for the assistant badge
    this.assistantService.getAssignments('Pending').subscribe({
      next: (a) => { this.pendingCount.set(a.length); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  goManager() { this.auth.setWorkMode('manager'); this.router.navigate(['/app/dashboard']); }
  goAssistant() { this.auth.setWorkMode('assistant'); this.router.navigate(['/assistant/dashboard']); }
}
