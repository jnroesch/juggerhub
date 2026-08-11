import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { merge } from 'rxjs';
import { filter, map } from 'rxjs/operators';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { SearchService } from '../../../core/services/search.service';
import { ProfileService } from '../../../core/services/profile.service';
import { FilterChip, PlayerBrowseParams, PlayerCard, SortOption } from '../../../core/models/search.models';
import { POMPFEN_CATALOG, Pompfe, pompfeLabelKey } from '../../../shared/pompfen.catalog';
import { BrowseList } from '../browse-list';
import { BrowseShellComponent } from '../browse-shell/browse-shell.component';
import { FilterPanelComponent } from '../filter-panel/filter-panel.component';
import { CountryPickerComponent } from '../../../shared/country-picker/country-picker.component';

/**
 * Players browse page (feature 007, US3). Same shell as Teams/Events; the player filter set is
 * position (derived from declared pompfen) and city. Only players who opted into search appear
 * (enforced server-side). Rows link to /u/:handle. Sort is A–Z / Nearest first, on the feature 030
 * pattern the other three tabs already use.
 */
@Component({
  selector: 'jh-browse-players',
  imports: [RouterLink, BrowseShellComponent, FilterPanelComponent, CountryPickerComponent, TranslocoPipe],
  templateUrl: './browse-players.component.html',
  styleUrl: './browse-players.component.css',
})
export class BrowsePlayersComponent implements OnInit, OnDestroy {
  private readonly search = inject(SearchService);
  private readonly profiles = inject(ProfileService);
  private readonly t = inject(TranslocoService);
  // Recompute translated labels (chips/count) on a language change AND when a catalogue finishes
  // loading. A plain `toSignal(langChanges$)` is not enough: the catalogue loads asynchronously, and
  // a `computed()` (e.g. `chips()`) whose other dependencies never change keeps whatever
  // `translate()` returned first — the raw key. `equal: () => false` is load-bearing so a repeat
  // `langChanges$` emission of the same language still notifies. See browse-trainings for the full
  // write-up (GH #147).
  private readonly lang = toSignal(
    merge(
      this.t.langChanges$,
      this.t.events$.pipe(
        filter((e) => e.type === 'translationLoadSuccess'),
        map(() => this.t.getActiveLang()),
      ),
    ),
    { initialValue: this.t.getActiveLang(), equal: () => false },
  );
  protected readonly catalog = POMPFEN_CATALOG;

  protected readonly query = signal('');
  protected readonly positions = signal<Pompfe[]>([]);
  protected readonly city = signal('');
  // Sort selection (feature 030 pattern). "Proximity" measures home city → home city, so it is only
  // offered once the viewer has one of their own — the server derives that anchor from their profile
  // and 409s without it. Players who have set no home city drop out of the nearest-first view
  // entirely, which is why A–Z stays the default.
  protected readonly sort = signal<'DisplayNameAsc' | 'Proximity'>('DisplayNameAsc');
  protected readonly hasHomeCity = signal(false);

  /**
   * "Every player on JuggerHub is listed here" is true of every ordering EXCEPT nearest-first,
   * which drops players who have set no home city — the distance join has no row for them. The note
   * is withdrawn for that sort rather than left contradicting the list in front of the viewer.
   */
  protected readonly note = computed(() => {
    this.lang();
    return this.sort() === 'Proximity' ? null : this.t.translate('browse.players.note');
  });

  protected readonly sortOptions = computed<SortOption[]>(() => {
    this.lang();
    const opts: SortOption[] = [{ value: 'DisplayNameAsc', label: this.t.translate('browse.sortNameAsc') }];
    if (this.hasHomeCity()) {
      opts.push({ value: 'Proximity', label: this.t.translate('browse.sortNearest') });
    }
    return opts;
  });

  protected readonly filtersOpen = signal(false);
  protected readonly pendingPositions = signal<Pompfe[]>([]);
  protected readonly pendingCity = signal('');
  protected readonly pendingCount = signal<number | null>(null);

  protected readonly list = new BrowseList<PlayerCard>((skip, take) =>
    this.search.browsePlayers({ ...this.appliedParams(), skip, take }),
  );

  protected readonly activeFilterCount = computed(() => this.positions().length + (this.city().trim() ? 1 : 0));

  protected readonly chips = computed<FilterChip[]>(() => {
    const chips: FilterChip[] = this.positions().map((p) => ({ key: `pos:${p}`, label: this.label(p) }));
    if (this.city().trim()) {
      chips.push({ key: 'city', label: this.city().trim() });
    }
    return chips;
  });

  ngOnInit(): void {
    this.reload();
    // Offer "Nearest first" only when the viewer has a home city to measure from.
    this.profiles.getMineCached().subscribe({
      next: (p) => this.hasHomeCity.set(p.location != null),
      error: () => this.hasHomeCity.set(false),
    });
  }

  ngOnDestroy(): void {
    this.list.destroy();
  }

  protected label(p: Pompfe): string {
    this.lang();
    return this.t.translate(pompfeLabelKey(p));
  }

  protected onQuery(q: string): void {
    this.query.set(q);
    this.reload();
  }

  protected openFilters(): void {
    this.pendingPositions.set([...this.positions()]);
    this.pendingCity.set(this.city());
    this.refreshPendingCount();
    this.filtersOpen.set(true);
  }

  protected applyFilters(): void {
    this.positions.set([...this.pendingPositions()]);
    this.city.set(this.pendingCity());
    this.filtersOpen.set(false);
    this.reload();
  }

  protected resetFilters(): void {
    this.pendingPositions.set([]);
    this.pendingCity.set('');
    this.refreshPendingCount();
  }

  /** The Sort menu picked a new ordering — applies instantly. */
  protected onSortChange(value: string): void {
    this.sort.set(value === 'Proximity' && this.hasHomeCity() ? 'Proximity' : 'DisplayNameAsc');
    this.reload();
  }

  protected removeChip(key: string): void {
    if (key.startsWith('pos:')) {
      const value = key.slice(4) as Pompfe;
      this.positions.update((list) => list.filter((p) => p !== value));
    } else if (key === 'city') {
      this.city.set('');
    }
    this.reload();
  }

  protected clearAll(): void {
    this.query.set('');
    this.positions.set([]);
    this.city.set('');
    this.sort.set('DisplayNameAsc');
    this.reload();
  }

  protected isPending(p: Pompfe): boolean {
    return this.pendingPositions().includes(p);
  }

  protected togglePending(p: Pompfe): void {
    this.pendingPositions.update((list) =>
      list.includes(p) ? list.filter((x) => x !== p) : [...list, p],
    );
    this.refreshPendingCount();
  }

  protected onPendingCity(value: string): void {
    this.pendingCity.set(value);
    this.refreshPendingCount();
  }

  private appliedParams(): PlayerBrowseParams {
    return {
      q: this.query() || undefined,
      positions: this.positions().length ? this.positions() : undefined,
      country: this.city().trim() || undefined,
      sort: this.sort(),
    };
  }

  private reload(): void {
    this.list.filtered.set(Boolean(this.query().trim()) || this.positions().length > 0 || Boolean(this.city().trim()));
    this.list.reload();
  }

  private refreshPendingCount(): void {
    this.pendingCount.set(null);
    this.search
      .browsePlayers({
        q: this.query() || undefined,
        positions: this.pendingPositions().length ? this.pendingPositions() : undefined,
        country: this.pendingCity().trim() || undefined,
        take: 0,
      })
      .subscribe({
        next: (page) => this.pendingCount.set(page.totalCount),
        error: () => this.pendingCount.set(null),
      });
  }
}
