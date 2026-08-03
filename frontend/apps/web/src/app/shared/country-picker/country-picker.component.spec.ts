import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CountryPickerComponent } from './country-picker.component';
import { Country } from '../../core/models/city.models';

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
