import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { EventContact, EventDetail, EventNews, Signup } from '../../../core/models/event.models';
import { EventService } from '../../../core/services/event.service';
import { problemDetail } from '../../../core/utils/problem';

/**
 * US2/US3/US5 — the public event page. Anyone can read the details, the three
 * participant groups, news, and contacts; a signed-in visitor can sign up / join the
 * waiting list / withdraw; an admin gets an in-page toolkit (manage, contacts,
 * co-admins, edit) and can post news. A cancelled event stays readable, marked
 * cancelled, with no sign-up actions. Authorization is enforced server-side.
 */
@Component({
  selector: 'jh-event-detail',
  imports: [RouterLink, DatePipe, FormsModule],
  templateUrl: './event-detail.component.html',
  styleUrl: './event-detail.component.css',
})
export class EventDetailComponent implements OnInit {
  private readonly events = inject(EventService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly detail = signal<EventDetail | null>(null);
  protected readonly joined = signal<Signup[]>([]);
  protected readonly awaiting = signal<Signup[]>([]);
  protected readonly waitlist = signal<Signup[]>([]);
  protected readonly news = signal<EventNews[]>([]);
  protected readonly contacts = signal<EventContact[]>([]);

  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly acting = signal(false);
  protected readonly actionError = signal<string | null>(null);

  protected readonly selectedTeamId = signal<string>('');
  protected readonly newsBody = signal('');

  private id = '';

  protected readonly cancelled = computed(() => this.detail()?.status === 'Cancelled');
  protected readonly canJoin = computed(() => {
    const d = this.detail();
    return !!d && d.status === 'Published' && d.viewer.isAuthenticated && !d.viewer.isAdmin && d.viewer.mySignupStatus === null;
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
        this.selectedTeamId.set(d.viewer.teamsICanEnter[0]?.teamId ?? '');
        this.loadLists();
        this.loading.set(false);
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

  protected participantLabel(s: Signup): string {
    return s.teamName ?? s.userDisplayName ?? 'A participant';
  }

  protected spotsLabel(d: EventDetail): string {
    return this.events.spotsLabel(d);
  }

  protected join(): void {
    const d = this.detail();
    if (!d || this.acting()) {
      return;
    }
    const teamId = d.participantMode === 'Teams' ? this.selectedTeamId() : null;
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
    const d = this.detail();
    if (!d?.viewer.mySignupId || this.acting()) {
      return;
    }
    this.acting.set(true);
    this.events.withdraw(this.id, d.viewer.mySignupId).subscribe({
      next: () => this.reload(),
      error: (err) => {
        this.acting.set(false);
        this.actionError.set(problemDetail(err));
      },
    });
  }

  protected postNews(): void {
    const body = this.newsBody().trim();
    if (!body || this.acting()) {
      return;
    }
    this.acting.set(true);
    this.events.postNews(this.id, body).subscribe({
      next: (post) => {
        this.news.update((n) => [post, ...n]);
        this.newsBody.set('');
        this.acting.set(false);
      },
      error: (err) => {
        this.acting.set(false);
        this.actionError.set(problemDetail(err));
      },
    });
  }

  private reload(): void {
    this.acting.set(false);
    this.actionError.set(null);
    this.load();
  }

  protected openProfile(handle: string | null): void {
    if (handle) {
      this.router.navigate(['/u', handle]);
    }
  }
}
