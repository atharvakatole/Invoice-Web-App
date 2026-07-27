import { Component, OnDestroy, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { SocialLoginComponent } from '../../../shared/components/social-login/social-login.component';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, SocialLoginComponent],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss'
})
export class RegisterComponent implements OnDestroy {
  private fb = inject(FormBuilder);

  /** 'details' = filling the form, 'otp' = entered OTP screen */
  step = signal<'details' | 'otp'>('details');

  loading = signal(false);
  otpSending = signal(false);
  showPassword = signal(false);

  /** Countdown for resend button (90 → 0) */
  resendCountdown = signal(0);
  private countdownTimer?: ReturnType<typeof setInterval>;

  form = this.fb.group({
    fullName:      ['', Validators.required],
    username:      ['', Validators.required],
    email:         ['', [Validators.required, Validators.email]],
    phoneNumber:   [''],
    password:      ['', [Validators.required, Validators.minLength(8)]],
    businessName:  ['', Validators.required],
    businessEmail: ['', [Validators.required, Validators.email]],
    otpCode:       ['', [Validators.required, Validators.minLength(6), Validators.maxLength(6)]]
  });

  constructor(
    private auth: AuthService,
    private router: Router,
    private toast: ToastService
  ) {}

  ngOnDestroy() {
    clearInterval(this.countdownTimer);
  }

  // ── Step 1: Send OTP and advance to OTP screen ──────────

  sendOtpAndNext() {
    const detailsControls = ['fullName','username','email','phoneNumber','password','businessName','businessEmail'];
    detailsControls.forEach(k => this.form.get(k)?.markAsTouched());
    const partial = this.fb.group({
      fullName:      this.form.get('fullName')!,
      username:      this.form.get('username')!,
      email:         this.form.get('email')!,
      password:      this.form.get('password')!,
      businessName:  this.form.get('businessName')!,
      businessEmail: this.form.get('businessEmail')!,
    });
    if (partial.invalid) return;

    this.otpSending.set(true);
    this.auth.sendOtp(this.form.value.email!, 'registration').subscribe({
      next: () => {
        this.otpSending.set(false);
        this.step.set('otp');
        this.startCountdown();
        this.toast.success('OTP sent! Check your email.');
      },
      error: (err) => {
        this.otpSending.set(false);
        this.toast.error(err?.error || 'Could not send OTP');
      }
    });
  }

  // ── Resend OTP ──────────────────────────────────────────

  resendOtp() {
    if (this.resendCountdown() > 0) return;
    this.otpSending.set(true);
    this.auth.sendOtp(this.form.value.email!, 'registration').subscribe({
      next: () => {
        this.otpSending.set(false);
        this.startCountdown();
        this.toast.success('New OTP sent!');
      },
      error: (err) => {
        this.otpSending.set(false);
        this.toast.error(err?.error || 'Could not resend OTP');
      }
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

  // ── Step 2: Submit with OTP ──────────────────────────────

  submit() {
    this.form.get('otpCode')?.markAsTouched();
    if (!this.form.get('otpCode')?.valid) {
      this.toast.error('Enter the 6-digit OTP');
      return;
    }

    this.loading.set(true);
    const v = this.form.getRawValue();
    this.auth.register({
      fullName: v.fullName,
      username: v.username,
      email: v.email,
      phoneNumber: v.phoneNumber || '',
      password: v.password,
      businessName: v.businessName,
      businessEmail: v.businessEmail,
      otpCode: v.otpCode
    } as any).subscribe({
      next: () => {
        this.loading.set(false);
        this.toast.success('Account created! Please sign in.');
        this.router.navigate(['/login']);
      },
      error: (err) => {
        this.loading.set(false);
        this.toast.error(err?.error || 'Registration failed');
      }
    });
  }

  backToDetails() {
    this.step.set('details');
    this.form.get('otpCode')?.reset();
  }
}
