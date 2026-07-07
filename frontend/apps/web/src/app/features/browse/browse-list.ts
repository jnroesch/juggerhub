import { Signal, computed, signal } from '@angular/core';
import { Observable, Subscription } from 'rxjs';
import { BrowseState, PagedResult } from '../../core/models/search.models';

/**
 * Reusable browse state machine (feature 007) shared by all three browse pages. Owns the
 * result list, pagination (skip/take with append-on-load-more), and the four display states
 * (loading / ready / empty / no-results / error). Kept framework-light (plain signals) so the
 * three pages behave identically and the logic is unit-testable in isolation.
 */
export class BrowseList<TCard> {
  private readonly take = 20;
  private skip = 0;
  private sub?: Subscription;

  private readonly _items = signal<TCard[]>([]);
  private readonly _total = signal(0);
  private readonly _loading = signal(false);
  private readonly _error = signal(false);
  private readonly _loaded = signal(false);

  /** Set by the page: true when a query or any non-default filter is active (drives no-results vs empty). */
  readonly filtered = signal(false);

  readonly items: Signal<TCard[]> = this._items.asReadonly();
  readonly total: Signal<number> = this._total.asReadonly();
  readonly loading: Signal<boolean> = this._loading.asReadonly();
  readonly hasMore = computed(() => this._items().length < this._total());
  /** Loading additional pages while results are already on screen. */
  readonly loadingMore = computed(() => this._loading() && this._items().length > 0);

  readonly state = computed<BrowseState>(() => {
    if (this._error()) {
      return 'error';
    }
    if (this._items().length > 0) {
      return 'ready';
    }
    if (this._loading() || !this._loaded()) {
      return 'loading';
    }
    return this.filtered() ? 'no-results' : 'empty';
  });

  /** @param fetcher Fetches one page using the page's *current* applied query + filters. */
  constructor(private readonly fetcher: (skip: number, take: number) => Observable<PagedResult<TCard>>) {}

  /** Reload from the first page (call on any query/filter/sort change). */
  reload(): void {
    this.skip = 0;
    this._error.set(false);
    this._loading.set(true);
    this.sub?.unsubscribe();
    this.sub = this.fetcher(0, this.take).subscribe({
      next: (page) => {
        this._items.set(page.items);
        this._total.set(page.totalCount);
        this.skip = page.items.length;
        this._loading.set(false);
        this._loaded.set(true);
      },
      error: () => {
        this._error.set(true);
        this._loading.set(false);
        this._loaded.set(true);
      },
    });
  }

  /** Append the next page (infinite scroll / "load more"). No-op while loading or at the end. */
  loadMore(): void {
    if (this._loading() || !this.hasMore()) {
      return;
    }
    this._loading.set(true);
    this.sub?.unsubscribe();
    this.sub = this.fetcher(this.skip, this.take).subscribe({
      next: (page) => {
        this._items.update((current) => [...current, ...page.items]);
        this._total.set(page.totalCount);
        this.skip += page.items.length;
        this._loading.set(false);
      },
      error: () => {
        this._error.set(true);
        this._loading.set(false);
      },
    });
  }

  destroy(): void {
    this.sub?.unsubscribe();
  }
}
