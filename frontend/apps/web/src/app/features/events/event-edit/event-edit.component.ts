import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EditEventRequest, EventDetail } from '../../../core/models/event.models';
import { EventService } from '../../../core/services/event.service';
import { problemDetail } from '../../../core/utils/problem';

/**
 * US4/US8 — event settings: edit the event's details (mode is immutable; the limit
 * can't drop below the current occupied count) and cancel in a danger zone. Separate
 * from participant administration (/manage). Admin-only; authorized server-side.
 */
@Component({
  selector: 'jh-event-edit',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './event-edit.component.html',
  styleUrl: './event-edit.component.css',
})
export class EventEditComponent implements OnInit {
  private readonly events = inject(EventService);
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly detail = signal<EventDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly forbidden = signal(false);
  protected readonly savingEdit = signal(false);
  protected readonly editError = signal<string | null>(null);
  protected readonly editSaved = signal(false);
  protected readonly cancelling = signal(false);
  protected readonly cancelError = signal<string | null>(null);
  protected readonly confirmingCancel = signal(false);

  private id = '';

  protected readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(120)]],
    description: ['', [Validators.required, Validators.maxLength(4000)]],
    startsAt: ['', [Validators.required]],
    endsAt: ['', [Validators.required]],
    participationLimit: [1, [Validators.required, Validators.min(1)]],
  });

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    this.events.getEvent(this.id).subscribe({
      next: (d) => {
        if (!d.viewer.isAdmin) {
          this.forbidden.set(true);
          this.loading.set(false);
          return;
        }
        this.detail.set(d);
        this.form.patchValue({
          name: d.name,
          description: d.description,
          startsAt: d.startsAt.slice(0, 16),
          endsAt: d.endsAt.slice(0, 16),
          participationLimit: d.participationLimit,
        });
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.forbidden.set(true);
      },
    });
  }

  protected saveEdit(): void {
    const d = this.detail();
    if (!d || this.form.invalid || this.savingEdit()) {
      return;
    }
    this.savingEdit.set(true);
    this.editError.set(null);
    this.editSaved.set(false);

    const v = this.form.getRawValue();
    const request: EditEventRequest = {
      name: v.name.trim(),
      type: d.type,
      customTypeLabel: d.customTypeLabel,
      description: v.description.trim(),
      startsAt: v.startsAt,
      endsAt: v.endsAt,
      locationKind: d.locationKind,
      venueName: d.venueName,
      street: d.street,
      postalCode: d.postalCode,
      city: d.city,
      country: d.country,
      virtualLink: d.virtualLink,
      participationLimit: v.participationLimit,
      isPaid: d.isPaid,
      feeAmount: d.feeAmount,
      feeCurrency: d.feeCurrency,
      feeRecipientName: d.feeRecipientName,
      feeIban: d.feeIban,
      feePaymentDeadline: d.feePaymentDeadline,
    };

    this.events.editEvent(this.id, request).subscribe({
      next: (updated) => {
        this.detail.set(updated);
        this.savingEdit.set(false);
        this.editSaved.set(true);
      },
      error: (err) => {
        this.savingEdit.set(false);
        this.editError.set(problemDetail(err));
      },
    });
  }

  protected cancelEvent(): void {
    if (this.cancelling()) {
      return;
    }
    this.cancelling.set(true);
    this.cancelError.set(null);
    this.events.cancelEvent(this.id).subscribe({
      next: () => this.router.navigate(['/events', this.id]),
      error: (err) => {
        this.cancelling.set(false);
        this.cancelError.set(problemDetail(err));
      },
    });
  }
}
