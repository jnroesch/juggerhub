import { TranslocoDatePipe } from '@jsverse/transloco-locale';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { CardComponent, LoadingComponent } from '../../../shared/ui';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { EventContact, EventDetail, EventNews, Signup } from '../../../core/models/event.models';
import { PartyContext } from '../../../core/models/party.models';
import { EventService } from '../../../core/services/event.service';
import { BrowseReturnService, RESTORE_BROWSE_SEARCH } from '../../../core/services/browse-return.service';
import { PartyService } from '../../../core/services/party.service';
import { problemDetail } from '../../../core/utils/problem';
import { EventContactsListComponent } from './components/contacts-list.component';
import { EventJoinActionsComponent } from './components/join-actions.component';
import { EventNewsFeedComponent } from './components/news-feed.component';
import { EventParticipantGroupsComponent } from './components/participant-groups.component';
import { EventResultsComponent } from './components/event-results.component';
import { MarketBoardComponent } from '../../marketplace/market-board/market-board.component';

/**
 * US2/US3/US5 — the public event page. Anyone can read the details, the three
 * participant groups, news, and contacts; a signed-in visitor can sign up / join the
 * waiting list / withdraw; an admin gets an in-page toolkit (manage, contacts,
 * co-admins) and can post news. A cancelled event stays readable, marked cancelled,
 * with no sign-up actions. Orchestrates the participant-groups / news-feed /
 * contacts-list / join-actions child components; authorization is enforced server-side.
 */
@Component({
  selector: 'jh-event-detail',
  imports: [
    CardComponent,
    RouterLink,
    TranslocoDatePipe,
    EventParticipantGroupsComponent,
    EventNewsFeedComponent,
    EventContactsListComponent,
    EventJoinActionsComponent,
    EventResultsComponent,
    MarketBoardComponent,
    LoadingComponent,
    TranslocoPipe,
  ],
  templateUrl: './event-detail.component.html',
  styleUrl: './event-detail.component.css',
})
export class EventDetailComponent implements OnInit {
  private readonly events = inject(EventService);
  private readonly parties = inject(PartyService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly browseReturns = inject(BrowseReturnService);

  /**
   * "‹ Events" leads to the events list — the event's parent, for every viewer, since every event
   * that is not cancelled is listed there (GH #279). It used to lead home whichever list the viewer
   * came from. Reopens the list as they left it; see {@link BrowseReturnService} for why this is
   * a parent link and not `Location.back()`.
   */
  protected readonly eventsQuery = computed(() => this.browseReturns.queryParams('/browse/events'));
  protected readonly restoreSearch = RESTORE_BROWSE_SEARCH;

  protected readonly detail = signal<EventDetail | null>(null);
  protected readonly joined = signal<Signup[]>([]);
  protected readonly awaiting = signal<Signup[]>([]);
  protected readonly waitlist = signal<Signup[]>([]);
  protected readonly news = signal<EventNews[]>([]);
  protected readonly contacts = signal<EventContact[]>([]);
  /** Feature 016: the caller's party affordances for a teams-only event (cards + form button). */
  protected readonly partyContext = signal<PartyContext | null>(null);

  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly acting = signal(false);
  protected readonly actionError = signal<string | null>(null);

  protected readonly menuOpen = signal(false);

  private id = '';

  protected readonly cancelled = computed(() => this.detail()?.status === 'Cancelled');
  /**
   * The event is over. The mercenary board is closed server-side from here on, so it is not shown —
   * a past-dated tournament (feature 050, FR-023) opens no board.
   */
  protected readonly ended = computed(() => {
    const d = this.detail();
    return !!d && new Date(d.endsAt).getTime() < Date.now();
  });
  /** Feature 027: a signed-in non-admin may contact the event's admins, unless the event is cancelled. */
  protected readonly canContactAdmins = computed(() => {
    const d = this.detail();
    return !!d && d.viewer.isAuthenticated && !d.viewer.isAdmin && d.status !== 'Cancelled';
  });

  /** Open a "contact the admins" thread for this event (feature 027). Nothing persists until first send. */
  protected contactAdmins(): void {
    const d = this.detail();
    if (!d) {
      return;
    }
    void this.router.navigate(['/chat', 'contact', 'event', this.id], { state: { name: d.name } });
  }
  /** Occupied-spots fill for the occupancy bar (0–100). */
  protected readonly occupancyPct = computed(() => {
    const d = this.detail();
    return d && d.participationLimit > 0 ? Math.min(100, Math.round((d.occupiedSpots / d.participationLimit) * 100)) : 0;
  });

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.loadError.set(false);
    this.events.getEvent(this.id).subscribe({
      next: (d) => {
        this.detail.set(d);
        this.loadLists();
        this.loading.set(false);
        this.partyContext.set(null);
        if (d.participantMode === 'Teams' && d.viewer.isAuthenticated) {
          this.parties.getPartyContext(this.id).subscribe({ next: (ctx) => this.partyContext.set(ctx) });
        }
      },
      error: () => {
        this.loading.set(false);
        this.loadError.set(true);
      },
    });
  }

  private loadLists(): void {
    forkJoin({
      joined: this.events.getParticipants(this.id, 'joined'),
      awaiting: this.events.getParticipants(this.id, 'awaiting'),
      waitlist: this.events.getParticipants(this.id, 'waitlist'),
      news: this.events.getNews(this.id),
      contacts: this.events.getContacts(this.id),
    }).subscribe((r) => {
      this.joined.set(r.joined.items);
      this.awaiting.set(r.awaiting.items);
      this.waitlist.set(r.waitlist.items);
      this.news.set(r.news.items);
      this.contacts.set(r.contacts.items);
    });
  }

  protected join(teamId: string | null): void {
    if (this.acting()) {
      return;
    }
    this.acting.set(true);
    this.actionError.set(null);
    this.events.signup(this.id, teamId).subscribe({
      next: () => this.reload(),
      error: (err) => {
        this.acting.set(false);
        this.actionError.set(problemDetail(err));
      },
    });
  }

  protected withdraw(): void {
    const signupId = this.detail()?.viewer.mySignupId;
    if (!signupId || this.acting()) {
      return;
    }
    this.acting.set(true);
    this.events.withdraw(this.id, signupId).subscribe({
      next: () => this.reload(),
      error: (err) => {
        this.acting.set(false);
        this.actionError.set(problemDetail(err));
      },
    });
  }

  protected postNews(body: string): void {
    if (!body.trim() || this.acting()) {
      return;
    }
    this.acting.set(true);
    this.events.postNews(this.id, body).subscribe({
      next: (post) => {
        this.news.update((n) => [post, ...n]);
        this.acting.set(false);
      },
      error: (err) => {
        this.acting.set(false);
        this.actionError.set(problemDetail(err));
      },
    });
  }

  protected openProfile(handle: string): void {
    this.router.navigate(['/u', handle]);
  }

  private reload(): void {
    this.acting.set(false);
    this.actionError.set(null);
    this.load();
  }
}
