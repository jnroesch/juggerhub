import { Component, computed, inject, signal } from '@angular/core';
import { ButtonDirective, CardComponent } from '../../../shared/ui';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TrainingsService } from '../../../core/services/trainings.service';
import { CreateTrainingRequest, LocationKind, TrainingInterval, TrainingVisibility } from '../../../core/models/trainings.models';
import { CityOption, toSelection } from '../../../core/models/city.models';
import { AddressFieldsComponent } from '../../../shared/address-fields/address-fields.component';
import { problemDetail } from '../../../core/utils/problem';

/**
 * The create-a-training wizard (feature 018): a calm one-decision-per-screen flow — series-or-one-off +
 * name, day/time/interval/end-date (collapses to a single date for a one-off), location + description,
 * team-only or public, then review and create. Admin-only; the API is the real guard.
 */
@Component({
  selector: 'jh-training-create',
  imports: [FormsModule, ButtonDirective, CardComponent, AddressFieldsComponent, TranslocoPipe],
  templateUrl: './training-create.component.html',
  styleUrl: './training-create.component.css',
})
export class TrainingCreateComponent {
  private readonly trainings = inject(TrainingsService);
  private readonly transloco = inject(TranslocoService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly slug = this.route.snapshot.paramMap.get('slug') ?? '';
  protected readonly step = signal(1);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  // Form state.
  protected isRecurring = true;
  protected name = '';
  // Default to today's weekday so the most common case (an admin scheduling a training for the day
  // they're setting it up) needs no change. Uses the same Monday-first labels as `weekdays` below;
  // getDay() is Sunday-indexed, hence the Sunday-first lookup array.
  protected weekday = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'][new Date().getDay()];
  protected interval: TrainingInterval = 'Weekly';
  protected startTime = '19:00';
  protected endTime = '21:00';
  protected startDate = '';
  protected endDate = '';
  // SIGNALS, not plain fields: `whereComplete` is a computed() over them, and the app is zoneless —
  // a computed() over plain properties never recomputes, leaving Continue permanently disabled. This
  // caught `locationKind` and `virtualLink` (the Virtual branch): typing a join link never re-ran the
  // computed, so Continue stayed locked for virtual trainings.
  protected readonly locationKind = signal<LocationKind>('InPerson');
  // Feature 042 — structured address; only meaningful when in-person.
  protected readonly venueName = signal('');
  protected readonly street = signal('');
  protected readonly postalCode = signal('');
  protected readonly selectedCity = signal<CityOption | null>(null);
  protected readonly virtualLink = signal('');
  protected description = '';
  protected visibility: TrainingVisibility = 'TeamOnly';

  /**
   * The "Where" step is only complete when the whole in-person address is there: a street, a
   * postal code AND a resolved city. A venue name alone is not an address. The server enforces
   * the same rule — this is guidance, never the boundary (constitution Principle I).
   */
  protected readonly whereComplete = computed(() =>
    this.locationKind() === 'InPerson'
      ? !!this.street().trim() && !!this.postalCode().trim() && this.selectedCity() !== null
      : !!this.virtualLink().trim(),
  );

  protected readonly weekdays = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

  protected readonly summaryCount = computed(() => {
    if (!this.isRecurring) return 1;
    if (!this.startDate || !this.endDate) return null;
    // Rough client-side estimate for the review copy (the server is authoritative).
    const start = new Date(this.startDate);
    const end = new Date(this.endDate);
    if (end < start) return 0;
    const days = Math.floor((end.getTime() - start.getTime()) / 86400000);
    const per = this.interval === 'Weekly' ? 7 : this.interval === 'BiWeekly' ? 14 : 30;
    return Math.max(1, Math.floor(days / per) + 1);
  });

  protected next(): void {
    this.error.set(null);
    this.step.update((s) => Math.min(s + 1, 5));
  }

  protected back(): void {
    this.error.set(null);
    this.step.update((s) => Math.max(s - 1, 1));
  }

  protected cancel(): void {
    this.router.navigate(['/t', this.slug, 'trainings']);
  }

  protected onCitySelected(option: CityOption | null): void {
    this.selectedCity.set(option);
  }

  /** Review-step display for the picked city; a neutral dash rather than a blank when unset. */
  protected cityLabel(): string {
    return this.selectedCity()?.label ?? '—';
  }

  protected create(): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    const inPerson = this.locationKind() === 'InPerson';
    const body: CreateTrainingRequest = {
      isRecurring: this.isRecurring,
      name: this.name.trim(),
      description: this.description.trim() || null,
      locationKind: this.locationKind(),
      venueName: inPerson ? this.venueName().trim() || null : null,
      street: inPerson ? this.street().trim() || null : null,
      postalCode: inPerson ? this.postalCode().trim() || null : null,
      location: inPerson ? toSelection(this.selectedCity()) : null,
      virtualLink: inPerson ? null : this.virtualLink().trim(),
      weekday: this.isRecurring ? this.weekday : null,
      interval: this.isRecurring ? this.interval : null,
      startTime: `${this.startTime}:00`,
      endTime: `${this.endTime}:00`,
      startDate: this.startDate,
      endDate: this.isRecurring ? this.endDate : null,
      visibility: this.visibility,
    };
    this.trainings.create(this.slug, body).subscribe({
      next: (created) => {
        this.router.navigate(['/trainings/sessions', created.firstSessionId]);
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(problemDetail(err, this.transloco.translate('trainings.genericError')));
        this.step.set(2); // schedule errors surface here
      },
    });
  }
}
