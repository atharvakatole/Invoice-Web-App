import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-assistant-change-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="fade-in">
      <div class="page-header"><div><h1>Change Password</h1><p class="text-dim">Update your login password.</p></div></div>
      <div class="card" style="max-width:480px;">
        <form [formGroup]="form" (ngSubmit)="submit()">
          <div class="field">
            <label>Current Password</label>
            <input type="password" formControlName="current" placeholder="Your current password" />
            <span class="error" *ngIf="form.get('current')?.touched && form.get('current')?.invalid">Required</span>
          </div>
          <div class="field">
            <label>New Password</label>
            <input type="password" formControlName="newPw" placeholder="Minimum 8 characters" />
            <span class="error" *ngIf="form.get('newPw')?.touched && form.get('newPw')?.invalid">Min 8 characters</span>
          </div>
          <div class="field">
            <label>Confirm Password</label>
            <input type="password" formControlName="confirm" placeholder="Re-enter new password" />
          </div>
          <button type="submit" class="btn btn-primary btn-block mt-8" [disabled]="loading()">
            {{ loading() ? 'Saving...' : 'Change Password' }}
          </button>
        </form>
      </div>
    </div>
  `
})
export class AssistantChangePasswordComponent {
  loading = signal(false);
  form: ReturnType<FormBuilder['group']>;

  constructor(private fb: FormBuilder, private auth: AuthService, private router: Router, private toast: ToastService) {
    this.form = this.fb.group({
      current: ['', Validators.required],
      newPw:   ['', [Validators.required, Validators.minLength(8)]],
      confirm: ['', Validators.required]
    });
  }

  submit() {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.value;
    if (v.newPw !== v.confirm) { this.toast.error('Passwords do not match'); return; }
    this.loading.set(true);
    this.auth.changePassword(v.current!, v.newPw!).subscribe({
      next: () => { this.loading.set(false); this.toast.success('Password changed!'); this.router.navigate(['/assistant/dashboard']); },
      error: err => { this.loading.set(false); this.toast.error(err?.error || 'Failed'); }
    });
  }
}
