import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AddressFieldsComponent } from './address-fields.component';
import { CityOption, Location } from '../../core/models/city.models';
import { translocoTestingModule } from '../../../testing/transloco-testing';

/**
 * The shared address group (feature 042). These cover the contract the three training forms rely
 * on: the four values reach the host, and the city selection is forwarded rather than owned here
 * (the host persists `cityExternalId`; the server re-resolves it — constitution Principle I).
 */

const KOLN: CityOption = {
  externalId: 'TEST:köln', name: 'Köln', region: 'Nordrhein-Westfalen', countryName: 'Germany',
  countryCode: 'DE', label: 'Köln, Germany', latitude: 50.94, longitude: 6.96,
};

const KOLN_STORED: Location = {
  externalId: 'TEST:köln', name: 'Köln', region: 'Nordrhein-Westfalen', countryName: 'Germany',
  countryCode: 'DE', label: 'Köln, Germany',
};

describe('AddressFieldsComponent', () => {
  let fixture: ComponentFixture<AddressFieldsComponent>;
  let component: AddressFieldsComponent;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(AddressFieldsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
  });

  afterEach(() => httpMock.verify());

  function field(testId: string): HTMLInputElement {
    const el = fixture.nativeElement.querySelector(`[data-testid="${testId}"]`) as HTMLInputElement;
    expect(el).toBeTruthy();
    return el;
  }

  async function type(testId: string, value: string): Promise<void> {
    const el = field(testId);
    el.value = value;
    el.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await fixture.whenStable();
  }

  it('renders venue, street, postal and city under the default testid prefix', () => {
    field('address-venue');
    field('address-street');
    field('address-postal');
    expect(fixture.nativeElement.querySelector('[data-testid="address-city"]')).toBeTruthy();
  });

  it('scopes testids to the given prefix so each host form targets its own fields', async () => {
    fixture.componentRef.setInput('testIdPrefix', 'series');
    fixture.detectChanges();
    await fixture.whenStable();

    field('series-street');
    expect(fixture.nativeElement.querySelector('[data-testid="address-street"]')).toBeNull();
  });

  it('writes each typed value back through its two-way model', async () => {
    await type('address-venue', 'Sportpark Müngersdorf');
    await type('address-street', 'Aachener Str. 999');
    await type('address-postal', '50933');

    expect(component.venueName()).toBe('Sportpark Müngersdorf');
    expect(component.street()).toBe('Aachener Str. 999');
    expect(component.postalCode()).toBe('50933');
  });

  it('renders values pushed in from the host, so an edit form prefills', async () => {
    fixture.componentRef.setInput('street', 'Aachener Str. 999');
    fixture.detectChanges();
    await fixture.whenStable();

    expect(field('address-street').value).toBe('Aachener Str. 999');
  });

  it('emits the picked city, and null when it is cleared', () => {
    const emitted: (CityOption | null)[] = [];
    component.cityChange.subscribe((c) => emitted.push(c));

    // The picker owns selection UX; this component only forwards it to the host form.
    component['onCitySelected'](KOLN);
    component['onCitySelected'](null);

    expect(emitted).toEqual([KOLN, null]);
  });

  it('forwards initialCity to the picker so an edit form reads back the current city', async () => {
    // `jh-city-picker` consumes `initial` in ngOnInit, so it must be present at FIRST render — a
    // value pushed in later never reaches the chip. Hosts must therefore only render this group
    // once their data has loaded, which is what the training edit forms do (they render the form
    // in the `@else` branch of their loading gate). Constructing a fresh fixture here mirrors that.
    const late = TestBed.createComponent(AddressFieldsComponent);
    late.componentRef.setInput('initialCity', KOLN_STORED);
    late.detectChanges();
    await late.whenStable();

    expect(late.nativeElement.textContent).toContain('Köln, Germany');
  });
});
