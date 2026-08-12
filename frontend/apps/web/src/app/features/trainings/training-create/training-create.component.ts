import { Component, computed, effect, inject, signal } from '@angular/core';
import { ButtonDirective, CardComponent } from '../../../shared/ui';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TrainingsService } from '../../../core/services/trainings.service';
import { CreateTrainingRequest, LocationKind, TrainingInterval, TrainingVisibility } from '../../../core/models/trainings.models';

/**
 * A snapshot of what was just created, shown on the wizard's success step (GH #188). Captured from
 * the form the instant the server accepts, because the form signals are reset to pristine immediately
 * afterwards so the persistence effect can't re-save the submitted answers as a fresh draft.
 */
interface CreatedSummary {
  name: string;
  sessionCount: number;
  firstSessionId: string;
  isOneOff: boolean;
  weekday: string;
  interval: TrainingInterval;
  startTime: string;
  endTime: string;
}
import { CityOption, toSelection } from '../../../core/models/city.models';
import { AddressFieldsComponent } from '../../../shared/address-fields/address-fields.component';
import { problemDetail } from '../../../core/utils/problem';
import { WizardDraftStore } from '../../../core/drafts/wizard-draft.store';
import { DRAFT_VERSION, TrainingDraft, TrainingDraftStep } from '../../../core/drafts/wizard-draft.models';
import { injectDateFormats } from '../../../core/i18n/locale-format';

/**
 * The pristine wizard, as a draft. Computed rather than written out inline because two things need
 * it: the initial value of every signal, and the comparison that decides whether there is anything
 * worth persisting (FR-013).
 *
 * ⚠ Note this is **not** an empty form. `weekday` defaults to today, the times to 19:00/21:00,
 * `isRecurring` to true and `visibility` to TeamOnly — so "has the user entered anything" can only
 * be answered by comparing against this, never by a hand-written list of emptiness checks, which
 * would drift from the defaults the moment one changes.
 */
function pristineDraft(): TrainingDraft {
  return {
    v: DRAFT_VERSION,
    step: 1,
    isRecurring: true,
    name: '',
    // Default to today's weekday so the most common case (an admin scheduling a training for the day
    // they're setting it up) needs no change. Uses the same Monday-first labels as `weekdays` below;
    // getDay() is Sunday-indexed, hence the Sunday-first lookup array.
    weekday: ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'][new Date().getDay()],
    interval: 'Weekly',
    startTime: '19:00',
    endTime: '21:00',
    startDate: '',
    endDate: '',
    locationKind: 'InPerson',
    venueName: '',
    street: '',
    postalCode: '',
    city: null,
    virtualLink: '',
    description: '',
    visibility: 'TeamOnly',
  };
}

/**
 * The create-a-training wizard (feature 018): a calm one-decision-per-screen flow — series-or-one-off +
 * name, day/time/interval/end-date (collapses to a single date for a one-off), location + description,
 * team-only or public, then review and create. Admin-only; the API is the real guard.
 *
 * Every answer is kept as a **signal**, not a plain field. Two reasons, and the second is why the
 * remaining plain fields were converted in feature 045: the app is zoneless, so a `computed()` over
 * a plain property never recomputes (this once left Continue permanently disabled for virtual
 * trainings, because typing a join link never re-ran `whereComplete`), and an `effect()` cannot
 * observe a plain property either — which is what the draft persistence below depends on.
 *
 * The wizard's state survives leaving the page (feature 045, GH #182): it is restored from
 * `WizardDraftStore` at construction and written back whenever an answer changes.
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
  private readonly drafts = inject(WizardDraftStore);
  private readonly fmt = injectDateFormats();

  protected readonly slug = this.route.snapshot.paramMap.get('slug') ?? '';

  /** What a wizard nobody has touched looks like. Captured once; the effect compares against it. */
  private readonly pristine = pristineDraft();

  /**
   * The draft as found at construction, or null.
   *
   * ⚠ Read **here**, in a field initialiser, and not in `ngOnInit` or anything asynchronous.
   * `CityPickerComponent` consumes its `initial` input in its own `ngOnInit` and never looks again,
   * so a city restored any later than first render reaches the parent signal but never the chip —
   * leaving an empty city field beside a filled street, while the review step confidently prints
   * the city. See the warning on {@link AddressFieldsComponent}.
   */
  private readonly restored = this.drafts.readTraining(this.slug);

  protected readonly step = signal<TrainingDraftStep>(this.restored?.step ?? this.pristine.step);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  /**
   * Non-null once a create succeeds — the wizard swaps to a success step instead of redirecting to
   * one arbitrary session of the new series (GH #188). Not part of the persisted snapshot.
   */
  protected readonly created = signal<CreatedSummary | null>(null);

  // Form state. All signals — see the class comment.
  protected readonly isRecurring = signal(this.restored?.isRecurring ?? this.pristine.isRecurring);
  protected readonly name = signal(this.restored?.name ?? this.pristine.name);
  protected readonly weekday = signal(this.restored?.weekday ?? this.pristine.weekday);
  protected readonly interval = signal<TrainingInterval>(this.restored?.interval ?? this.pristine.interval);
  protected readonly startTime = signal(this.restored?.startTime ?? this.pristine.startTime);
  protected readonly endTime = signal(this.restored?.endTime ?? this.pristine.endTime);
  protected readonly startDate = signal(this.restored?.startDate ?? this.pristine.startDate);
  protected readonly endDate = signal(this.restored?.endDate ?? this.pristine.endDate);
  protected readonly locationKind = signal<LocationKind>(this.restored?.locationKind ?? this.pristine.locationKind);
  // Feature 042 — structured address; only meaningful when in-person.
  protected readonly venueName = signal(this.restored?.venueName ?? this.pristine.venueName);
  protected readonly street = signal(this.restored?.street ?? this.pristine.street);
  protected readonly postalCode = signal(this.restored?.postalCode ?? this.pristine.postalCode);
  protected readonly selectedCity = signal<CityOption | null>(this.restored?.city ?? this.pristine.city);
  protected readonly virtualLink = signal(this.restored?.virtualLink ?? this.pristine.virtualLink);
  protected readonly description = signal(this.restored?.description ?? this.pristine.description);
  protected readonly visibility = signal<TrainingVisibility>(this.restored?.visibility ?? this.pristine.visibility);

  /**
   * The restored city, passed to `[initialCity]` so the picker renders its chip. A plain field, not
   * the live signal: the picker reads it once at first render (see {@link restored}), and a later
   * value would be ignored anyway.
   */
  protected restoredCity = this.restored?.city ?? null;

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
    if (!this.isRecurring()) return 1;
    if (!this.startDate() || !this.endDate()) return null;
    // Rough client-side estimate for the review copy (the server is authoritative).
    const start = new Date(this.startDate());
    const end = new Date(this.endDate());
    if (end < start) return 0;
    const days = Math.floor((end.getTime() - start.getTime()) / 86400000);
    const per = this.interval() === 'Weekly' ? 7 : this.interval() === 'BiWeekly' ? 14 : 30;
    return Math.max(1, Math.floor(days / per) + 1);
  });

  /**
   * The series end date for the review step, in the active language — never the raw `YYYY-MM-DD`
   * the `<input type="date">` holds (GH #187). Routes through the same locale-bound helper as every
   * other trainings surface (tab / session / edit), so it also re-renders on a language switch and
   * pins the date-only value to local midnight (no day-early bug). Empty until the user picks a date.
   */
  protected readonly untilLabel = computed(() => {
    const value = this.endDate();
    return value ? this.fmt.shortDate(value) : '';
  });

  constructor() {
    /**
     * Persist on every change, not on every step (feature 045, FR-005). Saving inside `next()`
     * would lose whichever step is open when the tab is discarded — and that is usually the
     * address-and-description step, the most expensive one to retype and the exact case reported.
     *
     * Synchronous and undebounced on purpose: the payload is well under 2 KB, and a debounce
     * window is a window in which the eviction we are defending against costs the user their input.
     *
     * `busy` and `error` are deliberately absent from the snapshot. Restoring `busy: true` would
     * leave the Create button disabled with no way out.
     */
    effect(() => {
      const draft = this.snapshot();
      // Equal to the pristine wizard ⇒ there is nothing to restore, and any earlier draft is stale.
      if (JSON.stringify(draft) === JSON.stringify(this.pristine)) {
        this.drafts.clearTraining(this.slug);
        return;
      }
      this.drafts.writeTraining(this.slug, draft);
    });
  }

  /** The whole persisted state, in one place, so a field can't be added without being persisted. */
  private snapshot(): TrainingDraft {
    return {
      v: DRAFT_VERSION,
      step: this.step(),
      isRecurring: this.isRecurring(),
      name: this.name(),
      weekday: this.weekday(),
      interval: this.interval(),
      startTime: this.startTime(),
      endTime: this.endTime(),
      startDate: this.startDate(),
      endDate: this.endDate(),
      locationKind: this.locationKind(),
      venueName: this.venueName(),
      street: this.street(),
      postalCode: this.postalCode(),
      city: this.selectedCity(),
      virtualLink: this.virtualLink(),
      description: this.description(),
      visibility: this.visibility(),
    };
  }

  protected next(): void {
    this.error.set(null);
    this.step.update((s) => Math.min(s + 1, 5) as TrainingDraftStep);
  }

  protected back(): void {
    this.error.set(null);
    this.step.update((s) => Math.max(s - 1, 1) as TrainingDraftStep);
  }

  protected cancel(): void {
    // An explicit cancel is the user throwing the draft away (FR-008).
    this.drafts.clearTraining(this.slug);
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
      isRecurring: this.isRecurring(),
      name: this.name().trim(),
      description: this.description().trim() || null,
      locationKind: this.locationKind(),
      venueName: inPerson ? this.venueName().trim() || null : null,
      street: inPerson ? this.street().trim() || null : null,
      postalCode: inPerson ? this.postalCode().trim() || null : null,
      location: inPerson ? toSelection(this.selectedCity()) : null,
      virtualLink: inPerson ? null : this.virtualLink().trim(),
      weekday: this.isRecurring() ? this.weekday() : null,
      interval: this.isRecurring() ? this.interval() : null,
      startTime: `${this.startTime()}:00`,
      endTime: `${this.endTime()}:00`,
      startDate: this.startDate(),
      endDate: this.isRecurring() ? this.endDate() : null,
      visibility: this.visibility(),
    };
    this.trainings.create(this.slug, body).subscribe({
      next: (created) => {
        // Only once the server has accepted it (FR-007). Clearing on the click instead would throw
        // the user's answers away at the one moment they most need them — a rejected create.
        this.drafts.clearTraining(this.slug);
        // Confirm the series on a success step rather than dropping the user on one arbitrary session
        // (GH #188). Capture the summary BEFORE resetting the form, so nothing is lost to the reset.
        this.created.set({
          name: this.name().trim(),
          sessionCount: created.sessionCount,
          firstSessionId: created.firstSessionId,
          isOneOff: !this.isRecurring(),
          weekday: this.weekday(),
          interval: this.interval(),
          startTime: this.startTime(),
          endTime: this.endTime(),
        });
        // Return the wizard to pristine so the persistence effect sees no draft to write — otherwise
        // the just-submitted answers would be re-saved and "create another" would not start blank.
        this.busy.set(false);
        this.resetToPristine();
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(problemDetail(err, this.transloco.translate('trainings.genericError')));
        this.step.set(2); // schedule errors surface here
      },
    });
  }

  /** Start over from a genuinely blank wizard, keeping the team context (GH #188). */
  protected createAnother(): void {
    this.created.set(null);
    this.resetToPristine();
  }

  protected goToTrainings(): void {
    this.router.navigate(['/t', this.slug, 'trainings']);
  }

  /** The old default destination, now an opt-in secondary link off the success step. */
  protected viewFirstSession(): void {
    const summary = this.created();
    if (summary) {
      this.router.navigate(['/trainings/sessions', summary.firstSessionId]);
    }
  }

  /** i18n key for a series interval, translated client-side — matches the session page (GH #189). */
  protected intervalKey(interval: TrainingInterval): string {
    switch (interval) {
      case 'Weekly':
        return 'trainings.form.weekly';
      case 'BiWeekly':
        return 'trainings.form.biweekly';
      case 'Monthly':
        return 'trainings.form.monthly';
    }
  }

  /**
   * Reset every answer signal to the pristine wizard. Returning to pristine is what stops the
   * persistence effect from writing the just-submitted answers back as a new draft, and it clears
   * the restored-city hint so "create another" opens a blank picker.
   */
  private resetToPristine(): void {
    const p = this.pristine;
    this.step.set(p.step);
    this.isRecurring.set(p.isRecurring);
    this.name.set(p.name);
    this.weekday.set(p.weekday);
    this.interval.set(p.interval);
    this.startTime.set(p.startTime);
    this.endTime.set(p.endTime);
    this.startDate.set(p.startDate);
    this.endDate.set(p.endDate);
    this.locationKind.set(p.locationKind);
    this.venueName.set(p.venueName);
    this.street.set(p.street);
    this.postalCode.set(p.postalCode);
    this.selectedCity.set(p.city);
    this.virtualLink.set(p.virtualLink);
    this.description.set(p.description);
    this.visibility.set(p.visibility);
    this.restoredCity = null;
    this.error.set(null);
  }
}
