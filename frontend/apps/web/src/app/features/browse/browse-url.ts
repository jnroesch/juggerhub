import { DestroyRef, inject } from '@angular/core';
import { ActivatedRoute, ParamMap, Params, Router } from '@angular/router';
import { BrowseReturnService } from '../../core/services/browse-return.service';

/** A query string with every value as a list — the one shape both directions compare in. */
type Normalized = Record<string, string[]>;

/**
 * Keeps a browse page's applied search, filters and sort in its query string (GH #279). Before
 * this they lived only in the component, so every way of coming back to a list — the browser's
 * back button, a reload, a shared link, a detail page's "‹ Trainings" link — rebuilt it from the
 * defaults and the viewer's search was gone.
 *
 * Only non-default values are written, so an untouched list keeps a bare URL. Writes REPLACE the
 * current history entry: a filter change is not a place, and one entry per search keystroke would
 * make the back button replay every search before leaving the page.
 *
 * Each write is also handed to {@link BrowseReturnService}, which is how a detail page reopens the
 * list as the viewer left it.
 */
export class BrowseUrl {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly returns = inject(BrowseReturnService);
  private readonly destroyRef = inject(DestroyRef);

  /** The query string the URL holds right now, so the page's own writes are not applied twice. */
  private current: string | null = null;

  /** @param path The list's route, e.g. `/browse/trainings` — the key a detail page asks for. */
  constructor(private readonly path: string) {}

  /**
   * Applies the URL immediately (the list's initial state) and again whenever it changes under the
   * page. The second case matters: re-clicking the page's own tab, or the Browse nav item, navigates
   * to the bare path on the SAME component instance — and must reset the list rather than leave it
   * filtered behind a clean URL.
   *
   * `apply` sets the page's state from the params and reloads; it must reset every field the
   * params do not mention, since an absent param means "default".
   */
  connect(apply: (params: ParamMap) => void): void {
    const sub = this.route.queryParamMap.subscribe((params) => {
      const key = serialize(fromParamMap(params));
      if (key === this.current) {
        return; // the echo of our own write
      }
      this.current = key;
      apply(params);
    });
    this.destroyRef.onDestroy(() => sub.unsubscribe());
  }

  /** Records the applied state. Empty, null and undefined values are dropped — they are defaults. */
  write(params: Params): void {
    const clean = compact(params);
    this.returns.remember(this.path, clean);
    const key = serialize(clean);
    if (key === this.current) {
      return;
    }
    this.current = key;
    void this.router.navigate([], { relativeTo: this.route, queryParams: clean, replaceUrl: true });
  }
}

/** An ISO date (`YYYY-MM-DD`) from the URL, or `''` for anything else — a hand-edited URL must not reach the API. */
export function dateParam(params: ParamMap, key: string): string {
  const value = params.get(key) ?? '';
  return /^\d{4}-\d{2}-\d{2}$/.test(value) ? value : '';
}

function compact(params: Params): Normalized {
  const out: Normalized = {};
  for (const [key, value] of Object.entries(params)) {
    const list = (Array.isArray(value) ? value : [value])
      .filter((v) => v !== undefined && v !== null && v !== '')
      .map(String);
    if (list.length > 0) {
      out[key] = list;
    }
  }
  return out;
}

function fromParamMap(params: ParamMap): Normalized {
  const out: Normalized = {};
  for (const key of params.keys) {
    out[key] = params.getAll(key);
  }
  return out;
}

function serialize(params: Normalized): string {
  return JSON.stringify(Object.keys(params).sort().map((key) => [key, params[key]]));
}
