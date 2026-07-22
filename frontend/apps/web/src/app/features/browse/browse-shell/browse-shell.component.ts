import { DestroyRef, Component, inject, input, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { BrowseState, FilterChip } from '../../../core/models/search.models';
import { ButtonDirective, LoadingComponent, AlertComponent } from '../../../shared/ui';

/**
 * Shared browse shell (feature 007) — the single implementation of the discovery behaviour
 * reused by the Teams, Events, and Players pages, so they are provably identical apart from
 * filter set, sort, and row content (SC-004). Presentational: it renders the header, live
 * search, Filters button + badge, Sort, active-filter chips, count line, the results (via the
 * projected [rows] slot), and the empty / no-results / loading / error states. Data-fetching
 * and filter state live in each page (see BrowseList).
 */
@Component({
  selector: 'jh-browse-shell',
  imports: [RouterLink, RouterLinkActive, ButtonDirective, LoadingComponent, AlertComponent],
  templateUrl: './browse-shell.component.html',
  styleUrl: './browse-shell.component.css',
})
export class BrowseShellComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly queryInput = new Subject<string>();

  /** Page title, e.g. "Teams". */
  readonly title = input.required<string>();
  /** Search input placeholder, e.g. "Search teams…". */
  readonly searchPlaceholder = input('Search…');
  /** Result-count line, e.g. "3 teams · active · beginners welcome". */
  readonly countLabel = input('');
  /** Active-filter chips shown above the results. */
  readonly chips = input<FilterChip[]>([]);
  /** Number badge on the Filters button (0 = no badge). */
  readonly activeFilterCount = input(0);
  /** Sort control label, e.g. "A–Z". */
  readonly sortLabel = input('');
  readonly state = input<BrowseState>('loading');
  readonly loadingMore = input(false);
  readonly hasMore = input(false);
  /** Optional note under the header (e.g. players opt-in message). */
  readonly note = input<string | null>(null);

  /** Debounced search text. */
  readonly query = output<string>();
  readonly openFilters = output<void>();
  readonly removeChip = output<string>();
  readonly clearAll = output<void>();
  readonly loadMore = output<void>();
  readonly retry = output<void>();

  constructor() {
    this.queryInput
      .pipe(debounceTime(250), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe((value) => this.query.emit(value.trim()));
  }

  protected onSearchInput(event: Event): void {
    this.queryInput.next((event.target as HTMLInputElement).value);
  }
}
