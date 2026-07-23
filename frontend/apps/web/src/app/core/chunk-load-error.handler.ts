import { ErrorHandler, Injectable, inject } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';

/** sessionStorage key marking that we've already auto-reloaded for a stale bundle in this tab. */
const RELOADED_FLAG = 'jh-chunk-reloaded';

/**
 * Recover from a stale lazy-chunk after a frontend redeploy.
 *
 * The app ships as hashed lazy chunks served by nginx. When the bundle is rebuilt, every chunk gets a
 * new hash — so a tab still running the *previous* build asks the server for an old
 * `chunk-XXXX.js` that no longer exists, and the dynamic import rejects with
 * "Failed to fetch dynamically imported module". Angular routes lazy-load failures through the global
 * {@link ErrorHandler}, so we catch them here and reload once, which fetches a fresh `index.html` with
 * valid chunk names and lets the navigation retry.
 *
 * The reload is guarded to **once per bundle**: if a chunk is genuinely broken (a bad deploy, not just
 * stale), a single failed reload stops here and logs, so the user is never trapped in a reload loop.
 * A completed navigation clears the guard, so a *later* redeploy that strands this same long-lived tab
 * can self-heal again.
 */
@Injectable()
export class ChunkLoadErrorHandler implements ErrorHandler {
  private readonly router = inject(Router);

  constructor() {
    // A navigation that completes means the current bundle loaded fine — reset the guard.
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe(() => this.safeStorage()?.removeItem(RELOADED_FLAG));
  }

  handleError(error: unknown): void {
    const storage = this.safeStorage();
    if (this.isChunkLoadError(error) && storage?.getItem(RELOADED_FLAG) !== '1') {
      storage?.setItem(RELOADED_FLAG, '1');
      // Full reload rather than router retry: only a fresh index.html carries the new chunk hashes.
      this.reloadPage();
      return;
    }

    console.error(error);
  }

  /** Seam over the hard document reload (which can't run under jsdom) so the guard stays testable. */
  protected reloadPage(): void {
    window.location.reload();
  }

  /** True for the family of "the lazy chunk isn't there" messages across browsers/bundlers. */
  private isChunkLoadError(error: unknown): boolean {
    const message =
      error instanceof Error
        ? error.message
        : typeof error === 'string'
          ? error
          : String((error as { message?: string } | null)?.message ?? '');

    return /Failed to fetch dynamically imported module|error loading dynamically imported module|Importing a module script failed|ChunkLoadError|Loading chunk \d+ failed/i.test(
      message,
    );
  }

  /** Guard against any non-browser context (SSR/tests) where storage isn't available. */
  private safeStorage(): Storage | null {
    try {
      return typeof window !== 'undefined' ? window.sessionStorage : null;
    } catch {
      return null;
    }
  }
}
