import { Component, computed, inject, signal } from '@angular/core';
import { ButtonDirective, IconComponent, LoadingComponent, EmptyStateComponent } from '../../../shared/ui';
import { TranslocoPipe } from '@jsverse/transloco';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { TrainingsService } from '../../../core/services/trainings.service';
import { TrainingSeriesSummary, TrainingSessionRow } from '../../../core/models/trainings.models';
import { injectDateFormats } from '../../../core/i18n/locale-format';

/**
 * The team's Trainings tab (feature 018): upcoming sessions as a dated list — each with a Series/One-off
 * badge, the going count and the viewer's own inline RSVP. Admins additionally get "+ New training" and
 * the active-series overview. Members see a gentle "nothing scheduled" empty state; admins a prompt to
 * set one up. Non-members are bounced (the API 404s).
 */
@Component({
  selector: 'jh-trainings-tab',
  imports: [RouterLink, ButtonDirective, IconComponent, LoadingComponent, EmptyStateComponent, TranslocoPipe],
  templateUrl: './trainings-tab.component.html',
  styleUrl: './trainings-tab.component.css',
})
export class TrainingsTabComponent {
  private readonly trainings = inject(TrainingsService);
  private readonly route = inject(ActivatedRoute);
  private readonly fmt = injectDateFormats();

  protected readonly slug = signal('');
  protected readonly sessions = signal<TrainingSessionRow[]>([]);
  protected readonly series = signal<TrainingSeriesSummary[]>([]);
  protected readonly isAdmin = signal(false);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);

  protected readonly hasSessions = computed(() => this.sessions().length > 0);

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((pm) => {
      this.slug.set(pm.get('slug') ?? '');
      this.load();
    });
  }

  private load(): void {
    this.loading.set(true);
    this.notFound.set(false);
    // The series list is admin-only (403 for plain members) — probe it to learn the viewer's role.
    forkJoin({
      sessions: this.trainings.listSessions(this.slug(), 'upcoming', 0, 50),
      series: this.trainings.listSeries(this.slug()).pipe(catchError(() => of(null))),
    }).subscribe({
      next: ({ sessions, series }) => {
        this.sessions.set(sessions.items);
        this.isAdmin.set(series != null);
        this.series.set(series?.items ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.notFound.set(true);
      },
    });
  }

  protected time(t: string): string {
    return t.slice(0, 5);
  }

  // --- Dates. The shared helpers pin a date-only `sessionDate` to local midnight (it would
  // otherwise parse as UTC and render a day early) and follow the app language, not the browser's.
  // Date chip: weekday over day over month, each part on its own so the chip stays locale-safe. ---

  // Arrows rather than `= this.fmt.shortDate`: those would capture `fmt` at field-initialization
  // time, making these silently depend on declaration order.
  protected readonly shortDate = (date: string) => this.fmt.shortDate(date);
  protected readonly chipWeekday = (date: string) => this.fmt.shortWeekday(date);
  protected readonly chipDay = (date: string) => this.fmt.dayOfMonth(date);
  protected readonly chipMonth = (date: string) => this.fmt.shortMonth(date);

  protected answerLabel(a: TrainingSessionRow['myAnswer']): string {
    switch (a) {
      case 'Going':
        return 'trainings.tab.answerGoing';
      case 'Maybe':
        return 'trainings.tab.answerMaybe';
      case 'Cant':
        return 'trainings.tab.answerCant';
      default:
        return 'trainings.tab.answerRespond';
    }
  }
}
