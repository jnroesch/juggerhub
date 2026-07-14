import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { EventContact, EventDetail, EventNews, Signup } from '../../../core/models/event.models';
import { PartyContext } from '../../../core/models/party.models';
import { EventService } from '../../../core/services/event.service';
import { PartyService } from '../../../core/services/party.service';
import { problemDetail } from '../../../core/utils/problem';
import { EventContactsListComponent } from './components/contacts-list.component';
import { EventJoinActionsComponent } from './components/join-actions.component';
import { EventNewsFeedComponent } from './components/news-feed.component';
import { EventParticipantGroupsComponent } from './components/participant-groups.component';

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
    RouterLink,
    DatePipe,
    EventParticipantGroupsComponent,
    EventNewsFeedComponent,
    EventContactsListComponent,
    EventJoinActionsComponent,
  ],
  templateUrl: './event-detail.component.html',
  styleUrl: './event-detail.component.css',
})
export class EventDetailComponent implements OnInit {
  private readonly events = inject(EventService);
  private readonly parties = inject(PartyService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

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
