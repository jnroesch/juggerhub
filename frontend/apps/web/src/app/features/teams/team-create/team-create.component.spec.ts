import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { CityOption } from '../../../core/models/city.models';
import { translocoTestingModule } from '../../../../testing/transloco-testing';
import { TeamCreateComponent } from './team-create.component';

const KIEL: CityOption = {
  externalId: 'osm:R:9', name: 'Kiel', region: 'Schleswig-Holstein', countryName: 'Germany',
  countryCode: 'DE', label: 'Kiel, Germany', latitude: 54.32, longitude: 10.14,
};

describe('TeamCreateComponent', () => {
  let fixture: ComponentFixture<TeamCreateComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    jest.useFakeTimers(); // the handle check debounces 300ms, the city type-ahead 250ms
    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        // A catch-all so the post-create navigation to /t/:slug resolves instead of throwing.
        provideRouter([{ path: '**', children: [] }]),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(TeamCreateComponent);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
    jest.useRealTimers();
  });

  function el<T extends HTMLElement>(testId: string): T {
    return fixture.nativeElement.querySelector(`[data-testid="${testId}"]`);
  }

  function submit(): HTMLButtonElement {
    return el<HTMLButtonElement>('team-create-submit');
  }

  function type(testId: string, value: string): void {
    const input = el<HTMLInputElement>(testId);
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function slugRequest() {
    return httpMock.expectOne((r) => r.url === '/api/v1/teams/slug-available');
  }

  /** Fills name + handle and lets the debounce elapse, leaving the check in flight. */
  function fillNameAndHandle(): void {
    type('team-name', 'Kiel Krakens');
    type('team-slug', 'kiel-krakens');
    jest.advanceTimersByTime(300);
    fixture.detectChanges();
  }

  /** Picks a city through the real picker, as a person would. */
  function pickCity(): void {
    const input = el<HTMLInputElement>('city-picker-input');
    input.value = 'kie';
    input.dispatchEvent(new Event('input'));
    jest.advanceTimersByTime(300);
    fixture.detectChanges();
    httpMock.expectOne((r) => r.url === '/api/v1/cities/search').flush([KIEL]);
    fixture.detectChanges();
    (fixture.nativeElement.querySelectorAll('[role="option"]')[0] as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  /**
   * The reported defect: with every other field filled, the button went live while the handle
   * check was still in flight — so a handle the check was about to refuse could be submitted,
   * and the team creation failed on the server instead.
   */
  it('keeps submit disabled while the handle check is still running', () => {
    fillNameAndHandle();
    pickCity();

    const request = slugRequest();
    expect(submit().disabled).toBe(true);

    request.flush({ available: true, normalized: 'kiel-krakens', reason: null });
    fixture.detectChanges();
    expect(submit().disabled).toBe(false);
  });

  /** The debounce window is the same hazard: nothing has been asked yet, so nothing is known. */
  it('keeps submit disabled before the debounce has fired', () => {
    type('team-name', 'Kiel Krakens');
    type('team-slug', 'kiel-krakens');
    pickCity();

    expect(submit().disabled).toBe(true);

    jest.advanceTimersByTime(300);
    slugRequest().flush({ available: true, normalized: 'kiel-krakens', reason: null });
    fixture.detectChanges();
    expect(submit().disabled).toBe(false);
  });

  it('re-blocks submit when the handle is edited after a positive check', () => {
    fillNameAndHandle();
    pickCity();
    slugRequest().flush({ available: true, normalized: 'kiel-krakens', reason: null });
    fixture.detectChanges();
    expect(submit().disabled).toBe(false);

    type('team-slug', 'kiel-krakens-2');
    expect(submit().disabled).toBe(true);

    jest.advanceTimersByTime(300);
    slugRequest().flush({ available: false, normalized: 'kiel-krakens-2', reason: 'Taken' });
    fixture.detectChanges();
    expect(submit().disabled).toBe(true);
  });

  /** A city team without a city is a refusal the server would make; don't offer the button. */
  it('keeps submit disabled until a city team has a city', () => {
    fillNameAndHandle();
    slugRequest().flush({ available: true, normalized: 'kiel-krakens', reason: null });
    fixture.detectChanges();

    expect(submit().disabled).toBe(true);
    expect(el('city-required')).not.toBeNull();

    pickCity();
    expect(submit().disabled).toBe(false);
  });

  /** A Mixteam has no city, so the city gate must not apply to it. */
  it('enables submit for a Mixteam with no city', () => {
    fillNameAndHandle();
    slugRequest().flush({ available: true, normalized: 'kiel-krakens', reason: null });
    el('type-mix').click();
    fixture.detectChanges();

    expect(submit().disabled).toBe(false);
  });

  /**
   * A failed check leaves availability unknown, so submit stays blocked — which makes it
   * essential that the failure is both said out loud and recoverable. Before, the error tore
   * the subscription down and no later keystroke was ever checked again.
   */
  it('says the check failed and still checks the next handle typed', () => {
    fillNameAndHandle();
    pickCity();
    slugRequest().error(new ProgressEvent('network error'));
    fixture.detectChanges();

    expect(submit().disabled).toBe(true);
    expect(el('slug-check-failed')).not.toBeNull();

    type('team-slug', 'kiel-krakens-2');
    jest.advanceTimersByTime(300);
    slugRequest().flush({ available: true, normalized: 'kiel-krakens-2', reason: null });
    fixture.detectChanges();

    expect(el('slug-check-failed')).toBeNull();
    expect(submit().disabled).toBe(false);
  });

  it('creates the team once every field has cleared', () => {
    fillNameAndHandle();
    pickCity();
    slugRequest().flush({ available: true, normalized: 'kiel-krakens', reason: null });
    fixture.detectChanges();

    submit().click();

    const request = httpMock.expectOne('/api/v1/teams');
    expect(request.request.body).toMatchObject({ name: 'Kiel Krakens', slug: 'kiel-krakens', type: 'CityTeam' });
    request.flush({ id: 't1', slug: 'kiel-krakens' });
    // The membership cache refresh the component fires after a successful create.
    httpMock.match((r) => r.url.startsWith('/api/v1/')).forEach((r) => r.flush({}));
  });
});
