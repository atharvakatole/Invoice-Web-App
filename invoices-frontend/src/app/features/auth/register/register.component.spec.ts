import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { AuthService } from '../../../core/services/auth.service';
import { SocialAuthService } from '../../../core/services/social-auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { RegisterComponent } from './register.component';

describe('RegisterComponent privacy-safe form hints', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RegisterComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: {} },
        { provide: SocialAuthService, useValue: { signInWithGoogle: () => new Promise(() => undefined) } },
        { provide: ToastService, useValue: {} }
      ]
    }).compileComponents();
  });

  afterEach(() => TestBed.resetTestingModule());

  it('uses generic placeholders and discourages autofill for registration identity fields', () => {
    const fixture = TestBed.createComponent(RegisterComponent);
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    const form = element.querySelector('form');

    expect(form?.getAttribute('autocomplete')).toBe('off');
    for (const field of ['fullName', 'username', 'email', 'phoneNumber', 'businessName', 'businessEmail']) {
      const input = element.querySelector(`[formControlName="${field}"]`);
      expect(input, `${field} input should be present`).not.toBeNull();
      expect(input?.getAttribute('autocomplete')).toBe('off');
      expect(input?.getAttribute('placeholder')).toMatch(/^Enter |^Choose /);
    }
    expect(element.textContent).not.toMatch(/aishwarya|acme|billing@/i);
    fixture.destroy();
  });

  it('confirms OTP delivery without rendering the entered email address', () => {
    const fixture = TestBed.createComponent(RegisterComponent);
    const component = fixture.componentInstance;
    component.form.patchValue({ email: 'person@example.test' });
    component.step.set('otp');
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;
    expect(element.textContent).toContain('We sent a 6-digit code to the email address you entered.');
    expect(element.textContent).not.toContain('person@example.test');
    fixture.destroy();
  });
});
