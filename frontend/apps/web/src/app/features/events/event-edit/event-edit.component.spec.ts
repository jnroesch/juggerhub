import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { Signal, WritableSignal } from '@angular/core';
import { EventEditComponent } from './event-edit.component';
import { translocoTestingModule } from '../../../../testing/transloco-testing';
import { EditEventRequest, EventDetail } from '../../../core/models/event.models';
import { CityOption, Location } from '../../../core/models/city.models';

/**
 * GH #136 — the event's city (and the rest of its address) must be editable after creation. Before
 * this the form re-sent the stored city verbatim with no picker in the template, so an organiser
 * could never move an event to another city.
 *
 * The server is the real validator (EditEventTests cover it); these cover the contract this form
 * owns: what it prefills, and what it sends.
 */

const BERLIN: Location = {
  externalId: 'TEST:berlin',
  name: 'Berlin',
  region: null,
  countryName: 'Germany',
  countryCode: 'DE',
  label: 'Berlin, Germany',
};

const HAMBURG_OPTION: CityOption = {
  externalId: 'TEST:hamburg',
  name: 'Hamburg',
  region: null,
  countryName: 'Germany',
  countryCode: 'DE',
  label: 'Hamburg, Germany',
  latitude: 53.55,
  longitude: 9.99,
};

const IN_PERSON: EventDetail = {
  id: 'e1',
  name: 'Berlin Cup',
  type: 'Tournament',
  customTypeLabel: null,
  description: 'A test event.',
  startsAt: '2026-10-01T10:00:00Z',
  endsAt: '2026-10-01T16:00:00Z',
  locationKind: 'InPerson',
  venueName: 'Sportpark',
  street: 'Hauptstr 1',
  postalCode: '10115',
  location: BERLIN,
  virtualLink: null,
  participantMode: 'Teams',
  participationLimit: 16,
  occupiedSpots: 2,
  isFull: false,
  isPaid: false,
  feeAmount: null,
  feeCurrency: null,
  feeRecipientName: null,
  feeIban: null,
  feePaymentDeadline: null,
  status: 'Published',
  viewer: { isAuthenticated: true, isAdmin: true, mySignupStatus: null, mySignupId: null, teamsICanEnter: [] },
};

const VIRTUAL: EventDetail = {
  ...IN_PERSON,
  locationKind: 'Virtual',
  venueName: null,
  street: null,
  postalCode: null,
  location: null,
  virtualLink: 'https://zoom.us/j/999',
};

/** Protected surface we drive directly in tests (signals are callable + .set). */
interface EditApi {
  venueName: WritableSignal<string>;
  street: WritableSignal<string>;
  postalCode: WritableSignal<string>;
  virtualLink: WritableSignal<string>;
  locationComplete: Signal<boolean>;
  onCitySelected(option: CityOption | null): void;
  saveEdit(): void;
}

describe('EventEditComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'e1' }) } } },
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    jest.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  });

  afterEach(() => httpMock.verify());

  function load(detail: EventDetail = IN_PERSON): ComponentFixture<EventEditComponent> {
    const fixture = TestBed.createComponent(EventEditComponent);
    fixture.detectChanges(); // ngOnInit → GET the event
    httpMock.expectOne('/api/v1/events/e1').flush(detail);
    fixture.detectChanges();
    return fixture;
  }

  function api(fixture: ComponentFixture<EventEditComponent>): EditApi {
    return fixture.componentInstance as unknown as EditApi;
  }

  /** The PATCH the save issued, flushed with the event as the server would return it. */
  function save(fixture: ComponentFixture<EventEditComponent>, response = IN_PERSON): EditEventRequest {
    api(fixture).saveEdit();
    const req = httpMock.expectOne('/api/v1/events/e1');
    expect(req.request.method).toBe('PATCH');
    req.flush(response);
    fixture.detectChanges();
    return req.request.body as EditEventRequest;
  }

  it('renders the address group with the stored city, so the organiser can see what to change', async () => {
    const fixture = load();
    await fixture.whenStable(); // `ngModel` writes the input values on the next microtask

    const host = fixture.nativeElement as HTMLElement;
    expect((host.querySelector('[data-testid="edit-street"]') as HTMLInputElement).value).toBe('Hauptstr 1');
    expect((host.querySelector('[data-testid="edit-postal"]') as HTMLInputElement).value).toBe('10115');
    expect((host.querySelector('[data-testid="edit-venue"]') as HTMLInputElement).value).toBe('Sportpark');
    // The picker reads its `initial` in ngOnInit — the form renders only after the event loaded.
    expect(host.querySelector('[data-testid="edit-city"]')?.textContent).toContain('Berlin, Germany');
  });

  it('sends the newly picked city — the bug: it used to resend the stored one', () => {
    const fixture = load();

    api(fixture).onCitySelected(HAMBURG_OPTION);
    const body = save(fixture);

    expect(body.location).toEqual({ cityExternalId: 'TEST:hamburg', name: 'Hamburg' });
  });

  it('resends the stored city when the picker was never touched', () => {
    const fixture = load();

    const body = save(fixture);

    expect(body.location).toEqual({ cityExternalId: 'TEST:berlin', name: 'Berlin' });
  });

  it('sends the edited street, postal code and venue', () => {
    const fixture = load();

    api(fixture).street.set('Aachener Str. 999');
    api(fixture).postalCode.set('50933');
    api(fixture).venueName.set('  Sportpark Müngersdorf  ');
    const body = save(fixture);

    expect(body).toMatchObject({
      street: 'Aachener Str. 999',
      postalCode: '50933',
      venueName: 'Sportpark Müngersdorf',
    });
  });

  it('blocks saving while the in-person address is incomplete', () => {
    const fixture = load();
    expect(api(fixture).locationComplete()).toBe(true);

    api(fixture).street.set('   ');
    expect(api(fixture).locationComplete()).toBe(false);

    api(fixture).street.set('Hauptstr 1');
    api(fixture).onCitySelected(null); // cleared, not merely untouched
    expect(api(fixture).locationComplete()).toBe(false);

    // The guard is real, not just a disabled button: saving issues no request.
    api(fixture).saveEdit();
    httpMock.expectNone('/api/v1/events/e1');
  });

  it('re-baselines after a save, so a second save resends the NEW city', () => {
    const fixture = load();

    api(fixture).onCitySelected(HAMBURG_OPTION);
    save(fixture, { ...IN_PERSON, location: { ...BERLIN, externalId: 'TEST:hamburg', name: 'Hamburg', label: 'Hamburg, Germany' } });

    // Nothing touched this time — the stored city is now Hamburg, not the Berlin loaded on entry.
    const body = save(fixture);
    expect(body.location).toEqual({ cityExternalId: 'TEST:hamburg', name: 'Hamburg' });
  });

  it('offers a virtual event its join link and no address at all', async () => {
    const fixture = load(VIRTUAL);
    await fixture.whenStable();

    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('[data-testid="edit-street"]')).toBeNull();
    expect(host.querySelector('[data-testid="edit-city"]')).toBeNull();
    expect((host.querySelector('[data-testid="edit-link"]') as HTMLInputElement).value)
      .toBe('https://zoom.us/j/999');

    const body = save(fixture, VIRTUAL);
    expect(body).toMatchObject({
      locationKind: 'Virtual',
      venueName: null,
      street: null,
      postalCode: null,
      location: null,
      virtualLink: 'https://zoom.us/j/999',
    });
  });

  it('sends a replaced join link — a link that expired is the whole location of a virtual event', () => {
    const fixture = load(VIRTUAL);

    api(fixture).virtualLink.set('  https://meet.google.com/abc-defg  ');
    const body = save(fixture, VIRTUAL);

    expect(body.virtualLink).toBe('https://meet.google.com/abc-defg');
  });

  it('blocks saving a virtual event without a usable link', () => {
    const fixture = load(VIRTUAL);

    api(fixture).virtualLink.set('   ');
    expect(api(fixture).locationComplete()).toBe(false);
    api(fixture).saveEdit();
    httpMock.expectNone('/api/v1/events/e1');

    // Lenient like the create wizard: a bare domain is fine, the server defaults to https.
    api(fixture).virtualLink.set('zoom.us/j/123');
    expect(api(fixture).locationComplete()).toBe(true);
  });
});
