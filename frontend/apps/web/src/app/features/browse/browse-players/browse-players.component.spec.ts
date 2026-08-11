import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PagedResult, PlayerCard } from '../../../core/models/search.models';
import { BrowsePlayersComponent } from './browse-players.component';
import { translocoLocaleTestingProviders, translocoTestingModule } from '../../../../testing/transloco-testing';

function card(overrides: Partial<PlayerCard> = {}): PlayerCard {
  return {
    handle: 'ada',
    displayName: 'Ada',
    location: null,
    positions: [],
    hasAvatar: false,
    ...overrides,
  };
}

function page(items: PlayerCard[]): PagedResult<PlayerCard> {
  return { items, totalCount: items.length, skip: 0, take: 20 };
}

/**
 * Covers the sort control only. The players tab gained A–Z / Nearest first on the feature 030
 * pattern; the option is offered strictly on whether the VIEWER has a home city, since the server
 * derives the anchor from their profile and answers 409 without one.
 */
describe('BrowsePlayersComponent — sort', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
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

  /** Mount and answer the initial list + profile calls. `homeCity` drives the proximity option. */
  function mount(items: PlayerCard[], homeCity: unknown = null): ComponentFixture<BrowsePlayersComponent> {
    const fixture = TestBed.createComponent(BrowsePlayersComponent);
    fixture.detectChanges(); // ngOnInit → browsePlayers() + getMineCached()
    listRequest().flush(page(items));
    httpMock.match((r) => r.url.includes('/api/v1/profiles/me')).forEach((r) => r.flush({ location: homeCity }));
    // The filter panel's country picker loads its list on init.
    httpMock.match((r) => r.url.includes('/api/v1/cities/countries')).forEach((r) => r.flush([]));
    fixture.detectChanges();
    return fixture;
  }

  function listRequest(): TestRequest {
    const matches = httpMock.match((r) => r.url === '/api/v1/profiles');
    expect(matches.length).toBeGreaterThan(0);
    return matches[matches.length - 1];
  }

  // Split across two tests deliberately: `ProfileService.getMineCached()` caches, so a second
  // mount inside one test issues no profile request and would silently keep the first answer.
  it('does not offer nearest-first without a home city', () => {
    const f = mount([card()]);
    const options = (f.componentInstance as unknown as { sortOptions(): { value: string }[] }).sortOptions();
    expect(options.map((o) => o.value)).toEqual(['DisplayNameAsc']);
  });

  it('offers nearest-first once the viewer has a home city', () => {
    const f = mount([card()], { name: 'Köln', label: 'Köln, Germany' });
    const options = (f.componentInstance as unknown as { sortOptions(): { value: string }[] }).sortOptions();
    expect(options.map((o) => o.value)).toEqual(['DisplayNameAsc', 'Proximity']);
  });

  it('sends the chosen ordering and changes nothing else', () => {
    const f = mount([card()], { name: 'Köln', label: 'Köln, Germany' });
    const component = f.componentInstance as unknown as {
      onSortChange(v: string): void;
      chips(): { key: string }[];
    };

    component.onSortChange('Proximity');
    f.detectChanges();
    const request = listRequest();

    expect(request.request.params.get('sort')).toBe('Proximity');
    // Sort is not a filter: no chip, no country narrowing, no extra params.
    expect(component.chips()).toHaveLength(0);
    expect(request.request.params.get('country')).toBeNull();
    request.flush(page([]));
  });

  it('withdraws the "every player is listed" note under nearest-first', () => {
    // Nearest-first excludes players with no home city, so the standing claim stops being true of
    // the list on screen. Asserted on the rendered note, not just the signal.
    const f = mount([card()], { name: 'Köln', label: 'Köln, Germany' });
    const noteText = () => (f.nativeElement.textContent ?? '') as string;
    expect(noteText()).toContain('Every player on JuggerHub is listed');

    (f.componentInstance as unknown as { onSortChange(v: string): void }).onSortChange('Proximity');
    f.detectChanges();
    expect(noteText()).not.toContain('Every player on JuggerHub is listed');

    listRequest().flush(page([]));
  });

  it('ignores a proximity request from a viewer with no home city', () => {
    // Belt and braces for the 409: the option is not rendered, but a stale value must not be
    // forwarded either — the request would fail the whole list rather than degrade.
    const f = mount([card()]);
    const component = f.componentInstance as unknown as { onSortChange(v: string): void };

    component.onSortChange('Proximity');
    f.detectChanges();
    const request = listRequest();

    expect(request.request.params.get('sort')).toBe('DisplayNameAsc');
    request.flush(page([]));
  });
});
