import { Component, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './forgot-password.component.html',
  styleUrl: './forgot-password.component.scss'
})
export class ForgotPasswordComponent implements OnDestroy {
  step = signal<'email' | 'otp'>('email');
  loading = signal(false);
  resendCountdown = signal(0);
  private countdownTimer?: ReturnType<typeof setInterval>;

  emailForm: ReturnType<typeof this.fb.group>;
  resetForm: ReturnType<typeof this.fb.group>;

  constructor(
    private fb: FormBuilder,
    private auth: AuthService,
    private router: Router,
    private toast: ToastService
  ) {
    this.emailForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]]
    });
    this.resetForm = this.fb.group({
      otpCode:     ['', [Validators.required, Validators.minLength(6), Validators.maxLength(6)]],
      newPassword: ['', [Validators.required, Validators.minLength(8)]],
      confirm:     ['', Validators.required]
    });
  }

  ngOnDestroy() { clearInterval(this.countdownTimer); }

  sendOtp() {
    if (this.emailForm.invalid) { this.emailForm.markAllAsTouched(); return; }
    this.loading.set(true);
    this.auth.forgotPassword(this.emailForm.value.email!).subscribe({
      next: () => {
        this.loading.set(false);
        this.step.set('otp');
        this.startCountdown();
        this.toast.success('OTP sent! Check your email.');
      },
      error: err => { this.loading.set(false); this.toast.error(err?.error || 'Failed to send OTP'); }
    });
  }

  resendOtp() {
    if (this.resendCountdown() > 0) return;
    this.loading.set(true);
    this.auth.forgotPassword(this.emailForm.value.email!).subscribe({
      next: () => { this.loading.set(false); this.startCountdown(); this.toast.success('New OTP sent!'); },
      error: err => { this.loading.set(false); this.toast.error(err?.error || 'Failed to resend OTP'); }
    });
  }

  resetPassword() {
    if (this.resetForm.invalid) { this.resetForm.markAllAsTouched(); return; }
    const v = this.resetForm.value;
    if (v.newPassword !== v.confirm) { this.toast.error('Passwords do not match'); return; }
    this.loading.set(true);
    this.auth.resetPassword(this.emailForm.value.email!, v.otpCode!, v.newPassword!).subscribe({
      next: () => {
        this.loading.set(false);
        this.toast.success('Password reset! Please sign in.');
        this.router.navigate(['/login']);
      },
      error: err => { this.loading.set(false); this.toast.error(err?.error || 'Reset failed'); }
    });
  }

  private startCountdown() {
    clearInterval(this.countdownTimer);
    this.resendCountdown.set(90);
    this.countdownTimer = setInterval(() => {
      const v = this.resendCountdown() - 1;
      this.resendCountdown.set(v);
      if (v <= 0) clearInterval(this.countdownTimer);
    }, 1000);
  }
}
