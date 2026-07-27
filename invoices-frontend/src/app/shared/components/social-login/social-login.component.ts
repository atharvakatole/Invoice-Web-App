import { Component, AfterViewInit, ElementRef, EventEmitter, Output, ViewChild, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { SocialAuthService } from '../../../core/services/social-auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-social-login',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './social-login.component.html',
  styleUrl: './social-login.component.scss'
})
export class SocialLoginComponent implements AfterViewInit {
  @ViewChild('googleBtn') googleBtn!: ElementRef<HTMLDivElement>;
  @Output() done = new EventEmitter<void>();

  loadingProvider = signal<string | null>(null);
  googleInitFailed = signal(false);
  googleConfigured = !!environment.googleClientId;
  facebookConfigured = !!environment.facebookAppId;
  appleConfigured = !!environment.appleClientId;

  constructor(
    private socialAuth: SocialAuthService,
    private auth: AuthService,
    private router: Router,
    private toast: ToastService
  ) {}

  ngAfterViewInit() {
    if (this.googleConfigured) {
      this.initGoogle();
    }
  }

  private initGoogle() {
    if (!this.googleBtn) return;
    this.socialAuth.signInWithGoogle(this.googleBtn.nativeElement)
      .then(idToken => this.completeLogin('google', idToken))
      .catch(err => {
        console.error('Google sign-in setup failed:', err);
        this.googleInitFailed.set(true);
      });
  }

  retryGoogle() {
    this.googleInitFailed.set(false);
    setTimeout(() => this.initGoogle(), 0);
  }

  continueWithFacebook() {
    this.loadingProvider.set('facebook');
    this.socialAuth.signInWithFacebook()
      .then(token => this.completeLogin('facebook', token))
      .catch(err => {
        this.loadingProvider.set(null);
        this.toast.error(err?.message || 'Facebook sign-in failed');
      });
  }

  continueWithApple() {
    this.loadingProvider.set('apple');
    this.socialAuth.signInWithApple()
      .then(({ token, fullName }) => this.completeLogin('apple', token, fullName))
      .catch(err => {
        this.loadingProvider.set(null);
        this.toast.error(err?.message || 'Apple sign-in failed');
      });
  }

  private completeLogin(provider: 'google' | 'facebook' | 'apple', token: string, fullName?: string) {
    this.loadingProvider.set(provider);
    this.auth.externalLogin(provider, token, fullName).subscribe({
      next: () => {
        this.loadingProvider.set(null);
        this.toast.success('Welcome!');
        this.done.emit();
        this.router.navigate(['/app/dashboard']);
      },
      error: (err) => {
        this.loadingProvider.set(null);
        this.toast.error(err?.error || `${provider} sign-in failed`);
      }
    });
  }
}
