import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { Subject, catchError, combineLatest, of, startWith, switchMap, tap } from 'rxjs';

/** One section of a legal document: a heading and its paragraphs. */
export interface LegalSection {
  heading: string;
  body: string[];
}

/** One legal document — the privacy policy or the imprint. */
export interface LegalDocument {
  title: string;
  intro: string[];
  sections: Record<string, LegalSection>;
}

/** The whole `legal` catalog for one language. */
export interface LegalContent {
  meta: {
    lastUpdated: string;
    lastUpdatedLabel: string;
    authoritativeNotice: string;
    tocLabel: string;
    loadErrorTitle: string;
    loadErrorBody: string;
    retry: string;
  };
  crossLink: Record<string, string>;
  privacy: LegalDocument;
  imprint: LegalDocument;
}

/**
 * Loads the long-form legal text for the active language (feature 036).
 *
 * The files live at `public/i18n/legal/{lang}.json` — the same folder layout Transloco scopes
 * use, and the same files the catalog guard tests check — but they are fetched here rather than
 * registered as a Transloco scope. That is a deliberate divergence from research.md R2, taken
 * during implementation for two reasons:
 *
 *  1. **Transloco has no error surface.** A failed scope load leaves keys missing, and the
 *     global `useFallbackTranslation: true` then renders the ENGLISH text in its place. For the
 *     legally authoritative German document that is the exact failure the feature is built to
 *     prevent — and it would happen silently. Fetching the document ourselves turns a failed
 *     load into a visible error (contracts/routes.md PC-7) instead of a wrong-language document.
 *  2. **Legal prose is content, not labels.** Paragraphs are arrays; the page needs the document
 *     shape intact, not a flattened key space.
 *
 * Everything else R2 decided is unchanged: the prose stays out of the always-loaded main
 * catalogs, it is fetched only when a legal route activates, and the language switcher still
 * drives it (via `langChanges$`). The short footer/nav labels remain in the main catalog under
 * `legal.*`, because the footer renders on every screen.
 */
@Injectable()
export class LegalContentService {
  private readonly http = inject(HttpClient);
  private readonly transloco = inject(TranslocoService);

  private readonly _content = signal<LegalContent | null>(null);
  private readonly _failed = signal(false);

  /** The loaded document set, or null while loading / after a failure. */
  readonly content = this._content.asReadonly();
  /** True when the fetch failed. Drives the visible error state — never an empty document. */
  readonly failed = this._failed.asReadonly();

  /** The active language, so the page can decide whether German governs or is being translated. */
  readonly lang = signal(this.transloco.getActiveLang());

  /** Manual re-fetch, pushed by the error state's "Try again". */
  private readonly retrigger = new Subject<void>();

  /**
   * Follows the active language and (re)loads the matching document. Subscribe once, from the
   * page, so the fetch is tied to that page's lifetime rather than the app's — `retry()` feeds
   * the same stream instead of opening a second subscription.
   */
  load() {
    return combineLatest([this.transloco.langChanges$, this.retrigger.pipe(startWith(undefined))]).pipe(
      tap(([lang]) => {
        this.lang.set(lang);
        this._failed.set(false);
      }),
      switchMap(([lang]) =>
        this.http.get<LegalContent>(`/i18n/legal/${lang}.json`).pipe(
          catchError(() => {
            // No retry loop here: the shared retry interceptor (feature 028) already covers the
            // transient case on this GET. A persistent failure must surface, not be papered over
            // — a blank privacy policy reads as a policy that says nothing (PC-7).
            this._failed.set(true);
            return of(null);
          }),
        ),
      ),
      tap((content) => this._content.set(content)),
    );
  }

  /** Re-run the fetch for the current language. */
  retry(): void {
    this.retrigger.next();
  }
}
