import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { merge } from 'rxjs';
import { filter, map } from 'rxjs/operators';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { injectDateFormats } from '../../../core/i18n/locale-format';
import { SearchService } from '../../../core/services/search.service';
import { ProfileService } from '../../../core/services/profile.service';
import { FilterChip, SortOption, TrainingBrowseParams, TrainingCard } from '../../../core/models/search.models';
import { CityOption } from '../../../core/models/city.models';
import { BrowseList } from '../browse-list';
import { BrowseShellComponent } from '../browse-shell/browse-shell.component';
import { FilterPanelComponent } from '../filter-panel/filter-panel.component';
import { FilterToggleComponent } from '../filter-panel/filter-toggle.component';
import { CountryPickerComponent } from '../../../shared/country-picker/country-picker.component';
import { CityPickerComponent } from '../../../shared/city-picker/city-picker.component';

/**
 * Public-training browse page (feature 043). The fourth instance of the shared discovery shell,
 * alongside Teams, Events and Players — same search, filters, sort, chips, paging and
 * states, differing only in the filter set and the row (feature 007, SC-004).
 *
 * Only sessions teams have opened to everyone appear here, enforced server-side; a team-only
 * session is absent for every viewer, members of the owning team included. Rows link to the
 * existing session page, where an outsider responds as a guest.
 */
@Component({
  selector: 'jh-browse-trainings',
  imports: [
    RouterLink,
    BrowseShellComponent,
    FilterPanelComponent,
    FilterToggleComponent,
    CountryPickerComponent,
    CityPickerComponent,
    TranslocoPipe,
  ],
  templateUrl: './browse-trainings.component.html',
  styleUrl: './browse-trainings.component.css',
})
export class BrowseTrainingsComponent implements OnInit, OnDestroy {
  private readonly search = inject(SearchService);
  private readonly profiles = inject(ProfileService);
  private readonly t = inject(TranslocoService);
  private readonly fmt = injectDateFormats();

  /**
   * Recompute trigger for every label built with `t.translate()` — ticks on a language change **and**
   * on a catalogue finishing loading.
   *
   * ⚠ The obvious `toSignal(t.langChanges$, { initialValue: getActiveLang() })` is not enough, and
   * the failure is silent. A `computed()` only re-runs when a dependency actually changes, so a
   * label built before the catalogue arrived keeps the raw key forever unless something it reads
   * changes afterwards. `chips()` reads only filter state, which does not change when the catalogue
   * arrives, so without this trigger it would render `browse.trainings.chipUpcoming` verbatim.
   *
   * `equal: () => false` is the load-bearing part: `langChanges$` re-emitting the same language
   * would otherwise be swallowed by signal equality, which is exactly the case that matters on a
   * first load.
   *
   * The other three browse pages have the same defect (GH #147); not fixed here
   * because this feature must not touch them (spec FR-030).
   */
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

  protected readonly query = signal('');
  protected readonly hidePast = signal(true);
  protected readonly from = signal('');
  protected readonly to = signal('');
  protected readonly city = signal('');
  protected readonly country = signal('');
  protected readonly sort = signal<'SessionDateAsc' | 'Proximity'>('SessionDateAsc');
  protected readonly hasHomeCity = signal(false);

  protected readonly sortOptions = computed<SortOption[]>(() => {
    this.lang();
    const opts: SortOption[] = [{ value: 'SessionDateAsc', label: this.t.translate('browse.trainings.sortSoonest') }];
    if (this.hasHomeCity()) {
      opts.push({ value: 'Proximity', label: this.t.translate('browse.sortNearest') });
    }
    return opts;
  });

  protected readonly filtersOpen = signal(false);
  protected readonly pendingHidePast = signal(true);
  protected readonly pendingFrom = signal('');
  protected readonly pendingTo = signal('');
  protected readonly pendingCity = signal('');
  protected readonly pendingCountry = signal('');
  protected readonly pendingCount = signal<number | null>(null);

  protected readonly list = new BrowseList<TrainingCard>((skip, take) =>
    this.search.browseTrainings({ ...this.appliedParams(), skip, take }),
  );

  protected readonly activeFilterCount = computed(
    () =>
      (this.hidePast() ? 1 : 0) +
      (this.from() || this.to() ? 1 : 0) +
      (this.city().trim() ? 1 : 0) +
      (this.country().trim() ? 1 : 0),
  );

  protected readonly chips = computed<FilterChip[]>(() => {
    this.lang();
    const chips: FilterChip[] = [];
    if (this.hidePast()) {
      chips.push({ key: 'hidePast', label: this.t.translate('browse.trainings.chipUpcoming') });
    }
    if (this.from() || this.to()) {
      chips.push({ key: 'dates', label: this.dateRangeLabel() });
    }
    if (this.city().trim()) {
      chips.push({ key: 'city', label: this.city().trim() });
    }
    if (this.country().trim()) {
      chips.push({ key: 'country', label: this.country().trim() });
    }
    return chips;
  });

  ngOnInit(): void {
    this.reload();
    this.profiles.getMineCached().subscribe({
      next: (p) => this.hasHomeCity.set(p.location != null),
      error: () => this.hasHomeCity.set(false),
    });
  }

  ngOnDestroy(): void {
    this.list.destroy();
  }

  protected onQuery(q: string): void {
    this.query.set(q);
    this.reload();
  }

  protected openFilters(): void {
    this.pendingHidePast.set(this.hidePast());
    this.pendingFrom.set(this.from());
    this.pendingTo.set(this.to());
    this.pendingCity.set(this.city());
    this.pendingCountry.set(this.country());
    this.refreshPendingCount();
    this.filtersOpen.set(true);
  }

  protected applyFilters(): void {
    this.hidePast.set(this.pendingHidePast());
    this.from.set(this.pendingFrom());
    this.to.set(this.pendingTo());
    this.city.set(this.pendingCity());
    this.country.set(this.pendingCountry());
    this.filtersOpen.set(false);
    this.reload();
  }

  protected resetFilters(): void {
    this.pendingHidePast.set(true);
    this.pendingFrom.set('');
    this.pendingTo.set('');
    this.pendingCity.set('');
    this.pendingCountry.set('');
    this.refreshPendingCount();
  }

  /**
   * The Sort menu picked a new ordering — applies instantly.
   *
   * Changing the sort changes ONLY the sort. An earlier revision also imposed a two-week date
   * window here, on the theory that a nearby weekly series would otherwise fill the first page;
   * the owner rejected it (2026-08-04) because a sort control silently applying a filter is
   * surprising, and someone asking for the closest trainings wants the closest trainings. Date
   * range stays entirely the viewer's to set.
   */
  protected onSortChange(value: string): void {
    this.sort.set(value === 'Proximity' && this.hasHomeCity() ? 'Proximity' : 'SessionDateAsc');
    this.reload();
  }

  protected removeChip(key: string): void {
    if (key === 'hidePast') {
      this.hidePast.set(false);
    } else if (key === 'dates') {
      this.from.set('');
      this.to.set('');
    } else if (key === 'city') {
      this.city.set('');
    } else if (key === 'country') {
      this.country.set('');
    }
    this.reload();
  }

  protected clearAll(): void {
    this.query.set('');
    this.hidePast.set(true);
    this.from.set('');
    this.to.set('');
    this.city.set('');
    this.country.set('');
    this.sort.set('SessionDateAsc');
    this.reload();
  }

  protected setPendingHidePast(value: boolean): void {
    this.pendingHidePast.set(value);
    this.refreshPendingCount();
  }

  protected onPendingFrom(value: string): void {
    this.pendingFrom.set(value);
    this.refreshPendingCount();
  }

  protected onPendingTo(value: string): void {
    this.pendingTo.set(value);
    this.refreshPendingCount();
  }

  /** The city picker emits the whole option; the filter travels as its canonical name. */
  protected onPendingCity(option: CityOption | null): void {
    this.pendingCity.set(option?.name ?? '');
    this.refreshPendingCount();
  }

  protected onPendingCountry(value: string): void {
    this.pendingCountry.set(value);
    this.refreshPendingCount();
  }

  // ---- Row display helpers ------------------------------------------------------------------
  //
  // The row mirrors the home screen's agenda card: a dark date chip on the left, then the content.
  // `injectDateFormats` accepts a date-only `YYYY-MM-DD`, so the session date needs no local-midnight
  // conversion — see its doc comment.

  protected weekday(card: TrainingCard): string {
    return this.fmt.shortWeekday(card.sessionDate);
  }

  protected dayOfMonth(card: TrainingCard): string {
    return this.fmt.dayOfMonth(card.sessionDate);
  }

  protected month(card: TrainingCard): string {
    return this.fmt.shortMonth(card.sessionDate);
  }

  /** Accessible name for the date chip, which is otherwise three stacked fragments. */
  protected dateLabel(card: TrainingCard): string {
    return `${this.weekday(card)} ${this.dayOfMonth(card)} ${this.month(card)}`;
  }

  /** "19:00–21:00" from "19:00:00" — the seconds are never meaningful for a training. */
  protected timeRange(card: TrainingCard): string {
    return `${card.startTime.slice(0, 5)}–${card.endTime.slice(0, 5)}`;
  }

  private dateRangeLabel(): string {
    if (this.from() && this.to()) {
      return `${this.from()} – ${this.to()}`;
    }
    return this.from() ? `from ${this.from()}` : `until ${this.to()}`;
  }

  private appliedParams(): TrainingBrowseParams {
    return {
      q: this.query() || undefined,
      hidePast: this.hidePast(),
      from: this.from() || undefined,
      to: this.to() || undefined,
      city: this.city().trim() || undefined,
      country: this.country().trim() || undefined,
      sort: this.sort(),
    };
  }

  private reload(): void {
    this.list.filtered.set(
      Boolean(this.query().trim()) ||
        Boolean(this.from()) ||
        Boolean(this.to()) ||
        Boolean(this.city().trim()) ||
        Boolean(this.country().trim()),
    );
    this.list.reload();
  }

  private refreshPendingCount(): void {
    this.pendingCount.set(null);
    this.search
      .browseTrainings({
        q: this.query() || undefined,
        hidePast: this.pendingHidePast(),
        from: this.pendingFrom() || undefined,
        to: this.pendingTo() || undefined,
        city: this.pendingCity().trim() || undefined,
        country: this.pendingCountry().trim() || undefined,
        take: 0,
      })
      .subscribe({
        next: (page) => this.pendingCount.set(page.totalCount),
        error: () => this.pendingCount.set(null),
      });
  }
}
