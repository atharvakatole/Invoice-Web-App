import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="fade-in change-pw-wrap">
      <div class="page-header">
        <div>
          <h1>Change Password</h1>
          <p class="text-dim">Keep your account secure with a strong password.</p>
        </div>
      </div>
      <div class="card" style="max-width:480px;">
        <form [formGroup]="form" (ngSubmit)="submit()">
          <div class="field">
            <label>Current Password</label>
            <div class="pw-wrap">
              <input [type]="show['current'] ? 'text' : 'password'" formControlName="current" placeholder="Your current password" autocomplete="current-password" />
              <button type="button" class="toggle-pass" (click)="toggle('current')">{{ show['current'] ? 'Hide' : 'Show' }}</button>
            </div>
            <span class="error" *ngIf="form.get('current')?.touched && form.get('current')?.invalid">Required</span>
          </div>
          <div class="field">
            <label>New Password</label>
            <div class="pw-wrap">
              <input [type]="show['newPw'] ? 'text' : 'password'" formControlName="newPw" placeholder="Minimum 8 characters" autocomplete="new-password" />
              <button type="button" class="toggle-pass" (click)="toggle('newPw')">{{ show['newPw'] ? 'Hide' : 'Show' }}</button>
            </div>
            <span class="error" *ngIf="form.get('newPw')?.touched && form.get('newPw')?.invalid">Minimum 8 characters</span>
          </div>
          <div class="field">
            <label>Confirm New Password</label>
            <div class="pw-wrap">
              <input [type]="show['confirm'] ? 'text' : 'password'" formControlName="confirm" placeholder="Re-enter new password" autocomplete="new-password" />
              <button type="button" class="toggle-pass" (click)="toggle('confirm')">{{ show['confirm'] ? 'Hide' : 'Show' }}</button>
            </div>
            <span class="error" *ngIf="form.get('confirm')?.touched && form.get('confirm')?.invalid">Required</span>
          </div>
          <button type="submit" class="btn btn-primary btn-block mt-8" [disabled]="loading()">
            {{ loading() ? 'Saving...' : 'Change Password' }}
          </button>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .change-pw-wrap { }
    .pw-wrap { position: relative; }
    .toggle-pass {
      position: absolute; right: 10px; top: 50%; transform: translateY(-50%);
      background: none; border: none; color: var(--accent-2);
      font-size: 12px; font-weight: 600; cursor: pointer;
    }
  `]
})
export class ChangePasswordComponent {
  loading = signal(false);
  show: Record<string, boolean> = { current: false, newPw: false, confirm: false };
  form: ReturnType<FormBuilder['group']>;

  constructor(
    private fb: FormBuilder,
    private auth: AuthService,
    private router: Router,
    private toast: ToastService
  ) {
    this.form = this.fb.group({
      current: ['', Validators.required],
      newPw:   ['', [Validators.required, Validators.minLength(8)]],
      confirm: ['', Validators.required]
    });
  }

  toggle(key: string) { this.show[key] = !this.show[key]; }

  submit() {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.value;
    if (v.newPw !== v.confirm) { this.toast.error('Passwords do not match'); return; }
    this.loading.set(true);
    this.auth.changePassword(v.current!, v.newPw!).subscribe({
      next: () => {
        this.loading.set(false);
        this.toast.success('Password changed successfully!');
        this.router.navigate(['/app/dashboard']);
      },
      error: err => { this.loading.set(false); this.toast.error(err?.error || 'Could not change password'); }
    });
  }
}
