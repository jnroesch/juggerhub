import { Injectable, inject, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { DEFAULT_LANGUAGE, SupportedLanguage } from './supported-languages';

/**
 * The single authority for "what language is the app in" (feature 031).
 *
 * Foundational responsibilities (this phase): hold the active language as a signal and apply a
 * change everywhere it must be reflected — the Transloco active catalog (which also drives
 * transloco-locale date/number formatting via the lang→locale mapping) and the document's `lang`
 * attribute so assistive technology announces content in the right language (FR-016).
 *
 * Later phases extend this service with browser detection (US1) and the full resolution
 * precedence + persistence (US2: account preference → localStorage → browser → English).
 */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly transloco = inject(TranslocoService);

  private readonly _language = signal<SupportedLanguage>(DEFAULT_LANGUAGE);

  /** The active interface language (FR-001). */
  readonly language = this._language.asReadonly();

  /**
   * Apply a language across the app. Immediate and reload-free (FR-004): Transloco re-renders
   * bound text in place and transloco-locale re-formats dates/numbers for the mapped locale.
   */
  setActive(lang: SupportedLanguage): void {
    this._language.set(lang);
    this.transloco.setActiveLang(lang);
    if (typeof document !== 'undefined') {
      document.documentElement.lang = lang;
    }
  }
}
