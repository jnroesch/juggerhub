import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../services/auth.service';

/**
 * Protects routes by hydrating session state from the server (a valid httpOnly
 * cookie counts even on a fresh load / silent-refresh). UX guard only — the API
 * still enforces 401 server-side regardless of this.
 */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.ensureSession().pipe(map((user) => (user ? true : router.parseUrl('/sign-in'))));
};
