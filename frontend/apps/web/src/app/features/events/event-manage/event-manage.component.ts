import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { EventDetail, Signup } from '../../../core/models/event.models';
import { EventService } from '../../../core/services/event.service';
import { problemDetail } from '../../../core/utils/problem';

/**
 * US4 — participant administration: approve awaiting-approval sign-ups, promote from
 * the waiting list (manual, capacity-guarded), and remove anyone. Editing the event and
 * cancelling live on the separate settings page (/edit). Admin-only; server-enforced.
 */
@Component({
  selector: 'jh-event-manage',
  imports: [RouterLink],
  templateUrl: './event-manage.component.html',
  styleUrl: './event-manage.component.css',
})
export class EventManageComponent implements OnInit {
  private readonly events = inject(EventService);
  private readonly route = inject(ActivatedRoute);

  protected readonly detail = signal<EventDetail | null>(null);
  protected readonly joined = signal<Signup[]>([]);
  protected readonly awaiting = signal<Signup[]>([]);
  protected readonly waitlist = signal<Signup[]>([]);

  protected readonly loading = signal(true);
  protected readonly forbidden = signal(false);
  protected readonly acting = signal(false);
  protected readonly error = signal<string | null>(null);

  private id = '';

  protected readonly full = computed(() => {
    const d = this.detail();
    return !!d && d.occupiedSpots >= d.participationLimit;
  });

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.events.getEvent(this.id).subscribe({
      next: (d) => {
        if (!d.viewer.isAdmin) {
          this.forbidden.set(true);
          this.loading.set(false);
          return;
        }
        this.detail.set(d);
        this.loadGroups();
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.forbidden.set(true);
      },
    });
  }

  private loadGroups(): void {
    forkJoin({
      joined: this.events.getParticipants(this.id, 'joined'),
      awaiting: this.events.getParticipants(this.id, 'awaiting'),
      waitlist: this.events.getParticipants(this.id, 'waitlist'),
    }).subscribe((r) => {
      this.joined.set(r.joined.items);
      this.awaiting.set(r.awaiting.items);
      this.waitlist.set(r.waitlist.items);
    });
  }

  protected label(s: Signup): string {
    return s.teamName ?? s.userDisplayName ?? 'A participant';
  }

  protected approve(signupId: string): void {
    this.run(this.events.approve(this.id, signupId));
  }

  protected promote(signupId: string): void {
    this.run(this.events.promote(this.id, signupId));
  }

  protected remove(signupId: string): void {
    this.run(this.events.removeParticipant(this.id, signupId));
  }

  private run(obs: { subscribe: (o: { next: () => void; error: (e: unknown) => void }) => void }): void {
    if (this.acting()) {
      return;
    }
    this.acting.set(true);
    this.error.set(null);
    obs.subscribe({
      next: () => {
        this.acting.set(false);
        this.refresh();
      },
      error: (err) => {
        this.acting.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }

  private refresh(): void {
    this.events.getEvent(this.id).subscribe((d) => this.detail.set(d));
    this.loadGroups();
  }
}
