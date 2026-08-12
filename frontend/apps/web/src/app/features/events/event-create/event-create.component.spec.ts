import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Signal, WritableSignal } from '@angular/core';
import { EventCreateComponent } from './event-create.component';
import { TranslocoService } from '@jsverse/transloco';
import { translocoTestingModule, translocoLocaleTestingProviders } from '../../../../testing/transloco-testing';
import { EventType, LocationKind, ParticipantMode } from '../../../core/models/event.models';
import { CityOption } from '../../../core/models/city.models';
import { DRAFT_VERSION, EventDraft } from '../../../core/drafts/wizard-draft.models';
import { WizardDraftStore } from '../../../core/drafts/wizard-draft.store';

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
  onCitySelected(option: CityOption | null): void;
  // The address group lives outside the FormGroup — `jh-address-fields` two-way binds these.
  street: WritableSignal<string>;
  postalCode: WritableSignal<string>;
  // The rest of the state that lives outside the FormGroup (feature 045 persists all of it).
  participantMode: WritableSignal<ParticipantMode>;
  venueName: WritableSignal<string>;
  selectedCity: WritableSignal<CityOption | null>;
  publish(): void;
}

const BERLIN_OPTION: CityOption = {
  externalId: 'TEST:berlin',
  name: 'Berlin',
  region: null,
  countryName: 'Germany',
  countryCode: 'DE',
  label: 'Berlin, Germany',
  latitude: 52.52,
  longitude: 13.405,
};

describe('EventCreateComponent wizard validation', () => {
  let fixture: ComponentFixture<EventCreateComponent>;
  let api: WizardApi;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([]), ...translocoLocaleTestingProviders()],
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

  it('where step: in-person needs a street, postal code, and a selected city', () => {
    api.step.set('where');
    api.locationKind.set('InPerson');

    // Street + postal code present but no city picked yet ⇒ cannot advance.
    api.street.set('Hauptstr 1');
    api.postalCode.set('10115');
    expect(api.canAdvance()).toBe(false);

    // Picking a canonical city completes the step.
    api.onCitySelected(BERLIN_OPTION);
    expect(api.canAdvance()).toBe(true);
  });

  it('where step: virtual needs a link-like value (scheme optional)', () => {
    api.step.set('where');
    api.locationKind.set('Virtual');

    api.form.patchValue({ virtualLink: 'not-a-url' });
    expect(api.canAdvance()).toBe(false);

    // A bare domain (no scheme) is accepted — the server defaults to https.
    api.form.patchValue({ virtualLink: 'zoom.us/j/123' });
    expect(api.canAdvance()).toBe(true);

    api.form.patchValue({ virtualLink: 'https://meet.google.com/abc-defg' });
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

/**
 * Feature 045 (GH #182) — the wizard's state survives leaving the page.
 *
 * The event wizard keeps its answers in two places — a thirteen-control `FormGroup` and seven
 * signals beside it — and both halves have to be persisted. The signals are the half that is easy
 * to forget, because `valueChanges` cannot see them.
 */
describe('EventCreateComponent draft persistence', () => {
  /** Everything the wizard holds, with all 21 answers different from their defaults. */
  const FULL_DRAFT: EventDraft = {
    v: DRAFT_VERSION,
    step: 'fee',
    type: 'Workshop',
    locationKind: 'InPerson',
    participantMode: 'Individuals',
    isPaid: true,
    venueName: 'Sportpark Nord',
    street: 'Ringstr. 3',
    postalCode: '10115',
    city: BERLIN_OPTION,
    name: 'Autumn Cup',
    customLabel: 'Meetup',
    description: 'Two days of jugger.',
    startsAt: '2026-10-01T10:00',
    endsAt: '2026-10-02T18:00',
    virtualLink: 'https://meet.example.com/xyz',
    participationLimit: 24,
    rosterCap: 10,
    feeAmount: 40,
    feeCurrency: 'CHF',
    feeRecipientName: 'JSC Berlin e.V.',
    feeIban: 'DE89370400440532013000',
    feePaymentDeadline: '2026-09-15',
  };

  let fixture: ComponentFixture<EventCreateComponent>;
  let api: WizardApi;
  let store: WizardDraftStore;
  let httpMock: HttpTestingController;

  function configure(): void {
    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([]), ...translocoLocaleTestingProviders()],
    });
    store = TestBed.inject(WizardDraftStore);
    httpMock = TestBed.inject(HttpTestingController);
    jest.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  }

  function createComponent(): void {
    fixture = TestBed.createComponent(EventCreateComponent);
    api = fixture.componentInstance as unknown as WizardApi;
    fixture.detectChanges();
  }

  beforeEach(() => {
    sessionStorage.clear();
    jest.restoreAllMocks();
  });

  afterEach(() => sessionStorage.clear());

  /**
   * SC-003. Asserted field by field on purpose: a test that samples a few fields passes happily
   * while one of the others is silently dropped — which is the failure being fixed.
   */
  it('restores all 21 answers and the step', () => {
    configure();
    store.writeEvent(FULL_DRAFT);
    createComponent();

    const v = api.form.getRawValue();
    expect(api.step()).toBe('fee');
    expect(api.type()).toBe('Workshop');
    expect(api.locationKind()).toBe('InPerson');
    expect(api.participantMode()).toBe('Individuals');
    expect(api.isPaid()).toBe(true);
    expect(api.venueName()).toBe('Sportpark Nord');
    expect(api.street()).toBe('Ringstr. 3');
    expect(api.postalCode()).toBe('10115');
    expect(api.selectedCity()).toEqual(BERLIN_OPTION);
    expect(v['name']).toBe('Autumn Cup');
    expect(v['customLabel']).toBe('Meetup');
    expect(v['description']).toBe('Two days of jugger.');
    expect(v['startsAt']).toBe('2026-10-01T10:00');
    expect(v['endsAt']).toBe('2026-10-02T18:00');
    expect(v['virtualLink']).toBe('https://meet.example.com/xyz');
    expect(v['participationLimit']).toBe(24);
    expect(v['rosterCap']).toBe(10);
    expect(v['feeAmount']).toBe(40);
    expect(v['feeCurrency']).toBe('CHF');
    expect(v['feePaymentDeadline']).toBe('2026-09-15');
    // The two the owner decided to persist deliberately, so the fee step needn't be retyped.
    expect(v['feeRecipientName']).toBe('JSC Berlin e.V.');
    expect(v['feeIban']).toBe('DE89370400440532013000');
  });

  /**
   * The trap this feature is most likely to ship half-fixed (plan, risk 1): the picker reads its
   * `initial` input once in `ngOnInit`, so restoring the parent signal alone leaves the chip empty.
   */
  it('restores the picked city into the picker chip, not just the parent state', () => {
    configure();
    store.writeEvent({ ...FULL_DRAFT, step: 'where' });
    createComponent();

    const chip = fixture.nativeElement.querySelector('[data-testid="city-picker-chip"]');

    expect(chip).not.toBeNull();
    expect(chip.textContent).toContain('Berlin, Germany');
  });

  /** FR-013, against a form whose defaults are not blank (limit 16, cap 8, EUR). */
  it('writes nothing when the wizard is opened and left untouched', () => {
    configure();
    createComponent();

    expect(store.readEvent()).toBeNull();
  });

  /** FR-005: the form half of the state persists without advancing a step. */
  it('persists a form answer immediately', () => {
    configure();
    createComponent();

    api.form.patchValue({ description: 'Two days of jugger.' });
    fixture.detectChanges();

    expect(store.readEvent()?.description).toBe('Two days of jugger.');
  });

  /** The half `valueChanges` cannot see — these seven live outside the FormGroup. */
  it('persists the signal answers that live outside the form', () => {
    configure();
    createComponent();

    api.type.set('Workshop');
    api.participantMode.set('Individuals');
    api.isPaid.set(true);
    api.street.set('Ringstr. 3');
    api.postalCode.set('10115');
    api.onCitySelected(BERLIN_OPTION);
    api.step.set('who');
    fixture.detectChanges();

    const draft = store.readEvent();
    expect(draft?.type).toBe('Workshop');
    expect(draft?.participantMode).toBe('Individuals');
    expect(draft?.isPaid).toBe(true);
    expect(draft?.street).toBe('Ringstr. 3');
    expect(draft?.postalCode).toBe('10115');
    expect(draft?.city).toEqual(BERLIN_OPTION);
    expect(draft?.step).toBe('who');
  });

  it('opens blank when the stored draft is from an older release', () => {
    configure();
    sessionStorage.setItem('jh-draft:event', JSON.stringify({ ...FULL_DRAFT, v: DRAFT_VERSION + 1 }));

    createComponent();

    expect(api.form.getRawValue()['name']).toBe('');
    expect(api.step()).toBe('type');
  });

  /** FR-007 — cleared once the server has accepted, so a reopened wizard is blank. */
  it('clears the draft after the server accepts the publish', () => {
    configure();
    store.writeEvent({ ...FULL_DRAFT, step: 'review' });
    createComponent();

    api.publish();
    httpMock.expectOne('/api/v1/events').flush({ id: 'e1' });

    expect(store.readEvent()).toBeNull();
  });

  /** The other half of FR-007: a rejected publish must not throw the user's answers away. */
  it('keeps the draft when the publish is rejected', () => {
    configure();
    store.writeEvent({ ...FULL_DRAFT, step: 'review' });
    createComponent();

    api.publish();
    httpMock
      .expectOne('/api/v1/events')
      .flush({ detail: 'Nope.' }, { status: 400, statusText: 'Bad Request' });

    expect(store.readEvent()?.name).toBe('Autumn Cup');
  });

  /**
   * GH #187 — the review step must show start/end in the active language, never the raw
   * `datetime-local` strings. Mirrors the recognition-display regression spec: assert the de/en
   * difference and that a mid-review language switch re-renders. `translocoDate` follows the active
   * language, unlike Angular's `| date` pipe, which is pinned to `LOCALE_ID` at bootstrap.
   */
  it('renders the review start/end in the active language, not raw datetime-local', () => {
    configure();
    store.writeEvent({ ...FULL_DRAFT, step: 'review' });
    createComponent();

    const text = () => fixture.nativeElement.textContent as string;
    // startsAt/endsAt are 2026-10-01T10:00 / 2026-10-02T18:00.
    expect(text()).not.toContain('2026-10-01T10:00');
    expect(text()).toContain('Oct'); // en short month

    TestBed.inject(TranslocoService).setActiveLang('de');
    fixture.detectChanges();

    expect(text()).toContain('Okt'); // de short month
    expect(text()).not.toContain('Oct');
  });
});
