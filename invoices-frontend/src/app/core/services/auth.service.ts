import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthUser, UserRole, SubscriptionPlan } from '../models/models';

interface JwtPayload {
  sub?: string;
  nameid?: string;
  email?: string;
  name?: string;
  role?: string;
  subscriptionPlan?: string | number;
  unique_name?: string;
  [key: string]: any;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private tokenKey = 'invoicely_token';
  private modeKey = 'invoicely_work_mode'; // 'manager' | 'assistant'
  user = signal<AuthUser | null>(null);
  isAuthenticated = computed(() => !!this.user());

  constructor(private http: HttpClient, private router: Router) {
    const token = localStorage.getItem(this.tokenKey);
    if (token) {
      this.setUserFromToken(token);
    }
  }

  register(payload: any): Observable<any> {
    return this.http.post(`${environment.apiUrl}/auth/register`, payload);
  }

  login(payload: { email: string; password: string }): Observable<{ token: string }> {
    return this.http.post<{ token: string }>(`${environment.apiUrl}/auth/login`, payload).pipe(
      tap(res => {
        localStorage.setItem(this.tokenKey, res.token);
        this.setUserFromToken(res.token);
      })
    );
  }

  /**
   * Sign in (or sign up on first use) with Google, Facebook, or Apple.
   * `token` is the provider-issued token obtained via their SDK
   * (Google ID token, Facebook access token, or Apple identity token).
   */
  externalLogin(provider: 'google' | 'facebook' | 'apple', token: string, fullName?: string): Observable<{ token: string }> {
    return this.http.post<{ token: string }>(`${environment.apiUrl}/auth/external-login`, { provider, token, fullName }).pipe(
      tap(res => {
        localStorage.setItem(this.tokenKey, res.token);
        this.setUserFromToken(res.token);
      })
    );
  }

  logout() {
    localStorage.removeItem(this.tokenKey);
    this.clearWorkMode();
    this.user.set(null);
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return localStorage.getItem(this.tokenKey);
  }

  isSuperAdmin(): boolean {
    return this.user()?.role === UserRole.SuperAdmin;
  }

  isAssistantOnly(): boolean {
    return this.user()?.role === UserRole.AssistantUser;
  }

  /** True if this user ALSO has assistant assignments (dual-role) */
  isAssistant(): boolean {
    return this.user()?.role === UserRole.AssistantUser ||
      !!this.user()?.isAssistant;
  }

  private setUserFromToken(token: string) {
    try {
      const payload: JwtPayload = JSON.parse(atob(token.split('.')[1]));
      const roleClaim = payload['role'] || payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
      const idClaim = payload['nameid'] || payload['sub'] || payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'];
      const emailClaim = payload['email'] || payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'];
      const nameClaim = payload['unique_name'] || payload['name'] || emailClaim;
      const planClaim = payload['subscriptionPlan'];

      let role = UserRole.BusinessOwner;
      if (roleClaim === 'SuperAdmin') role = UserRole.SuperAdmin;
      else if (roleClaim === 'Staff') role = UserRole.Staff;
      else if (roleClaim === 'AssistantUser') role = UserRole.AssistantUser;

      let subscriptionPlan = SubscriptionPlan.Trial;
      if (planClaim === 'Free' || planClaim === SubscriptionPlan.Free) subscriptionPlan = SubscriptionPlan.Free;
      else if (planClaim === 'Premium' || planClaim === SubscriptionPlan.Premium) subscriptionPlan = SubscriptionPlan.Premium;

      const assistantIdClaim = payload['assistantId'];
      this.user.set({
        id: idClaim,
        fullName: nameClaim || 'User',
        email: emailClaim || '',
        role,
        subscriptionPlan,
        isAssistant: !!assistantIdClaim,
        assistantId: assistantIdClaim
      });
    } catch {
      this.user.set(null);
    }
  }
  getWorkMode(): 'manager' | 'assistant' | null {
    return localStorage.getItem(this.modeKey) as 'manager' | 'assistant' | null;
  }

  setWorkMode(mode: 'manager' | 'assistant') {
    localStorage.setItem(this.modeKey, mode);
  }

  clearWorkMode() {
    localStorage.removeItem(this.modeKey);
  }

  sendOtp(email: string, type: 'registration' | 'password_reset'): Observable<any> {
    return this.http.post(`${environment.apiUrl}/auth/send-otp`, { email, type });
  }

  verifyOtp(email: string, code: string, type: string): Observable<any> {
    return this.http.post(`${environment.apiUrl}/auth/verify-otp`, { email, code, type });
  }

  forgotPassword(email: string): Observable<any> {
    return this.http.post(`${environment.apiUrl}/auth/forgot-password`, { email });
  }

  resetPassword(email: string, otpCode: string, newPassword: string): Observable<any> {
    return this.http.post(`${environment.apiUrl}/auth/reset-password`, { email, otpCode, newPassword });
  }

  changePassword(currentPassword: string, newPassword: string): Observable<any> {
    return this.http.post(`${environment.apiUrl}/auth/change-password`, { currentPassword, newPassword });
  }
}
