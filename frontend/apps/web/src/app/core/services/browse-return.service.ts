import { Injectable, signal } from '@angular/core';
import { Params } from '@angular/router';

/**
 * Remembers each browse list's query string (GH #279), so a detail page's "‹ Trainings" or
 * "‹ Events" link returns to the list the viewer left — search, filters and sort intact.
 *
 * ⚠ This is NOT historical back navigation, and must not become it. The destination is always the
 * same list — the detail page's hierarchical parent for this viewer — and only its query string is
 * restored. That is what keeps the link working, and its label true, on a deep link from an alert
 * or a shared URL, and after an edit-and-save: exactly the cases where `Location.back()` leaves the
 * app, returns to a form, or goes nowhere. The browser's own back button is the historical control.
 *
 * In memory only, and cleared when the session ends: a search can be a player's name, and a shared
 * device must not hand it to the next person.
 */
@Injectable({ providedIn: 'root' })
export class BrowseReturnService {
  private readonly lists = signal<ReadonlyMap<string, Params>>(new Map());

  /** Record the applied query string of the browse list at `path` (e.g. `/browse/trainings`). */
  remember(path: string, params: Params): void {
    this.lists.update((lists) => new Map(lists).set(path, params));
  }

  /** The query string to reopen the list at `path` as the viewer last left it; empty if never visited. */
  queryParams(path: string): Params {
    return this.lists().get(path) ?? {};
  }

  clear(): void {
    this.lists.set(new Map());
  }
}
