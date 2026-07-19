import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TrainingsService } from '../../../core/services/trainings.service';
import { TrainingRsvp, TrainingSessionDetail } from '../../../core/models/trainings.models';
import { problemDetail } from '../../../core/utils/problem';

/**
 * A single training session page (feature 018). The three-way Going/Maybe/Can't answer sits front and
 * centre for members and for outsiders on a public session (recorded as a guest); below it, who's coming
 * grouped by answer. Team admins get an inline manage menu (make public/team-only, skip, cancel, and a
 * link to full attendance). Team-only sessions are 404 to outsiders — enforced server-side.
 */
@Component({
  selector: 'jh-training-session',
  imports: [RouterLink],
  templateUrl: './training-session.component.html',
  styleUrl: './training-session.component.css',
})
export class TrainingSessionComponent {
  private readonly trainings = inject(TrainingsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly sessionId = signal('');
  protected readonly session = signal<TrainingSessionDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly menuOpen = signal(false);

  protected readonly canRespond = computed(() => {
    const s = this.session();
    return !!s && !s.isPast && s.status === 'Scheduled';
  });
  protected readonly isCancelled = computed(() => this.session()?.status === 'Cancelled');

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((pm) => {
      this.sessionId.set(pm.get('id') ?? '');
      this.load();
    });
  }

  private load(): void {
    this.loading.set(true);
    this.notFound.set(false);
    this.trainings.getSession(this.sessionId()).subscribe({
      next: (s) => {
        this.session.set(s);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.notFound.set(true);
      },
    });
  }

  protected respond(answer: TrainingRsvp): void {
    if (this.busy() || !this.canRespond()) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    this.trainings.respond(this.sessionId(), answer).subscribe({
      next: () => {
        this.busy.set(false);
        this.load(); // refresh counts + who's-coming
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }

  protected toggleVisibility(): void {
    const s = this.session();
    if (!s || this.busy()) {
      return;
    }
    this.busy.set(true);
    this.menuOpen.set(false);
    const next = s.visibility === 'Public' ? 'TeamOnly' : 'Public';
    this.trainings.setSessionVisibility(this.sessionId(), next).subscribe({
      next: () => {
        this.busy.set(false);
        this.load();
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }

  protected skip(): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.menuOpen.set(false);
    this.trainings.skip(this.sessionId()).subscribe({
      next: () => {
        const slug = this.session()?.teamSlug;
        this.router.navigate(['/t', slug]);
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }

  protected cancel(): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.menuOpen.set(false);
    this.trainings.cancel(this.sessionId()).subscribe({
      next: () => {
        this.busy.set(false);
        this.load();
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }

  protected copyLink(): void {
    const url = `${location.origin}/trainings/sessions/${this.sessionId()}`;
    void navigator.clipboard?.writeText(url);
    this.menuOpen.set(false);
  }

  protected avatarUrl(handle: string): string {
    return `/api/v1/profiles/${encodeURIComponent(handle)}/avatar`;
  }

  protected time(t: string): string {
    return t.slice(0, 5);
  }

  protected shortDate(date: string): string {
    return new Date(`${date}T00:00:00`).toLocaleDateString(undefined, { weekday: 'short', day: 'numeric', month: 'short' });
  }
}
