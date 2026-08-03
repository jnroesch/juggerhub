import { Component, OnDestroy, OnInit, computed, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { SearchService } from '../../../core/services/search.service';
import { FilterChip, PlayerBrowseParams, PlayerCard } from '../../../core/models/search.models';
import { POMPFEN_CATALOG, Pompfe, pompfeLabel } from '../../../shared/pompfen.catalog';
import { BrowseList } from '../browse-list';
import { BrowseShellComponent } from '../browse-shell/browse-shell.component';
import { FilterPanelComponent } from '../filter-panel/filter-panel.component';
import { CountryPickerComponent } from '../../../shared/country-picker/country-picker.component';

/**
 * Players browse page (feature 007, US3). Same shell as Teams/Events; the player filter set is
 * position (derived from declared pompfen) and city. Only players who opted into search appear
 * (enforced server-side). Rows link to /u/:handle.
 */
@Component({
  selector: 'jh-browse-players',
  imports: [RouterLink, BrowseShellComponent, FilterPanelComponent, CountryPickerComponent, TranslocoPipe],
  templateUrl: './browse-players.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './browse-players.component.css',
})
export class BrowsePlayersComponent implements OnInit, OnDestroy {
  private readonly search = inject(SearchService);
  private readonly t = inject(TranslocoService);
  private readonly lang = toSignal(this.t.langChanges$, { initialValue: this.t.getActiveLang() });
  protected readonly catalog = POMPFEN_CATALOG;

  protected readonly query = signal('');
  protected readonly positions = signal<Pompfe[]>([]);
  protected readonly city = signal('');

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

  protected readonly countLabel = computed(() => {
    this.lang();
    const n = this.list.total();
    const parts = [
      this.t.translate(n === 1 ? 'browse.players.countOne' : 'browse.players.countMany', { count: n }),
    ];
    for (const p of this.positions()) {
      parts.push(this.label(p).toLowerCase());
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

  protected label(p: Pompfe): string {
    return pompfeLabel(p)?.en ?? p;
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
      sort: 'DisplayNameAsc',
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
