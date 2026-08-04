import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { ButtonDirective, LoadingComponent, AlertComponent } from '../../../shared/ui';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EditEventRequest, EventDetail } from '../../../core/models/event.models';
import { CityOption, Location, LocationSelection, toSelection } from '../../../core/models/city.models';
import { AddressFieldsComponent } from '../../../shared/address-fields/address-fields.component';
import { EventService } from '../../../core/services/event.service';
import { problemDetail } from '../../../core/utils/problem';

/**
 * US4/US8 — event settings: edit the event's details (mode is immutable; the limit
 * can't drop below the current occupied count) and cancel in a danger zone. Separate
 * from participant administration (/manage). Admin-only; authorized server-side.
 *
 * Where the event happens is editable here (GH #136): the in-person address — venue, street, postal
 * code and the canonical city — through the shared `jh-address-fields` group, or the join link of a
 * virtual one. Before that the form re-sent both verbatim, so a mistyped or expired location could
 * only be corrected by cancelling the event and recreating it, losing every sign-up.
 *
 * `LocationKind` itself stays immutable: an event never switches between in-person and virtual.
 */
@Component({
  selector: 'jh-event-edit',
  imports: [ReactiveFormsModule, RouterLink, ButtonDirective, LoadingComponent, AlertComponent, AddressFieldsComponent, TranslocoPipe],
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

  // The location fields live beside the FormGroup, not in it: `jh-address-fields` two-way binds
  // signals (it is shared with the template-driven training forms), and `locationComplete` is a
  // computed() over them — in a zoneless app a computed() over plain properties never recomputes,
  // and a FormControl read inside one is not a dependency either.
  protected readonly venueName = signal('');
  protected readonly street = signal('');
  protected readonly postalCode = signal('');
  /** The stored city, so the picker reads it back. Set before the form renders. */
  protected readonly initialCity = signal<Location | null>(null);
  /**
   * Tri-state, and it has to be: `undefined` = the admin never touched the city (resend the stored
   * one), `null` = they cleared it (an in-person event needs one, so save stays blocked), a value =
   * they picked a new one.
   */
  protected readonly pickedCity = signal<CityOption | null | undefined>(undefined);
  /** The join link of a virtual event — its entire location, and the one thing participants click. */
  protected readonly virtualLink = signal('');

  protected readonly isInPerson = computed(() => this.detail()?.locationKind === 'InPerson');

  /**
   * An in-person event needs a street, a postal code and a city; a virtual one needs a link.
   * Guidance only — the server enforces the same rules and is the boundary (Principle I).
   */
  protected readonly locationComplete = computed(() => {
    if (!this.isInPerson()) {
      // Lenient like the create wizard: a bare domain is accepted, the server defaults to https.
      return /\S\.\S/.test(this.virtualLink().trim());
    }
    const city = this.pickedCity();
    const hasCity = city === undefined ? this.initialCity() !== null : city !== null;
    return !!this.street().trim() && !!this.postalCode().trim() && hasCity;
  });

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
        this.prefillLocation(d);
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

  /**
   * Prefill the location fields. Called before the template's loaded branch renders anything, which
   * `jh-city-picker` requires: it consumes its `initial` in `ngOnInit`, so a city pushed in after
   * the picker exists never reaches the chip.
   */
  private prefillLocation(d: EventDetail): void {
    this.venueName.set(d.venueName ?? '');
    this.street.set(d.street ?? '');
    this.postalCode.set(d.postalCode ?? '');
    this.initialCity.set(d.location);
    this.pickedCity.set(undefined);
    this.virtualLink.set(d.virtualLink ?? '');
  }

  protected onCitySelected(option: CityOption | null): void {
    this.pickedCity.set(option);
  }

  /**
   * The city fragment to send. Untouched ⇒ resend the stored city's provider id so the backend
   * re-links the same canonical city; cleared ⇒ an explicit null the server refuses.
   */
  private citySelection(): LocationSelection {
    const picked = this.pickedCity();
    if (picked !== undefined) {
      return toSelection(picked);
    }
    const stored = this.initialCity();
    return stored ? { cityExternalId: stored.externalId, name: stored.name } : toSelection(null);
  }

  protected saveEdit(): void {
    const d = this.detail();
    if (!d || this.form.invalid || !this.locationComplete() || this.savingEdit()) {
      return;
    }
    this.savingEdit.set(true);
    this.editError.set(null);
    this.editSaved.set(false);

    const v = this.form.getRawValue();
    const inPerson = d.locationKind === 'InPerson';
    const request: EditEventRequest = {
      name: v.name.trim(),
      type: d.type,
      customTypeLabel: d.customTypeLabel,
      description: v.description.trim(),
      startsAt: v.startsAt,
      endsAt: v.endsAt,
      // LocationKind is not editable here — an event never switches between in-person and virtual.
      // So exactly one of the two location shapes is sent: the whole address block, or the link.
      locationKind: d.locationKind,
      venueName: inPerson ? this.venueName().trim() || null : null,
      street: inPerson ? this.street().trim() || null : null,
      postalCode: inPerson ? this.postalCode().trim() || null : null,
      location: inPerson ? this.citySelection() : null,
      virtualLink: inPerson ? null : this.virtualLink().trim() || null,
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
        // Re-baseline the address on what was actually stored, so a second save without touching
        // the picker resends the NEW city rather than the one loaded on entry. The picker's own
        // chip already shows the pick — it owns that state.
        this.prefillLocation(updated);
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
