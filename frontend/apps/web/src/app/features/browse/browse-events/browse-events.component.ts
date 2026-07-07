import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { SearchService } from '../../../core/services/search.service';
import { EventBrowseParams, EventCard, EventType, FilterChip } from '../../../core/models/search.models';
import { BrowseList } from '../browse-list';
import { BrowseShellComponent } from '../browse-shell/browse-shell.component';
import { FilterPanelComponent } from '../filter-panel/filter-panel.component';
import { FilterToggleComponent } from '../filter-panel/filter-toggle.component';

const EVENT_TYPES: readonly EventType[] = ['Tournament', 'Workshop', 'Other'];

/**
 * Events browse page (feature 007, US2). Same shell as Teams; the event filter set is
 * hide-past (default on), a date range, event type, and city, sorted soonest-first. Cancelled
 * events are excluded server-side. Rows link to /events/:id.
 */
@Component({
  selector: 'jh-browse-events',
  imports: [RouterLink, DatePipe, BrowseShellComponent, FilterPanelComponent, FilterToggleComponent],
  templateUrl: './browse-events.component.html',
  styleUrl: './browse-events.component.css',
})
export class BrowseEventsComponent implements OnInit, OnDestroy {
  private readonly search = inject(SearchService);
  protected readonly eventTypes = EVENT_TYPES;

  protected readonly query = signal('');
  protected readonly hidePast = signal(true);
  protected readonly from = signal('');
  protected readonly to = signal('');
  protected readonly type = signal<EventType | ''>('');
  protected readonly city = signal('');

  protected readonly filtersOpen = signal(false);
  protected readonly pendingHidePast = signal(true);
  protected readonly pendingFrom = signal('');
  protected readonly pendingTo = signal('');
  protected readonly pendingType = signal<EventType | ''>('');
  protected readonly pendingCity = signal('');
  protected readonly pendingCount = signal<number | null>(null);

  protected readonly list = new BrowseList<EventCard>((skip, take) =>
    this.search.browseEvents({ ...this.appliedParams(), skip, take }),
  );

  protected readonly activeFilterCount = computed(
    () =>
      (this.hidePast() ? 1 : 0) +
      (this.from() || this.to() ? 1 : 0) +
      (this.type() ? 1 : 0) +
      (this.city().trim() ? 1 : 0),
  );

  protected readonly chips = computed<FilterChip[]>(() => {
    const chips: FilterChip[] = [];
    if (this.hidePast()) {
      chips.push({ key: 'hidePast', label: 'Upcoming' });
    }
    if (this.from() || this.to()) {
      chips.push({ key: 'dates', label: this.dateRangeLabel() });
    }
    if (this.type()) {
      chips.push({ key: 'type', label: this.type() });
    }
    if (this.city().trim()) {
      chips.push({ key: 'city', label: this.city().trim() });
    }
    return chips;
  });

  protected readonly countLabel = computed(() => {
    const n = this.list.total();
    const parts = [`${n} ${n === 1 ? 'event' : 'events'}`];
    if (this.hidePast()) {
      parts.push('upcoming');
    }
    if (this.type()) {
      parts.push(this.type().toLowerCase());
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
    this.pendingHidePast.set(this.hidePast());
    this.pendingFrom.set(this.from());
    this.pendingTo.set(this.to());
    this.pendingType.set(this.type());
    this.pendingCity.set(this.city());
    this.refreshPendingCount();
    this.filtersOpen.set(true);
  }

  protected applyFilters(): void {
    this.hidePast.set(this.pendingHidePast());
    this.from.set(this.pendingFrom());
    this.to.set(this.pendingTo());
    this.type.set(this.pendingType());
    this.city.set(this.pendingCity());
    this.filtersOpen.set(false);
    this.reload();
  }

  protected resetFilters(): void {
    this.pendingHidePast.set(true);
    this.pendingFrom.set('');
    this.pendingTo.set('');
    this.pendingType.set('');
    this.pendingCity.set('');
    this.refreshPendingCount();
  }

  protected removeChip(key: string): void {
    if (key === 'hidePast') {
      this.hidePast.set(false);
    } else if (key === 'dates') {
      this.from.set('');
      this.to.set('');
    } else if (key === 'type') {
      this.type.set('');
    } else if (key === 'city') {
      this.city.set('');
    }
    this.reload();
  }

  protected clearAll(): void {
    this.query.set('');
    this.hidePast.set(true);
    this.from.set('');
    this.to.set('');
    this.type.set('');
    this.city.set('');
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

  protected onPendingType(value: string): void {
    this.pendingType.set((value as EventType) || '');
    this.refreshPendingCount();
  }

  protected onPendingCity(value: string): void {
    this.pendingCity.set(value);
    this.refreshPendingCount();
  }

  private dateRangeLabel(): string {
    if (this.from() && this.to()) {
      return `${this.from()} – ${this.to()}`;
    }
    return this.from() ? `from ${this.from()}` : `until ${this.to()}`;
  }

  private appliedParams(): EventBrowseParams {
    return {
      q: this.query() || undefined,
      hidePast: this.hidePast(),
      from: this.from() || undefined,
      to: this.to() || undefined,
      type: this.type() || undefined,
      city: this.city().trim() || undefined,
      sort: 'StartsAtAsc',
    };
  }

  private reload(): void {
    this.list.filtered.set(
      Boolean(this.query().trim()) || Boolean(this.from()) || Boolean(this.to()) || Boolean(this.type()) || Boolean(this.city().trim()),
    );
    this.list.reload();
  }

  private refreshPendingCount(): void {
    this.pendingCount.set(null);
    this.search
      .browseEvents({
        q: this.query() || undefined,
        hidePast: this.pendingHidePast(),
        from: this.pendingFrom() || undefined,
        to: this.pendingTo() || undefined,
        type: this.pendingType() || undefined,
        city: this.pendingCity().trim() || undefined,
        take: 0,
      })
      .subscribe({
        next: (page) => this.pendingCount.set(page.totalCount),
        error: () => this.pendingCount.set(null),
      });
  }
}
