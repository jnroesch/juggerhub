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
import { InvitePreview, MyInvitation } from '../../core/models/team.models';
import { MyTeam } from '../../core/models/home.models';
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
  hasLogo: false,
};

/** Deliberately NOT beginners-welcome — only reachable by searching (FR-003). */
const HAMBURG: TeamCard = {
  slug: 'hamburg-hammers',
  name: 'Hamburg Hammers',
  location: HAMBURG_LOC,
  playerCount: 18,
  beginnersWelcome: false,
  logoInitial: 'H',
  hasLogo: true,
};

function page(items: TeamCard[]): PagedResult<TeamCard> {
  return { items, totalCount: items.length, skip: 0, take: 20 };
}

// --- Feature 053 fixtures ------------------------------------------------------------------

/** 43 base64url chars — the shape a real invite token has. */
const TOKEN = 'Xy9_abcDEF-ghiJKL012mnoPQR345stuVWX678yzAB_';
/** What sign-in carries into the wizard after the invite page bounced a signed-out visitor. */
const INVITE_RETURN = `/join/berlin-jugger/${TOKEN}?action=accept`;

const USABLE: InvitePreview = {
  teamName: 'Berlin Jugger',
  teamSlug: 'berlin-jugger',
  type: 'CityTeam',
  location: 'Berlin, Germany',
  memberCount: 24,
  inviterDisplayName: 'Mara',
  state: 'Usable',
};

const HAMBURG_INVITE: MyInvitation = {
  token: 'targeted-hamburg-token',
  teamName: 'Hamburg Hammers',
  teamSlug: 'hamburg-hammers',
  teamType: 'CityTeam',
  location: 'Hamburg, Germany',
  memberCount: 18,
  inviterDisplayName: 'Jonas',
  createdDate: '2026-09-18T10:00:00Z',
  expiresDate: '2026-09-25T10:00:00Z',
};

/** A targeted invite to the SAME team as the carried link invite — must be shown once, not twice. */
const BERLIN_TARGETED: MyInvitation = {
  ...HAMBURG_INVITE,
  token: 'targeted-berlin-token',
  teamName: 'Berlin Jugger',
  teamSlug: 'berlin-jugger',
  location: 'Berlin, Germany',
  memberCount: 24,
  inviterDisplayName: 'Mara',
};

function invitesPage(items: MyInvitation[]): PagedResult<MyInvitation> {
  return { items, totalCount: items.length, skip: 0, take: 100 };
}

function myTeamsPage(slugs: string[]): PagedResult<MyTeam> {
  return {
    items: slugs.map((slug) => ({ slug, name: slug, role: 'Member' as MyTeam['role'], hasLogo: false })),
    totalCount: slugs.length,
    skip: 0,
    take: 100,
  };
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
  enterApp(): void;
  // Invitations (feature 053)
  joinedSlugs: Signal<readonly string[]>;
  acceptInvite(token: string, teamSlug: string): void;
  declineInvite(token: string): void;
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

  /** The account's addressed-invitations list (feature 053) — fetched on every wizard entry. */
  function invitesList(): TestRequest {
    return httpMock.expectOne((r) => r.url === '/api/v1/profiles/me/invitations');
  }

  /** The memberships read, issued only once an invitation is actually known (feature 053). */
  function membershipsList(): TestRequest {
    return httpMock.expectOne((r) => r.url === '/api/v1/profiles/me/teams');
  }

  /**
   * ngOnInit fires three independent requests: the profile prefill, the team step's opening
   * list, and (feature 053) the account's addressed invitations. `teams` is what the opening
   * search resolves to; `invites` what the list resolves to.
   */
  function createComponent(prefill: OwnerProfile = PROFILE, teams: TeamCard[] = [BERLIN], invites: MyInvitation[] = []) {
    const fixture = TestBed.createComponent(OnboardingComponent);
    fixture.detectChanges(); // ngOnInit → getMine() + the opening team search + listMine()
    httpMock.expectOne('/api/v1/profiles/me').flush(prefill);
    teamSearch().flush(page(teams));
    invitesList().flush(invitesPage(invites));
    if (invites.length > 0) {
      membershipsList().flush(myTeamsPage([]));
    }
    fixture.detectChanges();
    return fixture;
  }

  /**
   * Feature 053 — the wizard entered with a carried invite. Answers the same three init requests
   * plus the invite preview and, when the invite is usable or invites are addressed, the
   * memberships read.
   */
  function createWithInvite(
    preview: InvitePreview | 'notFound',
    invites: MyInvitation[] = [],
    memberOf: string[] = [],
  ): ComponentFixture<OnboardingComponent> {
    withReturnUrl(INVITE_RETURN);
    const fixture = TestBed.createComponent(OnboardingComponent);
    fixture.detectChanges();
    httpMock.expectOne('/api/v1/profiles/me').flush(PROFILE);
    teamSearch().flush(page([BERLIN]));
    invitesList().flush(invitesPage(invites));
    const previewReq = httpMock.expectOne(`/api/v1/invitations/${TOKEN}`);
    if (preview === 'notFound') {
      previewReq.flush({ title: 'Invite not found' }, { status: 404, statusText: 'Not Found' });
    } else {
      previewReq.flush(preview);
    }
    if ((preview !== 'notFound' && preview.state === 'Usable') || invites.length > 0) {
      membershipsList().flush(myTeamsPage(memberOf));
    }
    fixture.detectChanges();
    return fixture;
  }

  function el(fixture: ComponentFixture<OnboardingComponent>, testId: string): HTMLElement | null {
    return fixture.nativeElement.querySelector(`[data-testid="${testId}"]`);
  }

  /** Enter the app from wherever the wizard is and return where it navigated. */
  function exitVia(fixture: ComponentFixture<OnboardingComponent>, run: () => void): string {
    run();
    httpMock
      .expectOne('/api/v1/auth/me')
      .flush({ id: 'u1', email: 'a@example.com', emailConfirmed: true, onboardingCompleted: true });
    const router = TestBed.inject(Router);
    const calls = (router.navigateByUrl as jest.Mock).mock.calls;
    return calls[calls.length - 1][0] as string;
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

  it('holds the city step Continue while suggestions load — and never holds Skip', () => {
    const fixture = createComponent();
    const comp = api(fixture);
    comp.next(); // welcome → name
    comp.next(); // name → city
    fixture.detectChanges();

    const button = (testid: string) =>
      fixture.nativeElement.querySelector(`[data-testid="${testid}"]`) as HTMLButtonElement;
    expect(button('onboarding-continue').disabled).toBe(false);

    const cityInput = fixture.nativeElement.querySelector(
      '[data-testid="city-picker-input"]',
    ) as HTMLInputElement;
    cityInput.value = 'Ham';
    cityInput.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(button('onboarding-continue').disabled).toBe(true); // already during the debounce
    expect(button('onboarding-skip').disabled).toBe(false);

    jest.advanceTimersByTime(300);
    const search = httpMock.expectOne((r) => r.url === '/api/v1/cities/search');
    fixture.detectChanges();
    expect(button('onboarding-continue').disabled).toBe(true);

    search.flush([{ ...HAMBURG_LOC, latitude: 53.55, longitude: 9.99 }]);
    fixture.detectChanges();
    expect(button('onboarding-continue').disabled).toBe(false);
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
      invitesList().flush(invitesPage([]));

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
      invitesList().flush(invitesPage([]));
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
      expect(confirmation.textContent).toContain('An admin still has to say yes');
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
      invitesList().flush(invitesPage([]));
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

  // --- Team step: invitations (feature 053) ---------------------------------

  describe('team step invitation (feature 053)', () => {
    it('leads with the carried invite and offers a secondary Accept above the search', () => {
      const fixture = createWithInvite(USABLE);
      goToTeamStep(fixture);

      const card = el(fixture, 'onboarding-invite');
      expect(card?.textContent).toContain('Berlin Jugger');
      expect(card?.textContent).toContain('Mara');
      expect(card?.textContent).toContain('24');
      const accept = el(fixture, 'onboarding-invite-accept') as HTMLButtonElement;
      expect(accept).not.toBeNull();
      expect(accept.getAttribute('variant')).toBe('secondary');
      expect(accept.textContent).toContain('Accept & join Berlin Jugger');
      // The 029 search is still there, framed as the other path.
      expect(el(fixture, 'onboarding-team-search')).not.toBeNull();
      expect(fixture.nativeElement.textContent).toContain('Or look for another team');
    });

    it('accepting posts once, confirms immediate membership, and makes the team the exit', () => {
      const fixture = createWithInvite(USABLE);
      const comp = goToTeamStep(fixture);

      comp.acceptInvite(TOKEN, 'berlin-jugger');
      httpMock.expectOne(`/api/v1/invitations/${TOKEN}/accept`).flush({ teamSlug: 'berlin-jugger' });
      membershipsList().flush(myTeamsPage(['berlin-jugger']));
      fixture.detectChanges();

      const joined = el(fixture, 'onboarding-invite-joined');
      expect(joined?.textContent).toContain("You're on Berlin Jugger now");
      expect(joined?.textContent).not.toMatch(/admin/i);
      expect(el(fixture, 'onboarding-invite-accept')).toBeNull();

      // A second press is a no-op — afterEach's verify() would catch a stray POST.
      comp.acceptInvite(TOKEN, 'berlin-jugger');

      comp.next();
      expect(exitVia(fixture, () => comp.enterApp())).toBe('/t/berlin-jugger');
    });

    it('keeps the joined state across Back and forward', () => {
      const fixture = createWithInvite(USABLE);
      const comp = goToTeamStep(fixture);
      comp.acceptInvite(TOKEN, 'berlin-jugger');
      httpMock.expectOne(`/api/v1/invitations/${TOKEN}/accept`).flush({ teamSlug: 'berlin-jugger' });
      membershipsList().flush(myTeamsPage(['berlin-jugger']));

      comp.back();
      comp.next();
      fixture.detectChanges();

      expect(el(fixture, 'onboarding-invite-joined')).not.toBeNull();
      expect(el(fixture, 'onboarding-invite-accept')).toBeNull();
    });

    it('finishing without accepting lands on the invite page WITHOUT the automatic accept', () => {
      const fixture = createWithInvite(USABLE);
      const comp = goToTeamStep(fixture);
      comp.next(); // walked past the offer without pressing

      expect(exitVia(fixture, () => comp.enterApp())).toBe(`/join/berlin-jugger/${TOKEN}`);
    });

    it('dismissing at Welcome keeps the pre-sign-in accept, exactly as before', () => {
      const fixture = createWithInvite(USABLE);
      const comp = api(fixture);

      const target = exitVia(fixture, () => {
        comp.dismiss();
        httpMock.expectOne('/api/v1/profiles/me/onboarding/complete').flush(null);
      });

      expect(target).toBe(INVITE_RETURN);
    });

    it('advancing past the step issues no request, invite or not', () => {
      const fixture = createWithInvite(USABLE);
      const comp = goToTeamStep(fixture);

      comp.next();
      expect(comp.step()).toBe('photo');
      comp.back();
      comp.next();
      expect(comp.step()).toBe('photo');
      // afterEach's httpMock.verify() is the assertion.
    });

    it('with no carried invite: no preview, no memberships read, no invite markup', () => {
      const fixture = createComponent();
      httpMock.expectNone((r) => r.url.startsWith('/api/v1/invitations/'));
      httpMock.expectNone((r) => r.url === '/api/v1/profiles/me/teams');
      goToTeamStep(fixture);

      expect(fixture.nativeElement.querySelector('[data-testid^="onboarding-invite"]')).toBeNull();
      expect(fixture.nativeElement.textContent).not.toContain('Or look for another team');
    });
  });

  describe('stale and failed invites never block the wizard (feature 053)', () => {
    it('an expired invite is a short note above a working search, and every exit still works', () => {
      const fixture = createWithInvite({ ...USABLE, state: 'Expired' });
      const comp = goToTeamStep(fixture);

      expect(el(fixture, 'onboarding-invite-expired')?.textContent).toContain('has expired');
      expect(el(fixture, 'onboarding-invite')).toBeNull();
      expect(el(fixture, 'onboarding-team-search')).not.toBeNull();

      comp.next();
      expect(comp.step()).toBe('photo');
      comp.back();
      expect(comp.step()).toBe('team');
      comp.back();
      expect(comp.step()).toBe('pompfen');
    });

    it('a revoked or unknown invite reads as "no longer valid"', () => {
      for (const preview of [{ ...USABLE, state: 'Invalid' } as InvitePreview, 'notFound' as const]) {
        const fixture = createWithInvite(preview);
        goToTeamStep(fixture);
        expect(el(fixture, 'onboarding-invite-invalid')?.textContent).toContain('no longer valid');
        expect(el(fixture, 'onboarding-invite-expired')).toBeNull();
        expect(el(fixture, 'onboarding-invite')).toBeNull();
        expect(el(fixture, 'onboarding-team-search')).not.toBeNull();
      }
    });

    it('a malformed reference is the plain step: no preview request, no note', () => {
      withReturnUrl(`/join/Bad_Slug/${TOKEN}`);
      const fixture = createComponent();
      httpMock.expectNone((r) => r.url.startsWith('/api/v1/invitations/'));
      goToTeamStep(fixture);

      expect(fixture.nativeElement.querySelector('[data-testid^="onboarding-invite"]')).toBeNull();
    });

    it('a failed accept is reported, stays pressable, and is never retried on its own', () => {
      const fixture = createWithInvite(USABLE);
      const comp = goToTeamStep(fixture);

      comp.acceptInvite(TOKEN, 'berlin-jugger');
      httpMock
        .expectOne(`/api/v1/invitations/${TOKEN}/accept`)
        .flush({ title: 'boom' }, { status: 500, statusText: 'Server Error' });
      fixture.detectChanges();

      expect(el(fixture, 'onboarding-invite-error')?.textContent).toContain("couldn't join");
      const accept = el(fixture, 'onboarding-invite-accept') as HTMLButtonElement;
      expect(accept).not.toBeNull();
      expect(accept.disabled).toBe(false);
      expect(comp.step()).toBe('team');

      // The player presses again: exactly one more request.
      comp.acceptInvite(TOKEN, 'berlin-jugger');
      httpMock.expectOne(`/api/v1/invitations/${TOKEN}/accept`).flush({ teamSlug: 'berlin-jugger' });
      membershipsList().flush(myTeamsPage(['berlin-jugger']));
      fixture.detectChanges();
      expect(el(fixture, 'onboarding-invite-joined')).not.toBeNull();
    });

    it('a team the player already belongs to shows no Accept and is not an error', () => {
      const fixture = createWithInvite(USABLE, [], ['berlin-jugger']);
      goToTeamStep(fixture);

      expect(el(fixture, 'onboarding-invite-member')?.textContent).toContain("already on Berlin Jugger");
      expect(el(fixture, 'onboarding-invite-accept')).toBeNull();
      expect(el(fixture, 'onboarding-invite-error')).toBeNull();
    });

    it('a stale carried invite exits to the dashboard, not the invite page', () => {
      const fixture = createWithInvite({ ...USABLE, state: 'Expired' });
      const comp = goToTeamStep(fixture);
      comp.next();

      expect(exitVia(fixture, () => comp.enterApp())).toBe('/');
    });
  });

  describe('team step addressed invitations (feature 053)', () => {
    it('lists usable invitations addressed to the account with Accept and Decline', () => {
      const fixture = createComponent(PROFILE, [BERLIN], [HAMBURG_INVITE]);
      goToTeamStep(fixture);

      const row = el(fixture, 'onboarding-invite-hamburg-hammers');
      expect(row?.textContent).toContain('Hamburg Hammers');
      expect(row?.textContent).toContain('Jonas');
      expect(el(fixture, 'onboarding-invite-accept-hamburg-hammers')).not.toBeNull();
      expect(el(fixture, 'onboarding-invite-decline-hamburg-hammers')).not.toBeNull();
      expect(fixture.nativeElement.textContent).toContain('Or look for another team');
    });

    it('shows one offer per team: the carried invite leads and hides an addressed one for the same team', () => {
      const fixture = createWithInvite(USABLE, [HAMBURG_INVITE, BERLIN_TARGETED]);
      goToTeamStep(fixture);

      expect(el(fixture, 'onboarding-invite')).not.toBeNull();
      expect(el(fixture, 'onboarding-invite-hamburg-hammers')).not.toBeNull();
      expect(el(fixture, 'onboarding-invite-berlin-jugger')).toBeNull();
    });

    it('declining removes the row — also when the invite is already gone server-side', () => {
      const fixture = createComponent(PROFILE, [BERLIN], [HAMBURG_INVITE, BERLIN_TARGETED]);
      const comp = goToTeamStep(fixture);

      comp.declineInvite(HAMBURG_INVITE.token);
      httpMock.expectOne(`/api/v1/invitations/${HAMBURG_INVITE.token}/decline`).flush(null, { status: 204, statusText: 'No Content' });
      fixture.detectChanges();
      expect(el(fixture, 'onboarding-invite-hamburg-hammers')).toBeNull();

      comp.declineInvite(BERLIN_TARGETED.token);
      httpMock
        .expectOne(`/api/v1/invitations/${BERLIN_TARGETED.token}/decline`)
        .flush({ title: 'Invite not found' }, { status: 404, statusText: 'Not Found' });
      fixture.detectChanges();
      expect(el(fixture, 'onboarding-invite-berlin-jugger')).toBeNull();
      expect(el(fixture, 'onboarding-invites')).toBeNull();
    });

    it('accepting an addressed invite joins, confirms in place, and becomes the exit', () => {
      const fixture = createComponent(PROFILE, [BERLIN], [HAMBURG_INVITE]);
      const comp = goToTeamStep(fixture);

      comp.acceptInvite(HAMBURG_INVITE.token, 'hamburg-hammers');
      httpMock.expectOne(`/api/v1/invitations/${HAMBURG_INVITE.token}/accept`).flush({ teamSlug: 'hamburg-hammers' });
      membershipsList().flush(myTeamsPage(['hamburg-hammers']));
      fixture.detectChanges();

      expect(el(fixture, 'onboarding-invite-joined-hamburg-hammers')).not.toBeNull();
      expect(el(fixture, 'onboarding-invite-accept-hamburg-hammers')).toBeNull();
      comp.next();
      expect(exitVia(fixture, () => comp.enterApp())).toBe('/t/hamburg-hammers');
    });

    it('a failed list is an empty list — no error, search intact', () => {
      const fixture = TestBed.createComponent(OnboardingComponent);
      fixture.detectChanges();
      httpMock.expectOne('/api/v1/profiles/me').flush(PROFILE);
      teamSearch().flush(page([BERLIN]));
      invitesList().error(new ProgressEvent('network'));
      fixture.detectChanges();
      goToTeamStep(fixture);

      expect(el(fixture, 'onboarding-invites')).toBeNull();
      expect(el(fixture, 'onboarding-invite-error')).toBeNull();
      expect(el(fixture, 'onboarding-team-search')).not.toBeNull();
      expect(el(fixture, 'onboarding-team-row')).not.toBeNull();
    });
  });
});
