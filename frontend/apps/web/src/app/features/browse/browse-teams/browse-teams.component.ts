import { Component, OnDestroy, OnInit, computed, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { SearchService } from '../../../core/services/search.service';
import { ProfileService } from '../../../core/services/profile.service';
import { FilterChip, SortOption, TeamBrowseParams, TeamCard } from '../../../core/models/search.models';
import { BrowseList } from '../browse-list';
import { BrowseShellComponent } from '../browse-shell/browse-shell.component';
import { FilterPanelComponent } from '../filter-panel/filter-panel.component';
import { FilterToggleComponent } from '../filter-panel/filter-toggle.component';
import { CountryPickerComponent } from '../../../shared/country-picker/country-picker.component';

/**
 * Teams browse page (feature 007, US1). Composes the shared shell + filter panel with the
 * team filter set (active-only, beginners-welcome, country) and A–Z / Nearest-first sort.
 * Rows link to /t/:slug.
 */
@Component({
  selector: 'jh-browse-teams',
  imports: [RouterLink, BrowseShellComponent, FilterPanelComponent, FilterToggleComponent, CountryPickerComponent, TranslocoPipe],
  templateUrl: './browse-teams.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './browse-teams.component.css',
})
export class BrowseTeamsComponent implements OnInit, OnDestroy {
  private readonly search = inject(SearchService);
  private readonly profiles = inject(ProfileService);
  private readonly t = inject(TranslocoService);
  // Recompute translated labels (sort/chips/count) when the active language changes (feature 031).
  private readonly lang = toSignal(this.t.langChanges$, { initialValue: this.t.getActiveLang() });

  // Applied state (drives results).
  protected readonly query = signal('');
  protected readonly activeOnly = signal(true);
  protected readonly beginners = signal(false);
  protected readonly city = signal('');
  // Feature 030 — sort selection. "Proximity" (nearest first) is only offered once the player has a
  // home city (the server derives the anchor from their profile; without one it would 409), so the
  // option list is computed from hasHomeCity.
  protected readonly sort = signal<'NameAsc' | 'Proximity'>('NameAsc');
  protected readonly hasHomeCity = signal(false);

  protected readonly sortOptions = computed<SortOption[]>(() => {
    this.lang();
    const opts: SortOption[] = [{ value: 'NameAsc', label: this.t.translate('browse.sortNameAsc') }];
    if (this.hasHomeCity()) {
      opts.push({ value: 'Proximity', label: this.t.translate('browse.sortNearest') });
    }
    return opts;
  });

  // Pending state (edited in the panel until "Show N").
  protected readonly filtersOpen = signal(false);
  protected readonly pendingActiveOnly = signal(true);
  protected readonly pendingBeginners = signal(false);
  protected readonly pendingCity = signal('');
  protected readonly pendingCount = signal<number | null>(null);

  protected readonly list = new BrowseList<TeamCard>((skip, take) =>
    this.search.browseTeams({ ...this.appliedParams(), skip, take }),
  );

  protected readonly activeFilterCount = computed(
    () => (this.activeOnly() ? 1 : 0) + (this.beginners() ? 1 : 0) + (this.city().trim() ? 1 : 0),
  );

  protected readonly chips = computed<FilterChip[]>(() => {
    this.lang();
    const chips: FilterChip[] = [];
    if (this.activeOnly()) {
      chips.push({ key: 'active', label: this.t.translate('browse.teams.chipActive') });
    }
    if (this.beginners()) {
      chips.push({ key: 'beginners', label: this.t.translate('browse.teams.chipBeginners') });
    }
    if (this.city().trim()) {
      chips.push({ key: 'city', label: this.city().trim() });
    }
    // Sort is not a chip — it has its own Sort menu in the toolbar (feature 030).
    return chips;
  });

  protected readonly countLabel = computed(() => {
    this.lang();
    const n = this.list.total();
    const parts = [
      this.t.translate(n === 1 ? 'browse.teams.countOne' : 'browse.teams.countMany', { count: n }),
    ];
    if (this.activeOnly()) {
      parts.push(this.t.translate('browse.teams.filterActive'));
    }
    if (this.beginners()) {
      parts.push(this.t.translate('browse.teams.filterBeginnersWelcome'));
    }
    if (this.city().trim()) {
      parts.push(this.city().trim());
    }
    return parts.join(' · ');
  });

  ngOnInit(): void {
    this.reload();
    // Offer "Near me" only when the player has a home city to measure from.
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
    this.pendingActiveOnly.set(this.activeOnly());
    this.pendingBeginners.set(this.beginners());
    this.pendingCity.set(this.city());
    this.refreshPendingCount();
    this.filtersOpen.set(true);
  }

  protected applyFilters(): void {
    this.activeOnly.set(this.pendingActiveOnly());
    this.beginners.set(this.pendingBeginners());
    this.city.set(this.pendingCity());
    this.filtersOpen.set(false);
    this.reload();
  }

  protected resetFilters(): void {
    this.pendingActiveOnly.set(true);
    this.pendingBeginners.set(false);
    this.pendingCity.set('');
    this.refreshPendingCount();
  }

  /** The Sort menu picked a new ordering — applies instantly. */
  protected onSortChange(value: string): void {
    this.sort.set(value === 'Proximity' && this.hasHomeCity() ? 'Proximity' : 'NameAsc');
    this.reload();
  }

  protected removeChip(key: string): void {
    if (key === 'active') {
      this.activeOnly.set(false);
    } else if (key === 'beginners') {
      this.beginners.set(false);
    } else if (key === 'city') {
      this.city.set('');
    }
    this.reload();
  }

  protected clearAll(): void {
    this.query.set('');
    this.activeOnly.set(true);
    this.beginners.set(false);
    this.city.set('');
    this.sort.set('NameAsc');
    this.reload();
  }

  protected onPendingCity(value: string): void {
    this.pendingCity.set(value);
    this.refreshPendingCount();
  }

  protected setPendingActive(value: boolean): void {
    this.pendingActiveOnly.set(value);
    this.refreshPendingCount();
  }

  protected setPendingBeginners(value: boolean): void {
    this.pendingBeginners.set(value);
    this.refreshPendingCount();
  }

  private appliedParams(): TeamBrowseParams {
    return {
      q: this.query() || undefined,
      activeOnly: this.activeOnly(),
      beginnersWelcome: this.beginners() || undefined,
      country: this.city().trim() || undefined,
      sort: this.sort(),
    };
  }

  private reload(): void {
    this.list.filtered.set(Boolean(this.query().trim()) || this.beginners() || Boolean(this.city().trim()));
    this.list.reload();
  }

  private refreshPendingCount(): void {
    this.pendingCount.set(null);
    this.search
      .browseTeams({
        q: this.query() || undefined,
        activeOnly: this.pendingActiveOnly(),
        beginnersWelcome: this.pendingBeginners() || undefined,
        country: this.pendingCity().trim() || undefined,
        take: 0,
      })
      .subscribe({
        next: (page) => this.pendingCount.set(page.totalCount),
        error: () => this.pendingCount.set(null),
      });
  }
}
