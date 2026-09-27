import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CountryPickerComponent } from './country-picker.component';
import { Country } from '../../core/models/city.models';
import { translocoTestingModule } from '../../../testing/transloco-testing';

const COUNTRIES: Country[] = [
  { code: 'DE', name: 'Germany' },
  { code: 'CH', name: 'Switzerland' },
  { code: 'GB', name: 'United Kingdom' },
];

describe('CountryPickerComponent', () => {
  let fixture: ComponentFixture<CountryPickerComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(CountryPickerComponent);
    fixture.detectChanges(); // ngOnInit → GET /countries
  });

  afterEach(() => httpMock.verify());

  function flushCountries(list: Country[] = COUNTRIES): void {
    httpMock.expectOne('/api/v1/cities/countries').flush(list);
    fixture.detectChanges();
  }

  function inputEl(): HTMLInputElement {
    return fixture.nativeElement.querySelector('[data-testid="country-picker-input"]');
  }

  function focus(): void {
    inputEl().dispatchEvent(new Event('focus'));
    fixture.detectChanges();
  }

  function setValue(v: string): void {
    fixture.componentRef.setInput('value', v);
    fixture.detectChanges();
  }

  function options(): HTMLButtonElement[] {
    return Array.from(fixture.nativeElement.querySelectorAll('[role="option"]'));
  }

  it('fetches the country list once and offers all of them when focused with no query', () => {
    flushCountries();
    focus();
    expect(options().length).toBe(3);
    expect(options()[0].textContent).toContain('Germany');
  });

  it('filters client-side as the viewer types — no per-keystroke request', () => {
    flushCountries();
    focus();
    setValue('ger');
    expect(options().length).toBe(1);
    expect(options()[0].textContent).toContain('Germany');
    httpMock.verify(); // proves no extra request went out while filtering
  });

  it('matches on the ISO code too', () => {
    flushCountries();
    focus();
    setValue('ch'); // not a substring of any name; only Switzerland's code
    expect(options().length).toBe(1);
    expect(options()[0].textContent).toContain('Switzerland');
  });

  it('emits the exact country name when a suggestion is picked', () => {
    flushCountries();
    focus();
    setValue('ger');
    const emitted: string[] = [];
    fixture.componentInstance.valueChange.subscribe((v) => emitted.push(v));

    options()[0].click();
    expect(emitted).toEqual(['Germany']);
  });

  it('emits the typed text on input', () => {
    flushCountries();
    focus();
    const emitted: string[] = [];
    fixture.componentInstance.valueChange.subscribe((v) => emitted.push(v));

    const el = inputEl();
    el.value = 'united';
    el.dispatchEvent(new Event('input'));
    expect(emitted).toEqual(['united']);
  });

  it('clears the filter and emits an empty value', () => {
    flushCountries();
    setValue('Germany');
    const emitted: string[] = [];
    fixture.componentInstance.valueChange.subscribe((v) => emitted.push(v));

    fixture.nativeElement.querySelector('[aria-label="Clear country"]').click();
    expect(emitted).toEqual(['']);
  });
});

describe('CountryPickerComponent — in German', () => {
  // The placeholder and both accessible names used to be English literals, so the German Browse
  // filters read "Any country" and a screen reader announced "Country" / "Clear country".
  it('reads the placeholder and both accessible names from the catalogue', () => {
    TestBed.configureTestingModule({
      imports: [
        translocoTestingModule(
          { de: require('../../../../public/i18n/de.json') },
          {
            translocoConfig: { availableLangs: ['en', 'de', 'es'], defaultLang: 'de', fallbackLang: 'de' },
          },
        ),
      ],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting()],
    });
    const fixture = TestBed.createComponent(CountryPickerComponent);
    fixture.componentRef.setInput('value', 'Deutschland');
    fixture.detectChanges();
    TestBed.inject(HttpTestingController).expectOne('/api/v1/cities/countries').flush([]);
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    const input = host.querySelector<HTMLInputElement>('[data-testid="country-picker-input"]')!;
    expect(input.placeholder).toBe('Jedes Land');
    expect(input.getAttribute('aria-label')).toBe('Land');
    expect(host.querySelector('[aria-label="Land entfernen"]')).not.toBeNull();
  });
});
