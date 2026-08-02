import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AccountDeletionPreview } from '../models/account-deletion.models';

/**
 * Self-service account deletion (feature 037). Two calls: what would happen, and do it.
 *
 * Neither takes an account identifier — the subject is always the authenticated caller, so no
 * request shape exists in which one member could target another (spec FR-002).
 */
@Injectable({ providedIn: 'root' })
export class AccountDeletionService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/account';

  /** Advisory. Re-checked server-side inside the transaction at confirmation (FR-013). */
  preview(): Observable<AccountDeletionPreview> {
    return this.http.get<AccountDeletionPreview>(`${this.base}/deletion-preview`);
  }

  /**
   * Erase the caller's account. Immediate, irreversible, all-or-nothing.
   *
   * **Never retried, and nothing here has to arrange that.** Constitution Principle VII forbids
   * automatically retrying a browser-hop mutation — a request that timed out may already have
   * erased the account, and the client cannot tell. `retryInterceptor` only retries safe methods,
   * so a POST is excluded structurally rather than by an opt-out this call site could forget.
   */
  deleteAccount(password: string, confirmation: string): Observable<void> {
    return this.http.post<void>(`${this.base}/deletion`, { password, confirmation });
  }
}
