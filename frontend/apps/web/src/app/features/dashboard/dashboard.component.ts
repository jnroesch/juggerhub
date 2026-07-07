import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HomeService } from '../../core/services/home.service';
import { Home } from '../../core/models/home.models';
import { UpNextCardComponent } from './modules/up-next-card.component';
import { NewsListComponent } from './modules/news-list.component';
import { relativeTime, shortDate } from '../../core/utils/format';

/**
 * Home — the logged-in entry point (feature 008). Loads the composite dashboard and renders an
 * agenda-led layout: Up next (with one-tap RSVP), Your teams activity, News, and Tournaments,
 * plus a desktop right rail. Players on no team get the warm find-a-team variant. Each module
 * owns loading/empty states; a load failure shows a retry rather than blanking the page.
 */
@Component({
  selector: 'jh-dashboard',
  imports: [RouterLink, UpNextCardComponent, NewsListComponent],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css',
})
export class DashboardComponent implements OnInit {
  private readonly home = inject(HomeService);

  protected readonly data = signal<Home | null>(null);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);

  protected readonly hasTeam = computed(() => (this.data()?.teams.length ?? 0) > 0);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.home.getHome().subscribe({
      next: (h) => {
        this.data.set(h);
        this.loading.set(false);
      },
      error: () => {
        this.failed.set(true);
        this.loading.set(false);
      },
    });
  }

  protected rel(iso: string): string {
    return relativeTime(iso);
  }

  protected fixtureDate(iso: string): string {
    return shortDate(iso);
  }
}
