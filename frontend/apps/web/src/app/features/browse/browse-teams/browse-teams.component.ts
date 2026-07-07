import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SearchService } from '../../../core/services/search.service';
import { FilterChip, TeamBrowseParams, TeamCard } from '../../../core/models/search.models';
import { BrowseList } from '../browse-list';
import { BrowseShellComponent } from '../browse-shell/browse-shell.component';
import { FilterPanelComponent } from '../filter-panel/filter-panel.component';
import { FilterToggleComponent } from '../filter-panel/filter-toggle.component';

/**
 * Teams browse page (feature 007, US1). Composes the shared shell + filter panel with the
 * team filter set (active-only, beginners-welcome, city) and A–Z sort. Rows link to /t/:slug.
 */
@Component({
  selector: 'jh-browse-teams',
  imports: [RouterLink, BrowseShellComponent, FilterPanelComponent, FilterToggleComponent],
  templateUrl: './browse-teams.component.html',
  styleUrl: './browse-teams.component.css',
})
export class BrowseTeamsComponent implements OnInit, OnDestroy {
  private readonly search = inject(SearchService);

  // Applied state (drives results).
  protected readonly query = signal('');
  protected readonly activeOnly = signal(true);
  protected readonly beginners = signal(false);
  protected readonly city = signal('');

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
    const chips: FilterChip[] = [];
    if (this.activeOnly()) {
      chips.push({ key: 'active', label: 'Active' });
    }
    if (this.beginners()) {
      chips.push({ key: 'beginners', label: 'Beginners' });
    }
    if (this.city().trim()) {
      chips.push({ key: 'city', label: this.city().trim() });
    }
    return chips;
  });

  protected readonly countLabel = computed(() => {
    const n = this.list.total();
    const parts = [`${n} ${n === 1 ? 'team' : 'teams'}`];
    if (this.activeOnly()) {
      parts.push('active');
    }
    if (this.beginners()) {
      parts.push('beginners welcome');
    }
    if (this.city().trim()) {
      parts.push(this.city().trim());
    }
    return parts.join(' · ');
  });

  ngOnInit(): void {
    this.reload();
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
      city: this.city().trim() || undefined,
      sort: 'NameAsc',
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
        city: this.pendingCity().trim() || undefined,
        take: 0,
      })
      .subscribe({
        next: (page) => this.pendingCount.set(page.totalCount),
        error: () => this.pendingCount.set(null),
      });
  }
}
