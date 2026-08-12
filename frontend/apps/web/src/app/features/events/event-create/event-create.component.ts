import { Component, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoPipe } from '@jsverse/transloco';
import { TranslocoDatePipe } from '@jsverse/transloco-locale';
import { ButtonDirective, AlertComponent } from '../../../shared/ui';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import {
  CreateEventRequest,
  EventType,
  LocationKind,
  ParticipantMode,
} from '../../../core/models/event.models';
import { CityOption, toSelection } from '../../../core/models/city.models';
import { EventService } from '../../../core/services/event.service';
import { problemDetail } from '../../../core/utils/problem';
import { AddressFieldsComponent } from '../../../shared/address-fields/address-fields.component';
import { WizardDraftStore } from '../../../core/drafts/wizard-draft.store';
import { DRAFT_VERSION, EventDraft, EventDraftStep } from '../../../core/drafts/wizard-draft.models';

type Step = EventDraftStep;

const STEPS: readonly Step[] = ['type', 'when', 'where', 'who', 'fee', 'review'];

/**
 * The pristine wizard, as a draft. Needed for the comparison that decides whether there is anything
 * worth persisting (FR-013).
 *
 * ⚠ Not an empty form: the participation limit defaults to 16, the roster cap to 8 and the currency
 * to EUR. "Has the user entered anything" can only be answered by comparing against this, never by
 * a hand-written list of emptiness checks that would drift from the defaults.
 */
function pristineDraft(): EventDraft {
  return {
    v: DRAFT_VERSION,
    step: 'type',
    type: 'Tournament',
    locationKind: 'InPerson',
    participantMode: 'Teams',
    isPaid: false,
    venueName: '',
    street: '',
    postalCode: '',
    city: null,
    name: '',
    customLabel: '',
    description: '',
    startsAt: '',
    endsAt: '',
    virtualLink: '',
    participationLimit: 16,
    rosterCap: 8,
    feeAmount: null,
    feeCurrency: 'EUR',
    feeRecipientName: '',
    feeIban: '',
    feePaymentDeadline: '',
  };
}

/**
 * US1 — the guided create wizard. One decision per screen with round-knob progress,
 * in the same calm style as onboarding: type & name, when, where (in-person address
 * vs virtual link), who can join + limit, fee, then a review before Publish. The
 * creator becomes the first admin and lands on the new event's page. All validation
 * is re-enforced server-side; this only gates the UX.
 */
@Component({
  selector: 'jh-event-create',
  imports: [ReactiveFormsModule, RouterLink, ButtonDirective, AlertComponent, AddressFieldsComponent, TranslocoPipe, TranslocoDatePipe],
  templateUrl: './event-create.component.html',
  styleUrl: './event-create.component.css',
})
export class EventCreateComponent {
  private readonly events = inject(EventService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly drafts = inject(WizardDraftStore);

  /** What a wizard nobody has touched looks like. Captured once; the effect compares against it. */
  private readonly pristine = pristineDraft();

  /**
   * The draft as found at construction, or null.
   *
   * ⚠ Read **here**, in a field initialiser, and not in `ngOnInit` or anything asynchronous.
   * `CityPickerComponent` consumes its `initial` input in its own `ngOnInit` and never looks again,
   * so a city restored any later reaches {@link selectedCity} but never the chip — leaving an empty
   * city field beside a filled street. See the warning on {@link AddressFieldsComponent}.
   */
  private readonly restored = this.drafts.readEvent();

  protected readonly steps = STEPS;
  protected readonly step = signal<Step>(this.restored?.step ?? this.pristine.step);
  protected readonly stepIndex = computed(() => STEPS.indexOf(this.step()));

  // Toggled choices (not form controls) — mirror the team-create toggle pattern.
  protected readonly type = signal<EventType>(this.restored?.type ?? this.pristine.type);
  protected readonly locationKind = signal<LocationKind>(this.restored?.locationKind ?? this.pristine.locationKind);
  protected readonly participantMode = signal<ParticipantMode>(this.restored?.participantMode ?? this.pristine.participantMode);
  protected readonly isPaid = signal(this.restored?.isPaid ?? this.pristine.isPaid);

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  // Feature 030 — structured city for an in-person event.
  protected readonly selectedCity = signal<CityOption | null>(this.restored?.city ?? this.pristine.city);
  // The structured address (GH #136 — moved out of the FormGroup into `jh-address-fields`, the same
  // group the training forms use). Signals rather than form controls: the component two-way binds
  // them, and `canAdvance()` reads them from a template binding in a zoneless app.
  protected readonly venueName = signal(this.restored?.venueName ?? this.pristine.venueName);
  protected readonly street = signal(this.restored?.street ?? this.pristine.street);
  protected readonly postalCode = signal(this.restored?.postalCode ?? this.pristine.postalCode);

  /**
   * The restored city, passed to `[initialCity]` so the picker renders its chip. A plain field
   * rather than the live signal: the picker reads it once at first render (see {@link restored}).
   */
  protected readonly restoredCity = this.restored?.city ?? null;

  protected readonly form = this.fb.nonNullable.group({
    name: [this.restored?.name ?? this.pristine.name, [Validators.required, Validators.minLength(3), Validators.maxLength(120)]],
    customLabel: [this.restored?.customLabel ?? this.pristine.customLabel, [Validators.maxLength(40)]],
    description: [this.restored?.description ?? this.pristine.description, [Validators.required, Validators.maxLength(4000)]],
    startsAt: [this.restored?.startsAt ?? this.pristine.startsAt, [Validators.required]],
    endsAt: [this.restored?.endsAt ?? this.pristine.endsAt, [Validators.required]],
    virtualLink: [this.restored?.virtualLink ?? this.pristine.virtualLink, [Validators.maxLength(500)]],
    participationLimit: [this.restored?.participationLimit ?? this.pristine.participationLimit, [Validators.required, Validators.min(1)]],
    rosterCap: [this.restored?.rosterCap ?? this.pristine.rosterCap, [Validators.min(5)]],
    feeAmount: [(this.restored?.feeAmount ?? this.pristine.feeAmount) as number | null],
    feeCurrency: [this.restored?.feeCurrency ?? this.pristine.feeCurrency, [Validators.maxLength(3)]],
    feeRecipientName: [this.restored?.feeRecipientName ?? this.pristine.feeRecipientName, [Validators.maxLength(120)]],
    feeIban: [this.restored?.feeIban ?? this.pristine.feeIban, [Validators.maxLength(34)]],
    feePaymentDeadline: [this.restored?.feePaymentDeadline ?? this.pristine.feePaymentDeadline],
  });

  /**
   * Establishes the reactive dependency on the form for the persistence effect below. An `effect()`
   * cannot observe a `FormGroup` on its own, and the seven toggle/address signals cannot be observed
   * by `valueChanges` — this wizard keeps its answers in both places, so both have to be watched.
   */
  private readonly formValue = toSignal(this.form.valueChanges, { initialValue: null });

  constructor() {
    /**
     * Persist on every change, not on every step (feature 045, FR-005). Saving inside `next()`
     * would lose whichever step is open when a backgrounded tab is discarded — usually the
     * where-and-description step, the most expensive one to retype.
     *
     * `submitting` and `error` are deliberately absent from the snapshot: restoring
     * `submitting: true` would leave Publish disabled with no way out.
     */
    effect(() => {
      const draft = this.snapshot();
      // Equal to the pristine wizard ⇒ nothing to restore, and any earlier draft is stale.
      if (JSON.stringify(draft) === JSON.stringify(this.pristine)) {
        this.drafts.clearEvent();
        return;
      }
      this.drafts.writeEvent(draft);
    });
  }

  /**
   * The whole persisted state, in one place, so a field cannot be added without being persisted.
   * All 21 answers plus the step: fourteen from the signals and the step, thirteen from the form.
   */
  private snapshot(): EventDraft {
    this.formValue(); // dependency only — the raw value below is the authority.
    const v = this.form.getRawValue();
    return {
      v: DRAFT_VERSION,
      step: this.step(),
      type: this.type(),
      locationKind: this.locationKind(),
      participantMode: this.participantMode(),
      isPaid: this.isPaid(),
      venueName: this.venueName(),
      street: this.street(),
      postalCode: this.postalCode(),
      city: this.selectedCity(),
      name: v.name,
      customLabel: v.customLabel,
      description: v.description,
      startsAt: v.startsAt,
      endsAt: v.endsAt,
      virtualLink: v.virtualLink,
      participationLimit: v.participationLimit,
      rosterCap: v.rosterCap,
      feeAmount: v.feeAmount,
      feeCurrency: v.feeCurrency,
      feeRecipientName: v.feeRecipientName,
      feeIban: v.feeIban,
      feePaymentDeadline: v.feePaymentDeadline,
    };
  }

  /**
   * Whether the current step's inputs are complete enough to advance. A plain method
   * (not a `computed`) so it re-evaluates against live form values each change-detection
   * cycle — a `computed` would cache, since reactive-form reads aren't signal dependencies.
   */
  protected canAdvance(): boolean {
    const v = this.form.getRawValue();
    switch (this.step()) {
      case 'type':
        return (
          v.name.trim().length >= 3 &&
          v.description.trim().length > 0 &&
          (this.type() !== 'Other' || v.customLabel.trim().length > 0)
        );
      case 'when':
        return !!v.startsAt && !!v.endsAt && v.endsAt >= v.startsAt;
      case 'where':
        return this.locationKind() === 'InPerson'
          ? this.street().trim().length > 0 && this.postalCode().trim().length > 0 && this.selectedCity() !== null
          // Lenient: accept a domain with or without the scheme (server defaults to https).
          : /\S\.\S/.test(v.virtualLink.trim());
      case 'who':
        return v.participationLimit >= 1;
      case 'fee':
        return !this.isPaid() || (v.feeRecipientName.trim().length > 0 && v.feeIban.trim().length > 0);
      default:
        return true;
    }
  }

  protected next(): void {
    if (!this.canAdvance()) {
      return;
    }
    const i = this.stepIndex();
    if (i < STEPS.length - 1) {
      this.step.set(STEPS[i + 1]);
    }
  }

  protected back(): void {
    const i = this.stepIndex();
    if (i > 0) {
      this.step.set(STEPS[i - 1]);
    }
  }

  protected publish(): void {
    if (this.submitting()) {
      return;
    }
    this.submitting.set(true);
    this.error.set(null);

    this.events.createEvent(this.buildRequest()).subscribe({
      next: (created) => {
        // Only once the server has accepted it (FR-007). Clearing on the click instead would throw
        // the user's answers away at the one moment they most need them — a rejected publish.
        this.drafts.clearEvent();
        this.router.navigate(['/events', created.id]);
      },
      error: (err) => {
        this.submitting.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }

  protected onCitySelected(option: CityOption | null): void {
    this.selectedCity.set(option);
  }

  private buildRequest(): CreateEventRequest {
    const v = this.form.getRawValue();
    const inPerson = this.locationKind() === 'InPerson';
    const paid = this.isPaid();
    return {
      name: v.name.trim(),
      type: this.type(),
      customTypeLabel: this.type() === 'Other' ? v.customLabel.trim() : null,
      description: v.description.trim(),
      startsAt: v.startsAt,
      endsAt: v.endsAt,
      locationKind: this.locationKind(),
      venueName: inPerson ? this.blankToNull(this.venueName()) : null,
      street: inPerson ? this.blankToNull(this.street()) : null,
      postalCode: inPerson ? this.blankToNull(this.postalCode()) : null,
      location: inPerson ? toSelection(this.selectedCity()) : null,
      virtualLink: inPerson ? null : this.blankToNull(v.virtualLink),
      participantMode: this.participantMode(),
      participationLimit: v.participationLimit,
      rosterCap: this.participantMode() === 'Teams' ? v.rosterCap : null,
      isPaid: paid,
      feeAmount: paid ? v.feeAmount : null,
      feeCurrency: paid ? this.blankToNull(v.feeCurrency) ?? 'EUR' : null,
      feeRecipientName: paid ? this.blankToNull(v.feeRecipientName) : null,
      feeIban: paid ? this.blankToNull(v.feeIban) : null,
      feePaymentDeadline: paid ? this.blankToNull(v.feePaymentDeadline) : null,
    };
  }

  private blankToNull(value: string): string | null {
    const t = value.trim();
    return t.length === 0 ? null : t;
  }
}
