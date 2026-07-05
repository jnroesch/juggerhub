import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Signal, WritableSignal } from '@angular/core';
import { EventCreateComponent } from './event-create.component';
import { EventType, LocationKind } from '../../../core/models/event.models';

/**
 * US1 — the wizard's per-step `canAdvance` gate. Server-side validation is the real
 * boundary (covered by CreateEventTests); this only checks the client-side gating so
 * the user can't advance past an incomplete step.
 */

/** Protected surface we drive directly in tests (signals are callable + .set). */
interface WizardApi {
  form: { patchValue(value: Record<string, unknown>): void; getRawValue(): Record<string, unknown> };
  type: WritableSignal<EventType>;
  locationKind: WritableSignal<LocationKind>;
  isPaid: WritableSignal<boolean>;
  step: WritableSignal<string>;
  canAdvance: Signal<boolean>;
}

describe('EventCreateComponent wizard validation', () => {
  let fixture: ComponentFixture<EventCreateComponent>;
  let api: WizardApi;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    fixture = TestBed.createComponent(EventCreateComponent);
    api = fixture.componentInstance as unknown as WizardApi;
    fixture.detectChanges();
  });

  it('type step needs a name and description, plus a custom label for the Other type', () => {
    expect(api.canAdvance()).toBe(false);
    api.form.patchValue({ name: 'Berlin Cup', description: 'A test event.' });
    expect(api.canAdvance()).toBe(true);

    api.type.set('Other');
    expect(api.canAdvance()).toBe(false); // Other requires a custom label
    api.form.patchValue({ customLabel: 'Meetup' });
    expect(api.canAdvance()).toBe(true);
  });

  it('when step requires end on or after start', () => {
    api.form.patchValue({ name: 'Cup', description: 'x' });
    api.step.set('when');

    api.form.patchValue({ startsAt: '2026-09-06T18:00', endsAt: '2026-09-05T09:00' });
    expect(api.canAdvance()).toBe(false);

    api.form.patchValue({ endsAt: '2026-09-06T20:00' });
    expect(api.canAdvance()).toBe(true);
  });

  it('where step: in-person needs a full address incl. country', () => {
    api.step.set('where');
    api.locationKind.set('InPerson');

    api.form.patchValue({ street: 'Hauptstr 1', postalCode: '10115', city: 'Berlin', country: '' });
    expect(api.canAdvance()).toBe(false);

    api.form.patchValue({ country: 'Deutschland' });
    expect(api.canAdvance()).toBe(true);
  });

  it('where step: virtual needs a valid link', () => {
    api.step.set('where');
    api.locationKind.set('Virtual');

    api.form.patchValue({ virtualLink: 'not-a-url' });
    expect(api.canAdvance()).toBe(false);

    api.form.patchValue({ virtualLink: 'https://zoom.us/j/123' });
    expect(api.canAdvance()).toBe(true);
  });

  it('who step needs a positive participation limit', () => {
    api.step.set('who');

    api.form.patchValue({ participationLimit: 0 });
    expect(api.canAdvance()).toBe(false);

    api.form.patchValue({ participationLimit: 8 });
    expect(api.canAdvance()).toBe(true);
  });

  it('fee step: free advances; paid requires a recipient and IBAN', () => {
    api.step.set('fee');

    api.isPaid.set(false);
    expect(api.canAdvance()).toBe(true);

    api.isPaid.set(true);
    api.form.patchValue({ feeRecipientName: '', feeIban: '' });
    expect(api.canAdvance()).toBe(false);

    api.form.patchValue({ feeRecipientName: 'JSC Berlin e.V.', feeIban: 'DE89370400440532013000' });
    expect(api.canAdvance()).toBe(true);
  });
});
