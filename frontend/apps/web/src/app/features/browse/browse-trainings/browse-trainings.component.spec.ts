import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { PagedResult, TrainingCard } from '../../../core/models/search.models';
import { BrowseReturnService } from '../../../core/services/browse-return.service';
import { BrowseTrainingsComponent } from './browse-trainings.component';
import { translocoLocaleTestingProviders, translocoTestingModule } from '../../../../testing/transloco-testing';

function card(overrides: Partial<TrainingCard> = {}): TrainingCard {
  return {
    sessionId: 's1',
    trainingId: 't1',
    name: 'Dienstagstraining',
    teamSlug: 'hamburg-hammers',
    teamName: 'Hamburg Hammers',
    isOneOff: false,
    sessionDate: '2026-08-11',
    startTime: '19:00:00',
    endTime: '21:00:00',
    locationKind: 'InPerson',
    location: null,
    locationLabel: 'Hamburg, Germany',
    ...overrides,
  };
}

function page(items: TrainingCard[]): PagedResult<TrainingCard> {
  return { items, totalCount: items.length, skip: 0, take: 20 };
}

describe('BrowseTrainingsComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      // The row renders dates through `translocoDate`, whose service needs the locale config
      // tokens; without these the component fails to construct with a NullInjector error rather
      // than anything resembling "missing pipe".
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
        ...translocoLocaleTestingProviders(),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify({ ignoreCancelled: true }));

  /** Mount and answer the initial list + profile calls. `location` drives the proximity option. */
  function mount(items: TrainingCard[], homeCity: unknown = null): ComponentFixture<BrowseTrainingsComponent> {
    const fixture = TestBed.createComponent(BrowseTrainingsComponent);
    fixture.detectChanges(); // ngOnInit → browseTrainings() + getMineCached()
    listRequest().flush(page(items));
    httpMock.match((r) => r.url.includes('/api/v1/profiles')).forEach((r) => r.flush({ location: homeCity }));
    // The filter panel's country picker loads its list on init.
    httpMock.match((r) => r.url.includes('/api/v1/cities/countries')).forEach((r) => r.flush([]));
    fixture.detectChanges();
    return fixture;
  }

  function listRequest(): TestRequest {
    const matches = httpMock.match((r) => r.url === '/api/v1/trainings');
    expect(matches.length).toBeGreaterThan(0);
    return matches[matches.length - 1];
  }

  const rows = (f: ComponentFixture<BrowseTrainingsComponent>) =>
    Array.from(f.nativeElement.querySelectorAll('[data-testid="training-row"]')) as HTMLAnchorElement[];

  it('leads with the team, keeps the training name secondary, and links to the session page', () => {
    const f = mount([card(), card({ sessionId: 's2', name: 'Open mat', teamName: 'Kiel Krakens' })]);

    const listed = rows(f);
    expect(listed).toHaveLength(2);
    expect(listed[0].getAttribute('href')).toBe('/trainings/sessions/s1');
    expect(listed[1].getAttribute('href')).toBe('/trainings/sessions/s2');

    // The team is the primary line: a guest is choosing who to train with, not reading a label
    // someone typed. The training's own name still appears, in the secondary line.
    const team = listed[0].querySelector('[data-testid="training-team"]') as HTMLElement;
    const meta = listed[0].querySelector('[data-testid="training-meta"]') as HTMLElement;
    expect(team.textContent).toContain('Hamburg Hammers');
    expect(team.textContent).not.toContain('Dienstagstraining');
    expect(meta.textContent).toContain('Dienstagstraining');
  });

  it('renders the date as a chip of weekday / day / month, like the home agenda', () => {
    const f = mount([card({ sessionDate: '2026-08-11' })]);
    const chip = rows(f)[0].querySelector('[aria-label]') as HTMLElement;

    // 11 Aug 2026 is a Tuesday. The accessible name carries all three parts, since visually they
    // are three stacked fragments.
    expect(chip.getAttribute('aria-label')).toBe('Tue 11 Aug');
    expect(chip.textContent).toContain('11');
  });

  it('renders a virtual training as online instead of an address', () => {
    // The backend deliberately returns an EMPTY label for a virtual training (unlike an event,
    // which gets the literal "Online"), so the wording comes from the client and follows the
    // viewer's language.
    const f = mount([
      card({ teamName: 'Kiel Krakens', locationKind: 'Virtual', locationLabel: '' }),
    ]);
    const text = rows(f)[0].textContent ?? '';

    expect(text).toContain('Online');
    expect(text).not.toContain('Germany');
  });

  it('sends the city filter — the param the other browse builders never send', () => {
    const f = mount([card()]);
    const component = f.componentInstance as unknown as {
      pendingCity: { set(v: string): void };
      applyFilters(): void;
    };

    component.pendingCity.set('Hamburg');
    httpMock.match((r) => r.url === '/api/v1/trainings').forEach((r) => r.flush(page([])));
    component.applyFilters();
    f.detectChanges();

    expect(listRequest().request.params.get('city')).toBe('Hamburg');
  });

  // Split across two tests deliberately: `ProfileService.getMineCached()` caches, so a second
  // mount inside one test issues no profile request and would silently keep the first answer.
  it('does not offer nearest-first without a home city', () => {
    const f = mount([card()]);
    const options = (f.componentInstance as unknown as { sortOptions(): { value: string }[] }).sortOptions();
    expect(options.map((o) => o.value)).toEqual(['SessionDateAsc']);
  });

  it('offers nearest-first once the viewer has a home city', () => {
    const f = mount([card()], { name: 'Köln', label: 'Köln, Germany' });
    const options = (f.componentInstance as unknown as { sortOptions(): { value: string }[] }).sortOptions();
    expect(options.map((o) => o.value)).toEqual(['SessionDateAsc', 'Proximity']);
  });

  it('changes only the ordering when the sort changes — never the date range', () => {
    // An earlier revision imposed a two-week window when switching to nearest-first. The owner
    // rejected it: a sort control that silently applies a filter is surprising, and someone asking
    // for the closest trainings wants the closest trainings, not the closest fortnight.
    const f = mount([card()], { name: 'Köln', label: 'Köln, Germany' });
    const component = f.componentInstance as unknown as {
      onSortChange(v: string): void;
      chips(): { key: string }[];
    };

    component.onSortChange('Proximity');
    f.detectChanges();
    const request = listRequest();

    expect(request.request.params.get('sort')).toBe('Proximity');
    expect(request.request.params.get('to')).toBeNull();
    expect(request.request.params.get('from')).toBeNull();
    expect(component.chips().some((c) => c.key === 'dates')).toBe(false);
    request.flush(page([]));
  });

  it('renders translated chip labels, never raw translation keys', () => {
    // ⚠ This assertion is worth keeping but does NOT guard the bug it looks like it guards.
    // `TranslocoTestingModule` preloads the catalogue synchronously, so `translate()` resolves on
    // the first computation and this passes even against the broken `lang` signal — verified by
    // reverting it and watching all 419 tests still pass. The real guard is in `browse.spec.ts`,
    // which exercises the actual asynchronous catalogue load in a browser.
    const f = mount([card()]);
    const chips = (f.componentInstance as unknown as { chips(): { key: string; label: string }[] }).chips();

    expect(chips.length).toBeGreaterThan(0);
    for (const chip of chips) {
      expect(chip.label).not.toMatch(/^browse\./);
      expect(chip.label.trim()).not.toBe('');
    }
    expect(chips.find((c) => c.key === 'hidePast')?.label).toBe('Upcoming');
  });

  describe('search and filters in the URL (GH #279)', () => {
    type Filterable = {
      pendingCity: { set(v: string): void };
      applyFilters(): void;
      clearAll(): void;
      query(): string;
      city(): string;
      hidePast(): boolean;
      initialCity(): { label: string } | null;
    };

    it('opens the list the URL describes — search, filters and the city chip', async () => {
      await TestBed.inject(Router).navigateByUrl('/?q=open&city=K%C3%B6ln&hidePast=false&from=2026-10-01&to=nonsense');
      const f = TestBed.createComponent(BrowseTrainingsComponent);
      f.detectChanges();
      const request = listRequest();
      const params = request.request.params;

      expect(params.get('q')).toBe('open');
      expect(params.get('city')).toBe('Köln');
      expect(params.get('hidePast')).toBe('false');
      expect(params.get('from')).toBe('2026-10-01');
      // A hand-edited value that is not a date never reaches the API.
      expect(params.get('to')).toBeNull();

      request.flush(page([card()]));
      httpMock.match((r) => r.url.includes('/api/v1/profiles')).forEach((r) => r.flush({ location: null }));
      httpMock.match((r) => r.url.includes('/api/v1/cities/countries')).forEach((r) => r.flush([]));
      f.detectChanges();

      const component = f.componentInstance as unknown as Filterable;
      expect(component.initialCity()?.label).toBe('Köln');
      const box = f.nativeElement.querySelector('[data-testid="browse-search"]') as HTMLInputElement;
      expect(box.value).toBe('open');
    });

    it('writes applied filters back to the URL and remembers them for the session page', async () => {
      const f = mount([card()]);
      const component = f.componentInstance as unknown as Filterable;

      component.pendingCity.set('Hamburg');
      component.applyFilters();
      listRequest().flush(page([]));
      await f.whenStable();

      expect(TestBed.inject(Router).url).toBe('/?city=Hamburg');
      expect(TestBed.inject(BrowseReturnService).queryParams('/browse/trainings')).toEqual({ city: ['Hamburg'] });
    });

    it('keeps an untouched list on a bare URL — defaults are never written', async () => {
      const f = mount([card()]);
      await f.whenStable();
      expect(TestBed.inject(Router).url).toBe('/');
    });

    it('resets the list when the URL is emptied under it, e.g. by re-clicking its tab', async () => {
      const router = TestBed.inject(Router);
      await router.navigateByUrl('/?city=Hamburg');
      const f = mount([card()]);
      const component = f.componentInstance as unknown as Filterable;
      expect(component.city()).toBe('Hamburg');

      await router.navigateByUrl('/');
      f.detectChanges();

      expect(component.city()).toBe('');
      expect(component.hidePast()).toBe(true);
      const request = listRequest();
      expect(request.request.params.get('city')).toBeNull();
      request.flush(page([]));
    });
  });

  it('distinguishes the empty state from no-results', () => {
    const f = mount([]);
    const component = f.componentInstance as unknown as { list: { state(): string; filtered: { set(v: boolean): void } } };
    expect(component.list.state()).toBe('empty');

    component.list.filtered.set(true);
    f.detectChanges();
    expect(component.list.state()).toBe('no-results');
  });
});
