import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { EditEventRequest, EventDetail, Signup } from '../../../core/models/event.models';
import { EventService } from '../../../core/services/event.service';
import { problemDetail } from '../../../core/utils/problem';

/**
 * US4/US8 — the admin management screen: approve awaiting-approval sign-ups, promote
 * from the waiting list (manual, capacity-guarded), remove anyone, edit the event's
 * details (mode is immutable; the limit can't drop below occupied), and cancel in a
 * danger zone. Authorization is enforced server-side; a non-admin is bounced.
 */
@Component({
  selector: 'jh-event-manage',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './event-manage.component.html',
  styleUrl: './event-manage.component.css',
})
export class EventManageComponent implements OnInit {
  private readonly events = inject(EventService);
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly detail = signal<EventDetail | null>(null);
  protected readonly joined = signal<Signup[]>([]);
  protected readonly awaiting = signal<Signup[]>([]);
  protected readonly waitlist = signal<Signup[]>([]);

  protected readonly loading = signal(true);
  protected readonly forbidden = signal(false);
  protected readonly acting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly savingEdit = signal(false);
  protected readonly editError = signal<string | null>(null);
  protected readonly editSaved = signal(false);
  protected readonly confirmingCancel = signal(false);

  private id = '';

  protected readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(120)]],
    description: ['', [Validators.required, Validators.maxLength(4000)]],
    startsAt: ['', [Validators.required]],
    endsAt: ['', [Validators.required]],
    participationLimit: [1, [Validators.required, Validators.min(1)]],
  });

  protected readonly full = computed(() => {
    const d = this.detail();
    return !!d && d.occupiedSpots >= d.participationLimit;
  });

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    this.load();
  }

  private load(): void {
    this.loading.set(true);
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
        this.loadGroups();
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.forbidden.set(true);
      },
    });
  }

  private loadGroups(): void {
    forkJoin({
      joined: this.events.getParticipants(this.id, 'joined'),
      awaiting: this.events.getParticipants(this.id, 'awaiting'),
      waitlist: this.events.getParticipants(this.id, 'waitlist'),
    }).subscribe((r) => {
      this.joined.set(r.joined.items);
      this.awaiting.set(r.awaiting.items);
      this.waitlist.set(r.waitlist.items);
    });
  }

  protected label(s: Signup): string {
    return s.teamName ?? s.userDisplayName ?? 'A participant';
  }

  protected approve(signupId: string): void {
    this.run(this.events.approve(this.id, signupId));
  }

  protected promote(signupId: string): void {
    this.run(this.events.promote(this.id, signupId));
  }

  protected remove(signupId: string): void {
    this.run(this.events.removeParticipant(this.id, signupId));
  }

  private run(obs: { subscribe: (o: { next: () => void; error: (e: unknown) => void }) => void }): void {
    if (this.acting()) {
      return;
    }
    this.acting.set(true);
    this.error.set(null);
    obs.subscribe({
      next: () => {
        this.acting.set(false);
        this.refresh();
      },
      error: (err) => {
        this.acting.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }

  private refresh(): void {
    this.events.getEvent(this.id).subscribe((d) => this.detail.set(d));
    this.loadGroups();
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
    if (this.acting()) {
      return;
    }
    this.acting.set(true);
    this.events.cancelEvent(this.id).subscribe({
      next: () => this.router.navigate(['/events', this.id]),
      error: (err) => {
        this.acting.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }
}
