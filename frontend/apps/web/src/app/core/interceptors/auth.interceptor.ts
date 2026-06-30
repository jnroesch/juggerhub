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
 * Attaches credentials (so the httpOnly cookies travel) and, on a 401 for a normal
 * request, performs a SINGLE-FLIGHT silent refresh and retries once. If the refresh
 * fails, clears client state and routes to sign-in. The server stays the security
 * boundary; this only shapes the experience.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const authReq = req.clone({ withCredentials: true });
  const skip = SKIP_REFRESH.some((path) => req.url.includes(path));

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
