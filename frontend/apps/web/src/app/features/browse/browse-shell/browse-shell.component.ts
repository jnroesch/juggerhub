import { DestroyRef, Component, HostListener, computed, inject, input, output, signal, ChangeDetectionStrategy } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { TranslocoPipe } from '@jsverse/transloco';
import { BrowseState, FilterChip, SortOption } from '../../../core/models/search.models';
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
  imports: [RouterLink, RouterLinkActive, ButtonDirective, LoadingComponent, AlertComponent, TranslocoPipe],
  templateUrl: './browse-shell.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
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
  /**
   * Feature 030 — the sort choices for this page. The shell renders a single "Sort" menu button
   * (a dropdown, mirroring the Filters button) so every ordering, including "Nearest first", is
   * chosen the same way. The button is hidden when there are fewer than two options — a page with
   * one fixed order has nothing to choose.
   */
  readonly sortOptions = input<SortOption[]>([]);
  /** The currently applied sort value (matches one of `sortOptions[].value`). */
  readonly activeSort = input<string>('');
  readonly state = input<BrowseState>('loading');
  readonly loadingMore = input(false);
  readonly hasMore = input(false);
  /** Optional note under the header (e.g. players opt-in message). */
  readonly note = input<string | null>(null);

  /** Debounced search text. */
  readonly query = output<string>();
  /** Emits the chosen sort value when the viewer picks a different option. */
  readonly sortChange = output<string>();
  readonly openFilters = output<void>();
  readonly removeChip = output<string>();
  readonly clearAll = output<void>();
  readonly loadMore = output<void>();
  readonly retry = output<void>();

  /** Whether the sort dropdown is open. */
  protected readonly sortMenuOpen = signal(false);
  /** Show the Sort button only when there is an actual choice to make. */
  protected readonly showSort = computed(() => this.sortOptions().length > 1);
  /** Label of the applied sort, for the button face (falls back to the first option). */
  protected readonly activeSortLabel = computed(() => {
    const opts = this.sortOptions();
    return (opts.find((o) => o.value === this.activeSort()) ?? opts[0])?.label ?? '';
  });

  constructor() {
    this.queryInput
      .pipe(debounceTime(250), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe((value) => this.query.emit(value.trim()));
  }

  protected onSearchInput(event: Event): void {
    this.queryInput.next((event.target as HTMLInputElement).value);
  }

  protected toggleSortMenu(): void {
    this.sortMenuOpen.update((open) => !open);
  }

  protected selectSort(value: string): void {
    this.sortMenuOpen.set(false);
    if (value !== this.activeSort()) {
      this.sortChange.emit(value);
    }
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this.sortMenuOpen.set(false);
  }
}
