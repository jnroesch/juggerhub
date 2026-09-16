import {
  DestroyRef,
  Component,
  ElementRef,
  HostListener,
  afterRenderEffect,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, debounceTime, filter, map } from 'rxjs';
import { TranslocoPipe } from '@jsverse/transloco';
import { BrowseState, FilterChip, SortOption } from '../../../core/models/search.models';
import { AlertComponent, ButtonDirective, ChipDirective, IconComponent, LoadingComponent } from '../../../shared/ui';

/**
 * Shared browse shell (feature 007) — the single implementation of the discovery behaviour
 * reused by the Teams, Events, and Players pages, so they are provably identical apart from
 * filter set, sort, and row content (SC-004). Presentational: it renders the header, live
 * search, Filters button + badge, Sort, active-filter chips, the results (via the
 * projected [rows] slot), and the empty / no-results / loading / error states. Data-fetching
 * and filter state live in each page (see BrowseList).
 */
@Component({
  selector: 'jh-browse-shell',
  imports: [RouterLink, RouterLinkActive, ButtonDirective, ChipDirective, LoadingComponent, AlertComponent, TranslocoPipe, IconComponent],
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

  /**
   * The page's applied search (GH #279) — restored from the URL, or emptied by "Clear all". Shown
   * in the box whenever it differs from what the box last reported.
   *
   * ⚠ Deliberately NOT a `[value]` binding. The page receives the search 250ms late and trimmed, so
   * binding it back would overwrite whatever was typed during the debounce, and strip a trailing
   * space mid-word. The box is written only when the page's value is news to it.
   */
  readonly searchValue = input('');

  private readonly searchBox = viewChild.required<ElementRef<HTMLInputElement>>('searchBox');
  /** What the box and the page last agreed the search is. */
  private agreed = '';

  constructor() {
    // Compared against `agreed`, not `distinctUntilChanged()`: after "Clear all" empties the box,
    // retyping the previous search must still be reported.
    this.queryInput
      .pipe(
        debounceTime(250),
        map((value) => value.trim()),
        filter((value) => value !== this.agreed),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((value) => {
        this.agreed = value;
        this.query.emit(value);
      });

    afterRenderEffect(() => {
      const value = this.searchValue();
      if (value !== this.agreed) {
        this.agreed = value;
        this.searchBox().nativeElement.value = value;
      }
    });
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
