import { Component, computed, inject, signal } from '@angular/core';
import { ButtonDirective, LoadingComponent, EmptyStateComponent } from '../../../shared/ui';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
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
import { CityOption, Location, toSelection } from '../../../core/models/city.models';
import { AddressFieldsComponent } from '../../../shared/address-fields/address-fields.component';
import { problemDetail } from '../../../core/utils/problem';
import { injectDateFormats } from '../../../core/i18n/locale-format';

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
  imports: [FormsModule, ButtonDirective, LoadingComponent, EmptyStateComponent, AddressFieldsComponent, TranslocoPipe],
  templateUrl: './training-edit.component.html',
  styleUrl: './training-edit.component.css',
})
export class TrainingEditComponent {
  private readonly trainings = inject(TrainingsService);
  private readonly transloco = inject(TranslocoService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fmt = injectDateFormats();

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
  // Feature 042 — structured address, prefilled from the session detail.
  protected venueName = '';
  protected street = '';
  protected postalCode = '';
  /** The city currently stored, so the picker reads it back. Set once, before the form renders. */
  protected readonly initialCity = signal<Location | null>(null);
  /**
   * Tri-state, and it has to be: `undefined` = the admin never touched the city (resend the stored
   * one), `null` = they cleared it (the server refuses that for an in-person training, which is the
   * point), a value = they picked a new one.
   */
  protected readonly pickedCity = signal<CityOption | null | undefined>(undefined);
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
    this.venueName = s.venueName ?? '';
    this.street = s.street ?? '';
    this.postalCode = s.postalCode ?? '';
    // Set before the form renders — `jh-city-picker` reads its `initial` in ngOnInit, and the form
    // only exists in the loaded branch of the template.
    this.initialCity.set(s.location);
    this.pickedCity.set(undefined);
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

  protected onCitySelected(option: CityOption | null): void {
    this.pickedCity.set(option);
  }

  /**
   * The city fragment to send. Untouched ⇒ resend the stored city so the block replace does not
   * silently drop it; cleared ⇒ send an explicit null and let the server refuse it.
   */
  private citySelection() {
    const picked = this.pickedCity();
    if (picked !== undefined) {
      return toSelection(picked);
    }
    const stored = this.initialCity();
    return stored ? { cityExternalId: stored.externalId, name: stored.name } : toSelection(null);
  }

  /** The whole in-person address, always sent together — never field by field (FR-007). */
  private addressBlock() {
    return {
      venueName: this.venueName.trim() || null,
      street: this.street.trim() || null,
      postalCode: this.postalCode.trim() || null,
      location: this.citySelection(),
    };
  }

  /** Follows the app language; the shared helper pins a date-only value to local midnight. */
  protected readonly shortDate = (date: string) => this.fmt.shortDate(date);

  protected cancel(): void {
    this.router.navigate(['/trainings/sessions', this.sessionId]);
  }

  protected saveSingle(): void {
    if (this.busy() || !this.canEdit()) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    const inPerson = this.locationKind === 'InPerson';
    const body: EditSessionRequest = {
      sessionDate: this.sessionDate,
      startTime: `${this.startTime}:00`,
      endTime: `${this.endTime}:00`,
      locationKind: this.locationKind,
      ...(inPerson
        ? this.addressBlock()
        : { venueName: null, street: null, postalCode: null, location: null }),
      virtualLink: inPerson ? null : this.virtualLink.trim(),
    };
    this.trainings.editSession(this.sessionId, body).subscribe({
      next: () => this.router.navigate(['/trainings/sessions', this.sessionId]),
      error: (err) => {
        this.busy.set(false);
        this.error.set(problemDetail(err, this.transloco.translate('trainings.genericError')));
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
    const inPerson = this.locationKind === 'InPerson';
    const body: EditSeriesRequest = {
      name: this.name.trim(),
      description: this.description.trim() || null,
      locationKind: this.locationKind,
      ...(inPerson
        ? this.addressBlock()
        : { venueName: null, street: null, postalCode: null, location: null }),
      virtualLink: inPerson ? null : this.virtualLink.trim(),
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
        this.error.set(problemDetail(err, this.transloco.translate('trainings.genericError')));
      },
    });
  }
}
