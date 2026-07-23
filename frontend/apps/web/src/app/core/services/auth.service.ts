import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, finalize, map, of, shareReplay, switchMap, tap } from 'rxjs';
import {
  AuthUser,
  ForgotPasswordRequest,
  LoginRequest,
  MessageResponse,
  PasswordPolicy,
  RegisterRequest,
  ResendVerificationRequest,
  ResetPasswordRequest,
  VerifyEmailRequest,
} from '../models/auth.models';

/**
 * Real client-side auth state + API. The server is the security boundary (tokens
 * live only in httpOnly cookies); this signal state is UX convenience only.
 *
 * `user`: `undefined` = not yet probed, `null` = known-not-authenticated,
 * `AuthUser` = authenticated.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/auth';

  private readonly user = signal<AuthUser | null | undefined>(undefined);
  private refreshInFlight: Observable<AuthUser> | null = null;

  readonly currentUser = computed(() => this.user() ?? null);
  readonly isAuthenticated = computed(() => !!this.user());
  /**
   * Raw session state: `undefined` = not yet probed, `null` = known anonymous, `AuthUser` = signed in.
   * The shell uses this to show its public (Sign in) bar ONLY once we know the visitor is anonymous,
   * avoiding a nav flash for signed-in users on first load.
   */
  readonly userState = this.user.asReadonly();

  register(request: RegisterRequest): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${this.base}/register`, request);
  }

  verifyEmail(request: VerifyEmailRequest): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${this.base}/verify-email`, request);
  }

  resendVerification(request: ResendVerificationRequest): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${this.base}/resend-verification`, request);
  }

  login(request: LoginRequest): Observable<AuthUser> {
    return this.http
      .post<AuthUser>(`${this.base}/login`, request)
      .pipe(tap((u) => this.user.set(u)));
  }

  logout(): Observable<void> {
    return this.http
      .post<void>(`${this.base}/logout`, {})
      .pipe(tap(() => this.user.set(null)));
  }

  forgotPassword(request: ForgotPasswordRequest): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${this.base}/forgot-password`, request);
  }

  resetPassword(request: ResetPasswordRequest): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(`${this.base}/reset-password`, request);
  }

  getPasswordPolicy(): Observable<PasswordPolicy> {
    return this.http.get<PasswordPolicy>(`${this.base}/password-policy`);
  }

  /**
   * Single-flight refresh: concurrent 401s share ONE /auth/refresh call (so the
   * rotating token isn't rotated N times → false reuse-detection). Resets when done.
   */
  refreshSession(): Observable<AuthUser> {
    this.refreshInFlight ??= this.http.post<AuthUser>(`${this.base}/refresh`, {}).pipe(
      tap((u) => this.user.set(u)),
      finalize(() => (this.refreshInFlight = null)),
      shareReplay(1),
    );
    return this.refreshInFlight;
  }

  /** Hydrate state from the server: /me, falling back to one refresh + /me. */
  loadSession(): Observable<AuthUser | null> {
    return this.http.get<AuthUser>(`${this.base}/me`).pipe(
      map((u) => u as AuthUser | null),
      // On 401, a valid refresh token can still recover the session.
      catchError(() =>
        this.refreshSession().pipe(
          switchMap(() => this.http.get<AuthUser>(`${this.base}/me`)),
          map((u) => u as AuthUser | null),
          catchError(() => of(null)),
        ),
      ),
      tap((u) => this.user.set(u ?? null)),
    );
  }

  /** Returns cached state if known, otherwise probes the server. Used by the guard. */
  ensureSession(): Observable<AuthUser | null> {
    const current = this.user();
    return current === undefined ? this.loadSession() : of(current);
  }

  /** Clears local auth state (used by the interceptor when refresh fails). */
  clearSession(): void {
    this.user.set(null);
  }
}
