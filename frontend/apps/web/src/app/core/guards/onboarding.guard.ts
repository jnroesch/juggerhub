import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../services/auth.service';

/**
 * Keeps the one-time onboarding flow one-time: an already-onboarded (or, defensively,
 * unauthenticated) user is redirected to the dashboard. Pairs with `authGuard`, which
 * runs first and handles the signed-out → sign-in redirect. UX only — the server
 * remains the authority for whether onboarding is complete.
 */
export const onboardingGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth
    .ensureSession()
    .pipe(map((user) => (user && !user.onboardingCompleted ? true : router.parseUrl('/'))));
};
