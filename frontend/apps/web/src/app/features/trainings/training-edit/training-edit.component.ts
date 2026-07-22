import { Component, computed, inject, signal } from '@angular/core';
import { ButtonDirective, LoadingComponent, EmptyStateComponent } from '../../../shared/ui';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TrainingsService } from '../../../core/services/trainings.service';
import {
  EditSeriesRequest,
  EditSessionRequest,
  LocationKind,
  TrainingInterval,
  TrainingSessionDetail,
  TrainingVisibility,
} from '../../../core/models/trainings.models';
import { problemDetail } from '../../../core/utils/problem';

type EditMode = 'fork' | 'single' | 'series';

/**
 * The this-vs-series edit fork (feature 018). Editing a recurring session first asks the scope:
 * change just this session (which detaches it and keeps its own values) or the whole series (which
 * applies to every upcoming non-detached session and — when the weekday/interval/end-date change —
 * regenerates the future set). A one-off or an already-detached session skips the fork straight to the
 * single-session form. Admin-only; the API is the real guard.
 */
@Component({
  selector: 'jh-training-edit',
  imports: [FormsModule, ButtonDirective, LoadingComponent, EmptyStateComponent],
  templateUrl: './training-edit.component.html',
  styleUrl: './training-edit.component.css',
})
export class TrainingEditComponent {
  private readonly trainings = inject(TrainingsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly sessionId = this.route.snapshot.paramMap.get('id') ?? '';
  private readonly requestedScope = this.route.snapshot.queryParamMap.get('scope');

  protected readonly session = signal<TrainingSessionDetail | null>(null);
  protected readonly mode = signal<EditMode>('fork');
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly result = signal<string | null>(null);

  // Form state (prefilled on load).
  protected sessionDate = '';
  protected startTime = '';
  protected endTime = '';
  protected locationKind: LocationKind = 'InPerson';
  protected location = '';
  protected virtualLink = '';
  protected name = '';
  protected description = '';
  protected weekday = 'Tuesday';
  protected interval: TrainingInterval = 'Weekly';
  protected endDate = '';
  protected visibility: TrainingVisibility = 'TeamOnly';

  // Originals for the series form, so unchanged pattern fields aren't sent (avoids needless regeneration).
  private origWeekday = '';
  private origInterval: TrainingInterval = 'Weekly';
  private origEndDate = '';

  protected readonly weekdays = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

  protected readonly canEdit = computed(() => {
    const s = this.session();
    return !!s && !s.isPast && s.status === 'Scheduled';
  });

  constructor() {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.trainings.getSession(this.sessionId).subscribe({
      next: (s) => {
        this.session.set(s);
        this.prefill(s);
        this.mode.set(this.resolveMode(s));
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.notFound.set(true);
      },
    });
  }

  private resolveMode(s: TrainingSessionDetail): EditMode {
    if (this.requestedScope === 'series') return 'series';
    if (this.requestedScope === 'single') return 'single';
    if (s.isOneOff || s.isDetached) return 'single';
    return 'fork';
  }

  private prefill(s: TrainingSessionDetail): void {
    this.sessionDate = s.sessionDate;
    this.startTime = s.startTime.slice(0, 5);
    this.endTime = s.endTime.slice(0, 5);
    this.locationKind = s.locationKind;
    this.location = s.location ?? '';
    this.virtualLink = s.virtualLink ?? '';
    this.name = s.name;
    this.description = s.description ?? '';
    this.weekday = this.origWeekday = s.weekday ?? 'Tuesday';
    this.interval = this.origInterval = s.interval ?? 'Weekly';
    this.endDate = this.origEndDate = s.endDate ?? '';
    this.visibility = s.visibility;
  }

  protected choose(mode: EditMode): void {
    this.mode.set(mode);
  }

  protected shortDate(date: string): string {
    return new Date(`${date}T00:00:00`).toLocaleDateString(undefined, { weekday: 'short', day: 'numeric', month: 'short' });
  }

  protected cancel(): void {
    this.router.navigate(['/trainings/sessions', this.sessionId]);
  }

  protected saveSingle(): void {
    if (this.busy() || !this.canEdit()) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    const body: EditSessionRequest = {
      sessionDate: this.sessionDate,
      startTime: `${this.startTime}:00`,
      endTime: `${this.endTime}:00`,
      locationKind: this.locationKind,
      location: this.locationKind === 'InPerson' ? this.location.trim() : null,
      virtualLink: this.locationKind === 'Virtual' ? this.virtualLink.trim() : null,
    };
    this.trainings.editSession(this.sessionId, body).subscribe({
      next: () => this.router.navigate(['/trainings/sessions', this.sessionId]),
      error: (err) => {
        this.busy.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }

  protected saveSeries(): void {
    const s = this.session();
    if (this.busy() || !s) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    const body: EditSeriesRequest = {
      name: this.name.trim(),
      description: this.description.trim() || null,
      locationKind: this.locationKind,
      location: this.locationKind === 'InPerson' ? this.location.trim() : null,
      virtualLink: this.locationKind === 'Virtual' ? this.virtualLink.trim() : null,
      startTime: `${this.startTime}:00`,
      endTime: `${this.endTime}:00`,
      visibility: this.visibility,
    };
    // Only send pattern/end-date fields when they actually changed — those trigger regeneration.
    if (this.weekday !== this.origWeekday) body.weekday = this.weekday;
    if (this.interval !== this.origInterval) body.interval = this.interval;
    if (this.endDate && this.endDate !== this.origEndDate) body.endDate = this.endDate;

    this.trainings.editSeries(s.trainingId, body).subscribe({
      next: () => this.router.navigate(['/trainings/sessions', this.sessionId]),
      error: (err) => {
        this.busy.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }
}
