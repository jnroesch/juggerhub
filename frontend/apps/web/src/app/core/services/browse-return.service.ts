import { Injectable, signal } from '@angular/core';
import { Params } from '@angular/router';

/**
 * Navigation state a back link passes so the browse list also restores the viewer's typed search.
 * Router state lives in the browser's history entry — never in the address.
 */
export const RESTORE_BROWSE_SEARCH_KEY = 'restoreBrowseSearch';
export const RESTORE_BROWSE_SEARCH: Readonly<Record<string, unknown>> = { [RESTORE_BROWSE_SEARCH_KEY]: true };

interface RememberedList {
  params: Params;
  search: string;
}

/**
 * Remembers each browse list as the viewer last left it (GH #279), so a detail page's "‹ Trainings"
 * or "‹ Events" link returns to it — search, filters and sort intact.
 *
 * ⚠ This is NOT historical back navigation, and must not become it. The destination is always the
 * same list — the detail page's hierarchical parent for this viewer — and only its state is
 * restored. That is what keeps the link working, and its label true, on a deep link from an alert
 * or a shared URL, and after an edit-and-save: exactly the cases where `Location.back()` leaves the
 * app, returns to a form, or goes nowhere. The browser's own back button is the historical control.
 *
 * The typed search is held apart from the query string on purpose. Filters are values picked from
 * controls and already shown on screen as chips; the search is what the viewer TYPED, and the
 * privacy policy promises typed input never leaves the device — session recording masks every input
 * field. The recorder, unlike the page-view tracker, does not drop the query string, so a search in
 * the address would carry the masked text out through the URL. It travels only as navigation state.
 *
 * In memory only, and cleared when the session ends: a search can be a player's name, and a shared
 * device must not hand it to the next person.
 */
@Injectable({ providedIn: 'root' })
export class BrowseReturnService {
  private readonly lists = signal<ReadonlyMap<string, RememberedList>>(new Map());

  /** Record the browse list at `path` (e.g. `/browse/trainings`): its query string and typed search. */
  remember(path: string, params: Params, search: string): void {
    this.lists.update((lists) => new Map(lists).set(path, { params, search }));
  }

  /** The query string that reopens the list at `path` with its filters; empty if never visited. */
  queryParams(path: string): Params {
    return this.lists().get(path)?.params ?? {};
  }

  /** The search last typed on the list at `path`. Read only when the viewer returns to the list. */
  search(path: string): string {
    return this.lists().get(path)?.search ?? '';
  }

  clear(): void {
    this.lists.set(new Map());
  }
}
