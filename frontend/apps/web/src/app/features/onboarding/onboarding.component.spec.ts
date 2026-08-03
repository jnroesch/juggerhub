import { provideHttpClient, withXhr } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed, ComponentFixture } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { WritableSignal, Signal } from '@angular/core';
import { OnboardingComponent } from './onboarding.component';
import { translocoTestingModule } from '../../../testing/transloco-testing';
import { OwnerProfile, UpdateProfileRequest } from '../../core/models/profile.models';
import { PagedResult, TeamCard } from '../../core/models/search.models';
import { CityOption, Location } from '../../core/models/city.models';
import { Pompfe } from '../../shared/pompfen.catalog';

const BERLIN_LOC: Location = {
  externalId: 'TEST:berlin',
  name: 'Berlin',
  region: null,
  countryName: 'Germany',
  countryCode: 'DE',
  label: 'Berlin, Germany',
};

const BERLIN_OPTION: CityOption = { ...BERLIN_LOC, latitude: 52.52, longitude: 13.405 };

const HAMBURG_LOC: Location = {
  externalId: 'TEST:hamburg',
  name: 'Hamburg',
  region: null,
  countryName: 'Germany',
  countryCode: 'DE',
  label: 'Hamburg, Germany',
};

const PROFILE: OwnerProfile = {
  handle: 'nik',
  displayName: 'nik',
  location: null,
  description: null,
  hasAvatar: false,
  pompfen: [],
  recentActivity: [],
};

const BERLIN: TeamCard = {
  slug: 'berlin-jugger',
  name: 'Berlin Jugger',
  location: BERLIN_LOC,
  playerCount: 24,
  beginnersWelcome: true,
  logoInitial: 'B',
};

/** Deliberately NOT beginners-welcome — only reachable by searching (FR-003). */
const HAMBURG: TeamCard = {
  slug: 'hamburg-hammers',
  name: 'Hamburg Hammers',
  location: HAMBURG_LOC,
  playerCount: 18,
  beginnersWelcome: false,
  logoInitial: 'H',
};

function page(items: TeamCard[]): PagedResult<TeamCard> {
  return { items, totalCount: items.length, skip: 0, take: 20 };
}

/** Protected surface we drive directly in tests (signals are callable + .set). */
interface OnboardingApi {
  step: Signal<string>;
  displayName: WritableSignal<string>;
  onCitySelected: (option: CityOption | null) => void;
  description: WritableSignal<string>;
  selectedPompfen: WritableSignal<Pompfe[]>;
  nameEmpty: Signal<boolean>;
  // Team step (feature 029)
  teamQuery: Signal<string>;
  selectedTeam: Signal<TeamCard | null>;
  requestedSlugs: Signal<ReadonlySet<string>>;
  selectedRequested: Signal<boolean>;
  teamRequestError: Signal<string | null>;
  teams: { state: Signal<string>; items: Signal<TeamCard[]> };
  onTeamQuery(value: string): void;
  selectTeam(team: TeamCard): void;
  askToJoin(): void;
  next(): void;
  back(): void;
  finish(): void;
  dismiss(): void;
}

describe('OnboardingComponent', () => {
  let httpMock: HttpTestingController;
  // Mutable ActivatedRoute stub — a test sets a pending returnUrl via withReturnUrl().
  let routeStub: { snapshot: { queryParamMap: ReturnType<typeof convertToParamMap> } };

  beforeEach(() => {
    // The team search debounces (250ms), so every test that types needs controllable time.
    jest.useFakeTimers();
    routeStub = { snapshot: { queryParamMap: convertToParamMap({}) } };
    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: routeStub },
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    const router = TestBed.inject(Router);
    jest.spyOn(router, 'navigate').mockResolvedValue(true);
    jest.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
  });

  /** Set a pending returnUrl query param on the injected ActivatedRoute stub. */
  function withReturnUrl(returnUrl: string): void {
    routeStub.snapshot.queryParamMap = convertToParamMap({ returnUrl });
  }

  afterEach(() => {
    httpMock.verify();
    jest.useRealTimers();
  });

  /** The one outstanding team-search request, whatever its query string. */
  function teamSearch(): TestRequest {
    return httpMock.expectOne((r) => r.url === '/api/v1/teams');
  }

  /**
   * ngOnInit fires two independent requests: the profile prefill and the team step's
   * opening list. `teams` is what the opening search resolves to.
   */
  function createComponent(prefill: OwnerProfile = PROFILE, teams: TeamCard[] = [BERLIN]) {
    const fixture = TestBed.createComponent(OnboardingComponent);
    fixture.detectChanges(); // ngOnInit → getMine() + the opening team search
    httpMock.expectOne('/api/v1/profiles/me').flush(prefill);
    teamSearch().flush(page(teams));
    fixture.detectChanges();
    return fixture;
  }

  function api(fixture: ComponentFixture<OnboardingComponent>): OnboardingApi {
    return fixture.componentInstance as unknown as OnboardingApi;
  }

  /** welcome → name → city → pompfen → team. */
  function goToTeamStep(fixture: ComponentFixture<OnboardingComponent>): OnboardingApi {
    const comp = api(fixture);
    for (let i = 0; i < 4; i++) {
      comp.next();
    }
    fixture.detectChanges();
    expect(comp.step()).toBe('team');
    return comp;
  }

  /** Type into the search field and let the debounce elapse. */
  function type(comp: OnboardingApi, value: string): void {
    comp.onTeamQuery(value);
    jest.advanceTimersByTime(300);
  }

  it('prefills the display name from the profile and blocks Continue when empty', () => {
    const fixture = createComponent();
    const comp = api(fixture);

    expect(comp.displayName()).toBe('nik'); // prefilled (defaults to the handle)
    expect(comp.nameEmpty()).toBe(false);

    comp.displayName.set('   ');
    expect(comp.nameEmpty()).toBe(true);

    // The name-step Continue button reflects the gate.
    comp.next(); // welcome → name
    fixture.detectChanges();
    const button = fixture.nativeElement.querySelector(
      '[data-testid="onboarding-continue"]',
    ) as HTMLButtonElement;
    expect(button.disabled).toBe(true);
  });

  it('finish() sends one profile update then marks onboarding complete (no avatar)', () => {
    const fixture = createComponent();
    const comp = api(fixture);

    comp.displayName.set('Nik Berlin');
    comp.onCitySelected(BERLIN_OPTION);
    comp.description.set('Läufer at heart.');
    comp.selectedPompfen.set(['Stab', 'Laeufer']);
    comp.finish();

    const update = httpMock.expectOne('/api/v1/profiles/me');
    expect(update.request.method).toBe('PUT');
    const body = update.request.body as UpdateProfileRequest;
    expect(body).toEqual({
      displayName: 'Nik Berlin',
      location: { cityExternalId: 'TEST:berlin', name: 'Berlin' },
      description: 'Läufer at heart.',
      pompfen: ['Stab', 'Laeufer'],
      isPublic: false,
    });
    update.flush(PROFILE);

    // No avatar was picked → no avatar upload, straight to complete.
    const complete = httpMock.expectOne('/api/v1/profiles/me/onboarding/complete');
    expect(complete.request.method).toBe('POST');
    complete.flush(null);

    expect(comp.step()).toBe('done');
  });

  it('a name-only finish sends null optional fields (skipped steps are not written as blanks)', () => {
    const fixture = createComponent();
    const comp = api(fixture);

    comp.displayName.set('Solo'); // everything else left at its prefilled (empty) default
    comp.finish();

    const update = httpMock.expectOne('/api/v1/profiles/me');
    expect(update.request.body).toEqual({
      displayName: 'Solo',
      location: null,
      description: null,
      pompfen: [],
      isPublic: false,
    });
    update.flush(PROFILE);
    httpMock.expectOne('/api/v1/profiles/me/onboarding/complete').flush(null);

    expect(comp.step()).toBe('done');
  });

  it('dismiss() marks onboarding complete without writing any profile update', () => {
    const fixture = createComponent();
    const comp = api(fixture);

    comp.dismiss();

    // No profile PUT — just complete, then a session refresh on the way out.
    const complete = httpMock.expectOne('/api/v1/profiles/me/onboarding/complete');
    expect(complete.request.method).toBe('POST');
    complete.flush(null);

    // enterApp() re-hydrates the session so the guard sees the completed flag.
    httpMock
      .expectOne('/api/v1/auth/me')
      .flush({ id: 'u1', email: 'a@example.com', emailConfirmed: true, onboardingCompleted: true });
  });

  it('resumes a pending returnUrl after onboarding instead of the dashboard', () => {
    withReturnUrl('/join/berlin-jugger/tok123?action=accept');
    const fixture = createComponent();
    const comp = api(fixture);
    const router = TestBed.inject(Router);

    comp.dismiss();
    httpMock.expectOne('/api/v1/profiles/me/onboarding/complete').flush(null);
    httpMock
      .expectOne('/api/v1/auth/me')
      .flush({ id: 'u1', email: 'a@example.com', emailConfirmed: true, onboardingCompleted: true });

    expect(router.navigateByUrl).toHaveBeenCalledWith('/join/berlin-jugger/tok123?action=accept');
  });

  it('ignores an external returnUrl (open-redirect guard) and enters the app', () => {
    withReturnUrl('https://evil.example.com');
    const fixture = createComponent();
    const comp = api(fixture);
    const router = TestBed.inject(Router);

    comp.dismiss();
    httpMock.expectOne('/api/v1/profiles/me/onboarding/complete').flush(null);
    httpMock
      .expectOne('/api/v1/auth/me')
      .flush({ id: 'u1', email: 'a@example.com', emailConfirmed: true, onboardingCompleted: true });

    expect(router.navigateByUrl).toHaveBeenCalledWith('/');
  });

  // --- Team step: search (feature 029) --------------------------------------

  describe('team step search', () => {
    it('opens on beginners-welcome teams and drops that filter as soon as there is a query', () => {
      const fixture = TestBed.createComponent(OnboardingComponent);
      fixture.detectChanges();
      httpMock.expectOne('/api/v1/profiles/me').flush(PROFILE);

      // Opening list: narrowed to teams that want new players, no query.
      const opening = teamSearch();
      expect(opening.request.params.get('beginnersWelcome')).toBe('true');
      expect(opening.request.params.get('activeOnly')).toBe('true');
      expect(opening.request.params.get('sort')).toBe('NameAsc');
      expect(opening.request.params.has('q')).toBe(false);
      opening.flush(page([BERLIN]));
      fixture.detectChanges();

      // Searching covers every team, so a non-beginners team is findable.
      const comp = api(fixture);
      type(comp, 'hamburg');
      const searched = teamSearch();
      expect(searched.request.params.get('q')).toBe('hamburg');
      expect(searched.request.params.has('beginnersWelcome')).toBe(false);
      searched.flush(page([HAMBURG]));
      fixture.detectChanges();

      expect(comp.teams.items()).toEqual([HAMBURG]);
    });

    it('persists a picked home city on leaving the city step, then orders teams by proximity (FR-013)', () => {
      const fixture = createComponent();
      const comp = api(fixture);

      // welcome → name → city; pick a city, then Continue off the city step.
      comp.next();
      comp.next();
      comp.onCitySelected(BERLIN_OPTION);
      comp.next(); // city → pompfen: persists the home city on its OWN endpoint (not the full profile)

      const save = httpMock.expectOne('/api/v1/profiles/me/home-city');
      expect(save.request.method).toBe('PUT');
      expect(save.request.body).toEqual({ cityExternalId: 'TEST:berlin', name: 'Berlin' });
      save.flush(null);

      // Persisting refreshes the team list, now proximity-ordered (beginners filter dropped).
      const near = teamSearch();
      expect(near.request.params.get('sort')).toBe('Proximity');
      expect(near.request.params.has('beginnersWelcome')).toBe(false);
      near.flush(page([BERLIN]));
    });

    it('a failed home-city save leaves the team step on the default ordering (never blocks)', () => {
      const fixture = createComponent();
      const comp = api(fixture);

      comp.next();
      comp.next();
      comp.onCitySelected(BERLIN_OPTION);
      comp.next(); // city → pompfen

      // The save fails — no proximity reload is issued, and navigation is unaffected.
      httpMock.expectOne('/api/v1/profiles/me/home-city').error(new ProgressEvent('network'));

      comp.next(); // pompfen → team
      fixture.detectChanges();
      expect(comp.step()).toBe('team');
    });

    it('clearing the query returns to the beginners-welcome opening list', () => {
      const fixture = createComponent();
      const comp = goToTeamStep(fixture);

      type(comp, 'hamburg');
      teamSearch().flush(page([HAMBURG]));

      type(comp, '');
      const reopened = teamSearch();
      expect(reopened.request.params.get('beginnersWelcome')).toBe('true');
      expect(reopened.request.params.has('q')).toBe(false);
      reopened.flush(page([BERLIN]));
    });

    it('debounces typing into a single request for the final value', () => {
      const fixture = createComponent();
      const comp = goToTeamStep(fixture);

      comp.onTeamQuery('b');
      comp.onTeamQuery('be');
      comp.onTeamQuery('ber');
      jest.advanceTimersByTime(100); // still inside the debounce window
      httpMock.expectNone((r) => r.url === '/api/v1/teams');

      jest.advanceTimersByTime(200);
      const request = teamSearch();
      expect(request.request.params.get('q')).toBe('ber');
      request.flush(page([BERLIN]));
    });

    it('shows the loading line, not a spinner, while a search is in flight', () => {
      const fixture = TestBed.createComponent(OnboardingComponent);
      fixture.detectChanges();
      httpMock.expectOne('/api/v1/profiles/me').flush(PROFILE);
      const comp = goToTeamStep(fixture); // the opening search is still outstanding

      expect(fixture.nativeElement.querySelector('[data-testid="onboarding-team-loading"]')).toBeTruthy();

      teamSearch().flush(page([BERLIN]));
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('[data-testid="onboarding-team-loading"]')).toBeNull();
      expect(comp.teams.state()).toBe('ready');
    });

    it('tells "no matches" and "we could not load" apart — only the failure offers a retry', () => {
      const fixture = createComponent();
      const comp = goToTeamStep(fixture);

      // No matches for a query: an invitation to try again, no retry button.
      type(comp, 'zzzznotateam');
      teamSearch().flush(page([]));
      fixture.detectChanges();
      expect(comp.teams.state()).toBe('no-results');
      expect(fixture.nativeElement.querySelector('[data-testid="onboarding-team-empty"]')).toBeTruthy();
      expect(fixture.nativeElement.querySelector('[data-testid="onboarding-team-error"]')).toBeNull();

      // A failed search: visibly different, and it offers a way to retry.
      type(comp, 'berlin');
      teamSearch().error(new ProgressEvent('network'));
      fixture.detectChanges();
      expect(comp.teams.state()).toBe('error');
      const error = fixture.nativeElement.querySelector('[data-testid="onboarding-team-error"]');
      expect(error).toBeTruthy();
      expect(error.querySelector('button')).toBeTruthy();
      expect(fixture.nativeElement.querySelector('[data-testid="onboarding-team-empty"]')).toBeNull();
    });

    it('keeps no trace of the feature-004 placeholder', () => {
      const fixture = createComponent();
      goToTeamStep(fixture);

      const search = fixture.nativeElement.querySelector(
        '[data-testid="onboarding-team-search"]',
      ) as HTMLInputElement;
      expect(search.disabled).toBe(false);

      const html = fixture.nativeElement.innerHTML as string;
      expect(html).not.toContain('coming soon');
      expect(html).not.toContain('Team A');
      expect(html).not.toContain('Team B');
    });
  });

  // --- Team step: asking to join --------------------------------------------

  describe('team step join request', () => {
    it('selecting is single-select and, on its own, writes nothing', () => {
      const fixture = createComponent(PROFILE, [BERLIN, HAMBURG]);
      const comp = goToTeamStep(fixture);

      comp.selectTeam(BERLIN);
      comp.selectTeam(HAMBURG); // replaces, never accumulates
      fixture.detectChanges();

      expect(comp.selectedTeam()).toEqual(HAMBURG);
      expect(comp.requestedSlugs().size).toBe(0);
      // The load-bearing part: selecting sent nothing (afterEach verify() proves it).
    });

    it('asking to join posts one request and confirms that approval is still pending', () => {
      const fixture = createComponent();
      const comp = goToTeamStep(fixture);

      comp.selectTeam(BERLIN);
      comp.askToJoin();

      const request = httpMock.expectOne('/api/v1/teams/berlin-jugger/join-requests');
      expect(request.request.method).toBe('POST');
      request.flush(null, { status: 204, statusText: 'No Content' });
      fixture.detectChanges();

      expect(comp.requestedSlugs().has('berlin-jugger')).toBe(true);
      expect(comp.selectedRequested()).toBe(true);

      const confirmation = fixture.nativeElement.querySelector(
        '[data-testid="onboarding-team-confirmation"]',
      );
      expect(confirmation).toBeTruthy();
      // Pending, never granted — the Done screen can't keep a membership promise.
      expect(confirmation.textContent).toContain('an admin still has to say yes');
      expect(confirmation.textContent).not.toContain('joined');

      // The ask action is gone, so the same team cannot be asked twice.
      expect(fixture.nativeElement.querySelector('[data-testid="onboarding-team-ask"]')).toBeNull();
      comp.askToJoin(); // no-op — verify() would fail on a second POST
    });

    it('reports a 409 as "already on that team" and does not retry it', () => {
      const fixture = createComponent();
      const comp = goToTeamStep(fixture);

      comp.selectTeam(BERLIN);
      comp.askToJoin();
      httpMock
        .expectOne('/api/v1/teams/berlin-jugger/join-requests')
        .flush(null, { status: 409, statusText: 'Conflict' });
      fixture.detectChanges();

      expect(comp.teamRequestError()).toBe("You're already on that team.");
      expect(comp.requestedSlugs().size).toBe(0);
      // No second POST: a rejection repeated is still a rejection (constitution VII).
      httpMock.expectNone('/api/v1/teams/berlin-jugger/join-requests');
    });

    it('reports any other failure generically, leaking no status code', () => {
      const fixture = createComponent();
      const comp = goToTeamStep(fixture);

      comp.selectTeam(BERLIN);
      comp.askToJoin();
      httpMock
        .expectOne('/api/v1/teams/berlin-jugger/join-requests')
        .flush(null, { status: 500, statusText: 'Server Error' });
      fixture.detectChanges();

      expect(comp.teamRequestError()).toBe("We couldn't send that request just now.");
      expect(comp.requestedSlugs().size).toBe(0);
      const line = fixture.nativeElement.querySelector(
        '[data-testid="onboarding-team-request-error"]',
      );
      expect(line.textContent).not.toContain('500');
    });
  });

  // --- Team step: it can never trap the player -------------------------------

  describe('team step never blocks onboarding', () => {
    it('advancing past the step issues no request at all — with or without a selection', () => {
      const fixture = createComponent();
      const comp = goToTeamStep(fixture);

      comp.next(); // nothing selected
      expect(comp.step()).toBe('photo');

      comp.back();
      comp.selectTeam(BERLIN);
      comp.next(); // selected but never asked
      expect(comp.step()).toBe('photo');

      // afterEach's httpMock.verify() is the assertion: not one request left the browser.
    });

    it('leaves every exit working after the search fails', () => {
      const fixture = TestBed.createComponent(OnboardingComponent);
      fixture.detectChanges();
      httpMock.expectOne('/api/v1/profiles/me').flush(PROFILE);
      teamSearch().error(new ProgressEvent('network'));
      fixture.detectChanges();

      const comp = goToTeamStep(fixture);
      expect(comp.teams.state()).toBe('error');

      for (const testId of ['onboarding-continue', 'onboarding-skip']) {
        const button = fixture.nativeElement.querySelector(
          `[data-testid="${testId}"]`,
        ) as HTMLButtonElement;
        expect(button.disabled).toBe(false);
      }

      comp.next();
      expect(comp.step()).toBe('photo'); // the flow moves on regardless
    });

    it('lets the player finish onboarding after a failed join request', () => {
      const fixture = createComponent();
      const comp = goToTeamStep(fixture);

      comp.selectTeam(BERLIN);
      comp.askToJoin();
      httpMock
        .expectOne('/api/v1/teams/berlin-jugger/join-requests')
        .flush(null, { status: 500, statusText: 'Server Error' });

      comp.next();
      comp.finish();
      httpMock.expectOne('/api/v1/profiles/me').flush(PROFILE);
      httpMock.expectOne('/api/v1/profiles/me/onboarding/complete').flush(null);

      expect(comp.step()).toBe('done');
    });

    it('keeps the finish payload identical when a team is selected but never asked', () => {
      const fixture = createComponent();
      const comp = goToTeamStep(fixture);

      comp.selectTeam(BERLIN);
      comp.displayName.set('Solo');
      comp.finish();

      const update = httpMock.expectOne('/api/v1/profiles/me');
      expect(update.request.body).toEqual({
        displayName: 'Solo',
        location: null,
        description: null,
        pompfen: [],
        isPublic: false,
      });
      update.flush(PROFILE);
      httpMock.expectOne('/api/v1/profiles/me/onboarding/complete').flush(null);

      expect(comp.step()).toBe('done');
    });
  });
});
