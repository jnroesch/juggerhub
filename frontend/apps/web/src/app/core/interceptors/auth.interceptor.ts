import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

/**
 * Attaches credentials (so the httpOnly JWT cookie travels with same-origin API
 * calls) and handles 401s by signing out and routing toward sign-in. The server
 * remains the security boundary; this only shapes the client experience.
 *
 * Token refresh is intentionally not wired yet — no refresh endpoint exists. When
 * the auth feature adds one, reintroduce a refresh-and-retry step here (call it
 * before the sign-in redirect, and skip it for the refresh request itself).
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const authReq = req.clone({ withCredentials: true });

  return next(authReq).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        auth.signOut();
        router.navigate(['/sign-in']);
      }
      return throwError(() => error);
    }),
  );
};
