import { HttpClient } from '@angular/common/http';
import { Injectable, effect, inject, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { AuthService } from '../services/auth.service';
import {
  DEFAULT_LANGUAGE,
  SupportedLanguage,
  resolveLanguage,
} from './supported-languages';

const STORAGE_KEY = 'jh.lang';

/**
 * The single authority for "what language is the app in" (feature 031).
 *
 * Resolves the effective language by the fixed precedence (FR-007):
 *   account preference (when signed in) → locally stored choice → browser language → English,
 * and applies it everywhere it must show: the Transloco active catalog (which also drives
 * transloco-locale date/number formatting via the lang→locale mapping) and the document's `lang`
 * attribute — the latter both for assistive tech (FR-016) and as the source the language HTTP
 * interceptor reads to stamp `Accept-Language`.
 *
 * An effect re-resolves whenever the auth session changes, so signing in to an account that has a
 * stored preference switches the UI to it, and signing out falls back to the local/browser choice.
 */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly transloco = inject(TranslocoService);
  private readonly auth = inject(AuthService);
  private readonly http = inject(HttpClient);

  private readonly _language = signal<SupportedLanguage>(DEFAULT_LANGUAGE);

  /** The active interface language (FR-001). */
  readonly language = this._language.asReadonly();

  constructor() {
    // Re-resolve when the session becomes known / changes (sign-in, sign-out, refresh).
    // Runs once immediately (userState is `undefined` at first) so detection applies at startup.
    effect(() => {
      const user = this.auth.userState();
      const accountPref = user ? user.preferredLanguage : null;
      this.apply(this.resolve(accountPref));
    });
  }

  /**
   * Change the language on explicit user action (FR-004): applies immediately (no reload), persists
   * locally for return visits (FR-006), and — when signed in — persists to the account (FR-005) and
   * syncs the cached session so the precedence keeps the new choice. Never signs the user out (FR-015).
   */
  select(language: SupportedLanguage): void {
    this.apply(language);
    this.writeStored(language);

    if (this.auth.isAuthenticated()) {
      this.http.put('/api/v1/account/language', { language }).subscribe({
        next: () => this.auth.setPreferredLanguage(language),
        // On failure the choice stays applied + stored locally; it re-syncs on the next /me.
        error: () => undefined,
      });
    }
  }

  /** Resolve the effective language from the precedence chain (FR-007). */
  private resolve(accountPref: string | null): SupportedLanguage {
    return resolveLanguage(accountPref, this.readStored(), this.browserLanguage());
  }

  private apply(language: SupportedLanguage): void {
    if (this._language() !== language) {
      this._language.set(language);
    }
    if (this.transloco.getActiveLang() !== language) {
      this.transloco.setActiveLang(language);
    }
    if (typeof document !== 'undefined') {
      document.documentElement.lang = language;
    }
  }

  private readStored(): string | null {
    try {
      return typeof localStorage !== 'undefined' ? localStorage.getItem(STORAGE_KEY) : null;
    } catch {
      return null;
    }
  }

  private writeStored(language: SupportedLanguage): void {
    try {
      localStorage?.setItem(STORAGE_KEY, language);
    } catch {
      // Private-mode / storage-disabled: the choice still applies for this session.
    }
  }

  private browserLanguage(): string | null {
    return typeof navigator !== 'undefined' ? navigator.language : null;
  }
}
