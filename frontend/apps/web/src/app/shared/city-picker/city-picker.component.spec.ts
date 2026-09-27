import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CityPickerComponent } from './city-picker.component';
import { CityOption } from '../../core/models/city.models';
import { translocoTestingModule } from '../../../testing/transloco-testing';

const BERLIN: CityOption = {
  externalId: 'osm:R:1', name: 'Berlin', region: 'Berlin', countryName: 'Germany',
  countryCode: 'DE', label: 'Berlin, Germany', latitude: 52.52, longitude: 13.4,
};
const BERN: CityOption = {
  externalId: 'osm:R:2', name: 'Bern', region: null, countryName: 'Switzerland',
  countryCode: 'CH', label: 'Bern, Switzerland', latitude: 46.95, longitude: 7.44,
};

describe('CityPickerComponent', () => {
  let fixture: ComponentFixture<CityPickerComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    jest.useFakeTimers(); // the type-ahead debounces 250ms
    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(CityPickerComponent);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
    jest.useRealTimers();
  });

  function input(): HTMLInputElement {
    return fixture.nativeElement.querySelector('[data-testid="city-picker-input"]');
  }

  function type(value: string): void {
    const el = input();
    el.value = value;
    el.dispatchEvent(new Event('input'));
    jest.advanceTimersByTime(300); // clear the debounce window
    fixture.detectChanges();
  }

  function search(): ReturnType<HttpTestingController['expectOne']> {
    return httpMock.expectOne((r) => r.url === '/api/v1/cities/search');
  }

  function options(): HTMLButtonElement[] {
    return Array.from(fixture.nativeElement.querySelectorAll('[role="option"]'));
  }

  it('debounces, queries the backend, and renders disambiguated options', () => {
    type('ber');
    const req = search();
    expect(req.request.params.get('q')).toBe('ber');
    req.flush([BERLIN, BERN]);
    fixture.detectChanges();

    const labels = options().map((b) => b.textContent?.trim());
    expect(labels).toEqual(['Berlin, Germany', 'Bern, Switzerland']);
  });

  it('does not query for a query shorter than two characters', () => {
    type('b');
    httpMock.expectNone((r) => r.url === '/api/v1/cities/search');
    expect(options().length).toBe(0);
  });

  it('emits the picked option and swaps the input for a chip', () => {
    let picked: CityOption | null | undefined;
    fixture.componentInstance.selectedChange.subscribe((o) => (picked = o));

    type('ber');
    search().flush([BERLIN, BERN]);
    fixture.detectChanges();

    options()[0].click();
    fixture.detectChanges();

    expect(picked).toEqual(BERLIN);
    expect(fixture.nativeElement.querySelector('[data-testid="city-picker-chip"]')).toBeTruthy();
    expect(input()).toBeFalsy(); // input is replaced by the confirmed chip
  });

  it('clears the selection and emits null', () => {
    const emitted: (CityOption | null)[] = [];
    fixture.componentInstance.selectedChange.subscribe((o) => emitted.push(o));

    type('ber');
    search().flush([BERLIN]);
    fixture.detectChanges();
    options()[0].click();
    fixture.detectChanges();

    fixture.nativeElement.querySelector('[aria-label="Clear city"]').click();
    fixture.detectChanges();

    expect(emitted).toEqual([BERLIN, null]);
    expect(input()).toBeTruthy(); // search field is back
  });

  describe('pending', () => {
    function keystroke(value: string): void {
      const el = input();
      el.value = value;
      el.dispatchEvent(new Event('input'));
    }

    it('is true from the keystroke — before the debounce — until the suggestions arrive', () => {
      const picker = fixture.componentInstance;
      expect(picker.pending()).toBe(false);

      keystroke('ber');
      expect(picker.pending()).toBe(true); // the 250ms wait counts too

      jest.advanceTimersByTime(300);
      const req = search();
      expect(picker.pending()).toBe(true); // request in flight

      req.flush([BERLIN]);
      expect(picker.pending()).toBe(false);
    });

    it('is never true for text too short to search', () => {
      keystroke('b');
      expect(fixture.componentInstance.pending()).toBe(false);
      jest.advanceTimersByTime(300);
      httpMock.expectNone((r) => r.url === '/api/v1/cities/search');
    });

    it('settles when the search fails, so nothing waiting on it is stuck', () => {
      type('ber');
      search().flush('unavailable', { status: 503, statusText: 'Service Unavailable' });
      expect(fixture.componentInstance.pending()).toBe(false);
    });

    it('searches again, and settles, when the same text is retyped after a clear', () => {
      // A swallowed repeat query would leave pending true for good — the reason the pipeline
      // does not use distinctUntilChanged.
      type('ber');
      search().flush([BERLIN]);
      fixture.detectChanges();
      options()[0].click();
      fixture.detectChanges();
      fixture.nativeElement.querySelector('[aria-label="Clear city"]').click();
      fixture.detectChanges();

      type('ber');
      expect(fixture.componentInstance.pending()).toBe(true);
      search().flush([BERLIN]);
      expect(fixture.componentInstance.pending()).toBe(false);
    });
  });

  it('shows a retryable transient state when the geocoder is unavailable (503)', () => {
    type('ber');
    search().flush('unavailable', { status: 503, statusText: 'Service Unavailable' });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="city-picker-unavailable"]')).toBeTruthy();
    expect(options().length).toBe(0);
  });
});

describe('CityPickerComponent — in German', () => {
  // Every string in this control used to be an English literal, so onboarding, the team wizard,
  // the training and event address steps and the profile read English wherever a city was asked for.
  let fixture: ComponentFixture<CityPickerComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    jest.useFakeTimers();
    TestBed.configureTestingModule({
      imports: [
        translocoTestingModule(
          { de: require('../../../../public/i18n/de.json') },
          { translocoConfig: { availableLangs: ['en', 'de', 'es'], defaultLang: 'de', fallbackLang: 'de' } },
        ),
      ],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(CityPickerComponent);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
    jest.useRealTimers();
  });

  const host = () => fixture.nativeElement as HTMLElement;
  const input = () => host().querySelector<HTMLInputElement>('[data-testid="city-picker-input"]')!;

  function type(value: string): void {
    input().value = value;
    input().dispatchEvent(new Event('input'));
    jest.advanceTimersByTime(300);
    fixture.detectChanges();
  }

  it('falls back to a German placeholder and names the field in German', () => {
    expect(input().placeholder).toBe('Nach einer Stadt suchen…');
    expect(input().getAttribute('aria-label')).toBe('Stadtsuche');
  });

  it('says in German that nothing matched, and that the search is unavailable', () => {
    type('xyz');
    httpMock.expectOne((r) => r.url === '/api/v1/cities/search').flush([]);
    fixture.detectChanges();
    expect(host().textContent).toContain('Keine passende Stadt – versuch eine andere Schreibweise.');

    type('abc');
    httpMock
      .expectOne((r) => r.url === '/api/v1/cities/search')
      .flush('unavailable', { status: 503, statusText: 'Service Unavailable' });
    fixture.detectChanges();
    expect(host().querySelector('[data-testid="city-picker-unavailable"]')?.textContent?.trim()).toBe(
      'Die Stadtsuche ist gerade nicht erreichbar. Bitte versuch es gleich noch einmal.',
    );
  });
});
