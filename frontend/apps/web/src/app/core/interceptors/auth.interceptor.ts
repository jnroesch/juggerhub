import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

/**
 * Endpoints where a 401 is expected and handled by the caller — never trigger
 * refresh-and-retry or a sign-in redirect for these (the auth flows themselves and
 * the /me + /refresh probes).
 */
const SKIP_REFRESH = [
  '/auth/login',
  '/auth/register',
  '/auth/refresh',
  '/auth/forgot-password',
  '/auth/reset-password',
  '/auth/verify-email',
  '/auth/resend-verification',
  '/auth/me',
];

/**
 * Feature 037 — the account-deletion POST re-authenticates, so ITS 401 means "wrong password", not
 * "session expired". Without this the interceptor would refresh a session that is perfectly valid,
 * retry the delete with the same wrong password, and end up signing the member out — mistyping your
 * password while deleting your account would log you out instead of telling you. Caught by the e2e,
 * which is the only place the two meanings of 401 collide.
 *
 * Matched on method as well as path, deliberately: `GET /account/deletion-preview` shares the
 * prefix, and a 401 there really does mean the session expired and should still refresh.
 */
function isReauthenticatingRequest(method: string, url: string): boolean {
  return method.toUpperCase() === 'POST' && url.includes('/account/deletion');
}

/**
 * Attaches credentials (so the httpOnly cookies travel) and, on a 401 for a normal
 * request, performs a SINGLE-FLIGHT silent refresh and retries once. If the refresh
 * fails, clears client state and routes to sign-in. The server stays the security
 * boundary; this only shapes the experience.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const authReq = req.clone({ withCredentials: true });
  const skip =
    SKIP_REFRESH.some((path) => req.url.includes(path)) ||
    isReauthenticatingRequest(req.method, req.url);

  return next(authReq).pipe(
    catchError((error: unknown) => {
      if (!skip && error instanceof HttpErrorResponse && error.status === 401) {
        return auth.refreshSession().pipe(
          switchMap(() => next(authReq)),
          catchError(() => {
            auth.clearSession();
            router.navigate(['/sign-in']);
            return throwError(() => error);
          }),
        );
      }
      return throwError(() => error);
    }),
  );
};
