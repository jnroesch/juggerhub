import { Injectable, signal } from '@angular/core';
import { Observable, of } from 'rxjs';

/**
 * Client-side session state. This is a UX convenience only — never the security
 * boundary, which is enforced server-side (the API's 401). The sign-in / refresh
 * / sign-out endpoints are deferred to the auth feature; the surfaces here are
 * stubs the guard and interceptor depend on.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly authenticated = signal(false);

  isAuthenticated(): boolean {
    return this.authenticated();
  }

  /**
   * Attempt to renew the session. No refresh endpoint exists yet, so this
   * reports failure; the auth feature will call the refresh endpoint here.
   */
  refresh(): Observable<boolean> {
    return of(false);
  }

  signOut(): void {
    this.authenticated.set(false);
  }
}
