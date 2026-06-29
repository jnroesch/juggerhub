import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Redirects unauthenticated access of guarded routes toward sign-in. This is a
 * UX guard only — protected data is enforced server-side (the API rejects
 * unauthenticated requests with 401 regardless of any client state).
 */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated()) {
    return true;
  }

  return router.parseUrl('/sign-in');
};
