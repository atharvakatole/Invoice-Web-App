import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { UserRole } from '../models/models';

export const assistantGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const user = auth.user();
  if (!user) { router.navigate(['/login']); return false; }

  if (user.role === UserRole.AssistantUser || user.isAssistant) return true;

  router.navigate(['/app/dashboard']);
  return false;
};
