import { Component, computed, inject, signal } from '@angular/core';
import { ButtonDirective, CardComponent } from '../../../shared/ui';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TrainingsService } from '../../../core/services/trainings.service';
import { CreateTrainingRequest, LocationKind, TrainingInterval, TrainingVisibility } from '../../../core/models/trainings.models';
import { problemDetail } from '../../../core/utils/problem';

/**
 * The create-a-training wizard (feature 018): a calm one-decision-per-screen flow — series-or-one-off +
 * name, day/time/interval/end-date (collapses to a single date for a one-off), location + description,
 * team-only or public, then review and create. Admin-only; the API is the real guard.
 */
@Component({
  selector: 'jh-training-create',
  imports: [FormsModule, ButtonDirective, CardComponent],
  templateUrl: './training-create.component.html',
  styleUrl: './training-create.component.css',
})
export class TrainingCreateComponent {
  private readonly trainings = inject(TrainingsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly slug = this.route.snapshot.paramMap.get('slug') ?? '';
  protected readonly step = signal(1);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  // Form state.
  protected isRecurring = true;
  protected name = '';
  protected weekday = 'Tuesday';
  protected interval: TrainingInterval = 'Weekly';
  protected startTime = '19:00';
  protected endTime = '21:00';
  protected startDate = '';
  protected endDate = '';
  protected locationKind: LocationKind = 'InPerson';
  protected location = '';
  protected virtualLink = '';
  protected description = '';
  protected visibility: TrainingVisibility = 'TeamOnly';

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

  protected create(): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    const body: CreateTrainingRequest = {
      isRecurring: this.isRecurring,
      name: this.name.trim(),
      description: this.description.trim() || null,
      locationKind: this.locationKind,
      location: this.locationKind === 'InPerson' ? this.location.trim() : null,
      virtualLink: this.locationKind === 'Virtual' ? this.virtualLink.trim() : null,
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
        this.error.set(problemDetail(err));
        this.step.set(2); // schedule errors surface here
      },
    });
  }
}
