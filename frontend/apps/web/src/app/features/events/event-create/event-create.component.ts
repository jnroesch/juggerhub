import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import {
  CreateEventRequest,
  EventType,
  LocationKind,
  ParticipantMode,
} from '../../../core/models/event.models';
import { EventService } from '../../../core/services/event.service';
import { problemDetail } from '../../../core/utils/problem';

type Step = 'type' | 'when' | 'where' | 'who' | 'fee' | 'review';

const STEPS: readonly Step[] = ['type', 'when', 'where', 'who', 'fee', 'review'];

/**
 * US1 — the guided create wizard. One decision per screen with round-knob progress,
 * in the same calm style as onboarding: type & name, when, where (in-person address
 * vs virtual link), who can join + limit, fee, then a review before Publish. The
 * creator becomes the first admin and lands on the new event's page. All validation
 * is re-enforced server-side; this only gates the UX.
 */
@Component({
  selector: 'jh-event-create',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './event-create.component.html',
  styleUrl: './event-create.component.css',
})
export class EventCreateComponent {
  private readonly events = inject(EventService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  protected readonly steps = STEPS;
  protected readonly step = signal<Step>('type');
  protected readonly stepIndex = computed(() => STEPS.indexOf(this.step()));

  // Toggled choices (not form controls) — mirror the team-create toggle pattern.
  protected readonly type = signal<EventType>('Tournament');
  protected readonly locationKind = signal<LocationKind>('InPerson');
  protected readonly participantMode = signal<ParticipantMode>('Teams');
  protected readonly isPaid = signal(false);

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(120)]],
    customLabel: ['', [Validators.maxLength(40)]],
    description: ['', [Validators.required, Validators.maxLength(4000)]],
    startsAt: ['', [Validators.required]],
    endsAt: ['', [Validators.required]],
    venueName: ['', [Validators.maxLength(120)]],
    street: ['', [Validators.maxLength(160)]],
    postalCode: ['', [Validators.maxLength(20)]],
    city: ['', [Validators.maxLength(120)]],
    country: ['', [Validators.maxLength(80)]],
    virtualLink: ['', [Validators.maxLength(500)]],
    participationLimit: [16, [Validators.required, Validators.min(1)]],
    feeAmount: [null as number | null],
    feeCurrency: ['EUR', [Validators.maxLength(3)]],
    feeRecipientName: ['', [Validators.maxLength(120)]],
    feeIban: ['', [Validators.maxLength(34)]],
    feePaymentDeadline: [''],
  });

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
          ? [v.street, v.postalCode, v.city, v.country].every((s) => s.trim().length > 0)
          : /^https?:\/\/.+/i.test(v.virtualLink.trim());
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
      next: (created) => this.router.navigate(['/events', created.id]),
      error: (err) => {
        this.submitting.set(false);
        this.error.set(problemDetail(err));
      },
    });
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
      venueName: inPerson ? this.blankToNull(v.venueName) : null,
      street: inPerson ? this.blankToNull(v.street) : null,
      postalCode: inPerson ? this.blankToNull(v.postalCode) : null,
      city: inPerson ? this.blankToNull(v.city) : null,
      country: inPerson ? this.blankToNull(v.country) : null,
      virtualLink: inPerson ? null : this.blankToNull(v.virtualLink),
      participantMode: this.participantMode(),
      participationLimit: v.participationLimit,
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
