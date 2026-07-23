import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../services/auth.service';

/**
 * Protects routes by hydrating session state from the server (a valid httpOnly
 * cookie counts even on a fresh load / silent-refresh). UX guard only — the API
 * still enforces 401 server-side regardless of this.
 *
 * On a signed-out hit the attempted URL is carried to sign-in as `returnUrl`, so
 * after a successful login the user lands on the page they originally opened (e.g.
 * a team or event link shared while signed out). The open-redirect guard in
 * `safeReturnUrl` keeps only internal paths.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.ensureSession().pipe(
    map((user) =>
      user ? true : router.createUrlTree(['/sign-in'], { queryParams: { returnUrl: state.url } }),
    ),
  );
};
