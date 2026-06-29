import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

/**
 * Attaches credentials (so the httpOnly JWT cookie travels with same-origin API
 * calls) and handles 401s: it attempts a session renewal and, failing that,
 * routes toward sign-in. The server remains the security boundary; this only
 * shapes the client experience.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const authReq = req.clone({ withCredentials: true });

  return next(authReq).pipe(
    catchError((error: unknown) => {
      const isUnauthorized = error instanceof HttpErrorResponse && error.status === 401;
      if (!isUnauthorized || req.url.includes('/refresh')) {
        return throwError(() => error);
      }

      return auth.refresh().pipe(
        switchMap((renewed) => {
          if (renewed) {
            return next(req.clone({ withCredentials: true }));
          }
          return redirectToSignIn();
        }),
        catchError(() => redirectToSignIn()),
      );

      function redirectToSignIn() {
        auth.signOut();
        router.navigate(['/sign-in']);
        return throwError(() => error);
      }
    }),
  );
};
