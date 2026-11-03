import { signal } from '@angular/core';
import { describe, expect, it, vi } from 'vitest';
import { of } from 'rxjs';
import { SocialLoginComponent } from './social-login.component';
import { UserRole } from '../../../core/models/models';

function runLogin(user: any, savedMode: 'manager' | 'assistant' | null = null) {
  const auth = {
    externalLogin: vi.fn(() => of({ token: 'token' })),
    user: signal(user),
    getWorkMode: vi.fn(() => savedMode),
    setWorkMode: vi.fn()
  };
  const router = { navigate: vi.fn() };
  const toast = { success: vi.fn(), error: vi.fn() };
  const component = new SocialLoginComponent({} as any, auth as any, router as any, toast as any);
  (component as any).completeLogin('google', 'provider-token');
  return { auth, router, toast };
}

describe('SocialLoginComponent post-login routing', () => {
  it('routes manager-only users to the manager dashboard and remembers manager mode', () => {
    const { auth, router } = runLogin({ role: UserRole.BusinessOwner, isAssistant: false });
    expect(router.navigate).toHaveBeenCalledWith(['/app/dashboard']);
    expect(auth.setWorkMode).toHaveBeenCalledWith('manager');
  });

  it('routes assistant-only users to the assistant portal and remembers assistant mode', () => {
    const { auth, router } = runLogin({ role: UserRole.AssistantUser, isAssistant: false });
    expect(router.navigate).toHaveBeenCalledWith(['/assistant/dashboard']);
    expect(auth.setWorkMode).toHaveBeenCalledWith('assistant');
  });

  it('uses the remembered assistant mode for dual-role users', () => {
    const { auth, router } = runLogin({ role: UserRole.BusinessOwner, isAssistant: true }, 'assistant');
    expect(router.navigate).toHaveBeenCalledWith(['/assistant/dashboard']);
    expect(auth.setWorkMode).not.toHaveBeenCalled();
  });

  it('uses the remembered manager mode for dual-role users', () => {
    const { auth, router } = runLogin({ role: UserRole.BusinessOwner, isAssistant: true }, 'manager');
    expect(router.navigate).toHaveBeenCalledWith(['/app/dashboard']);
    expect(auth.setWorkMode).not.toHaveBeenCalled();
  });

  it('opens the mode picker for dual-role users without a remembered mode', () => {
    const { auth, router } = runLogin({ role: UserRole.BusinessOwner, isAssistant: true });
    expect(router.navigate).toHaveBeenCalledWith(['/mode']);
    expect(auth.setWorkMode).not.toHaveBeenCalled();
  });

  it.each([null, undefined, {}])('safely applies the password-login manager fallback for absent or malformed users', user => {
    const { auth, router } = runLogin(user);
    expect(router.navigate).toHaveBeenCalledWith(['/app/dashboard']);
    expect(auth.setWorkMode).toHaveBeenCalledWith('manager');
  });
});
