import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { SocialLoginComponent } from '../../../shared/components/social-login/social-login.component';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, SocialLoginComponent],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  private fb = inject(FormBuilder);
  loading = signal(false);
  showPassword = signal(false);

  form = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]]
  });

  constructor(
    private auth: AuthService,
    private router: Router,
    private toast: ToastService
  ) {}

  submit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading.set(true);
    this.auth.login(this.form.getRawValue() as any).subscribe({
      next: () => {
        this.loading.set(false);
        this.toast.success('Welcome back!');
        const user = this.auth.user();

        if (user?.role === 4) {
          // Assistant only — go straight to assistant portal
          this.auth.setWorkMode('assistant');
          this.router.navigate(['/assistant/dashboard']);
        } else if (user?.isAssistant) {
          // Dual role — check remembered mode first
          const savedMode = this.auth.getWorkMode();
          if (savedMode === 'assistant') {
            this.router.navigate(['/assistant/dashboard']);
          } else if (savedMode === 'manager') {
            this.router.navigate(['/app/dashboard']);
          } else {
            // No saved mode — show picker
            this.router.navigate(['/mode']);
          }
        } else {
          // Manager only
          this.auth.setWorkMode('manager');
          this.router.navigate(['/app/dashboard']);
        }
      },
      error: (err) => {
        this.loading.set(false);
        this.toast.error(err?.error || 'Invalid email or password');
      }
    });
  }
}
