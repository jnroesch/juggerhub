import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { AlertComponent, ButtonDirective, EmptyStateComponent, LoadingComponent, RiseDirective, RiseScope } from '../../shared/ui';
import { RouterLink } from '@angular/router';
import { HomeService } from '../../core/services/home.service';
import { Home } from '../../core/models/home.models';
import { NeedsYouCardComponent } from './modules/needs-you-card.component';
import { UpNextCardComponent } from './modules/up-next-card.component';
import { NewsListComponent } from './modules/news-list.component';
import { ActivityListComponent } from './modules/activity-list.component';
import { TranslocoPipe } from '@jsverse/transloco';
import { PluralKeyPipe } from '../../core/i18n/plural-key.pipe';

/**
 * Home — the logged-in entry point (feature 008, reshaped by feature 025). Loads the composite
 * dashboard and renders a participation-and-action layout, top to bottom: Needs you (actionable),
 * Up next (unified events + trainings agenda with inline RSVP), News (authored team/event/party),
 * and What's going on (quiet passive activity). Players on no team get the warm find-a-team variant
 * plus open-to-everyone. A load failure shows a retry rather than blanking the page.
 */
@Component({
  selector: 'jh-dashboard',
  imports: [LoadingComponent, RouterLink, NeedsYouCardComponent, UpNextCardComponent, NewsListComponent, ActivityListComponent, ButtonDirective, EmptyStateComponent, TranslocoPipe, PluralKeyPipe, RiseDirective, AlertComponent],
  providers: [RiseScope],
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

  /**
   * Why a "Needs you" item just left without the viewer answering it (feature 058) — a translation
   * key, so a language switch re-renders it. Kept here rather than in the card, because the card
   * disappears with its last item and the explanation must not go with it.
   */
  protected readonly needsYouNotice = signal<string | null>(null);

  /** A "Needs you" item was resolved in place — refresh the composite so all sections reconcile. */
  protected onResolved(): void {
    this.needsYouNotice.set(null);
    this.refresh();
  }

  /**
   * A join request another admin answered first (or the player withdrew) — say so, and refresh so
   * the greeting's count of waiting things drops with the item (feature 058, FR-019).
   */
  protected onGone(): void {
    this.needsYouNotice.set('home.needsYouItem.noLongerWaiting');
    this.refresh();
  }

  /**
   * Accepting a team invite met our own limit on joining by invitation (feature 064). The item stays
   * and nothing changed, so there is nothing to refresh — only the note.
   */
  protected onLimited(): void {
    this.needsYouNotice.set('teams.inviteLimited');
  }

  private refresh(): void {
    this.home.getHome().subscribe({ next: (h) => this.data.set(h) });
  }
}
