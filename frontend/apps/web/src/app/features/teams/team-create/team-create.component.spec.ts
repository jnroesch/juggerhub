import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
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

  /** The step's forward control. Named for what it is on every step but the review. */
  function nextBtn(): HTMLButtonElement {
    return el<HTMLButtonElement>('team-next');
  }

  function createBtn(): HTMLButtonElement {
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

  /** Step 1, answered and cleared, leaving the wizard on the type step. */
  function passBasics(): void {
    fillNameAndHandle();
    slugRequest().flush({ slug: 'kiel-krakens', normalized: 'kiel-krakens', available: true, reason: null });
    fixture.detectChanges();
    nextBtn().click();
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

  /** Walks a city team all the way to the review step. */
  function reachReview(): void {
    passBasics();
    pickCity();
    nextBtn().click();
    fixture.detectChanges();
  }

  /** The membership-cache refresh the component fires after a successful create/upload. */
  function flushMembershipRefresh(): void {
    httpMock.match((r) => r.url.startsWith('/api/v1/')).forEach((r) => r.flush({}));
  }

  // --- Step 1: name & handle ------------------------------------------------

  /**
   * The reported defect this screen already carried, now on a step boundary: with every other
   * field filled, the control went live while the handle check was still in flight — so a handle
   * the check was about to refuse could be sent, and creation failed on the server instead.
   */
  it('holds the first step while the handle check is still running', () => {
    fillNameAndHandle();

    const request = slugRequest();
    expect(nextBtn().disabled).toBe(true);

    request.flush({ slug: 'kiel-krakens', normalized: 'kiel-krakens', available: true, reason: null });
    fixture.detectChanges();
    expect(nextBtn().disabled).toBe(false);
  });

  /** The debounce window is the same hazard: nothing has been asked yet, so nothing is known. */
  it('holds the first step before the debounce has fired', () => {
    type('team-name', 'Kiel Krakens');
    type('team-slug', 'kiel-krakens');

    expect(nextBtn().disabled).toBe(true);

    jest.advanceTimersByTime(300);
    slugRequest().flush({ slug: 'kiel-krakens', normalized: 'kiel-krakens', available: true, reason: null });
    fixture.detectChanges();
    expect(nextBtn().disabled).toBe(false);
  });

  it('re-blocks the first step when the handle is edited after a positive check', () => {
    fillNameAndHandle();
    slugRequest().flush({ slug: 'kiel-krakens', normalized: 'kiel-krakens', available: true, reason: null });
    fixture.detectChanges();
    expect(nextBtn().disabled).toBe(false);

    type('team-slug', 'kiel-krakens-2');
    expect(nextBtn().disabled).toBe(true);

    jest.advanceTimersByTime(300);
    slugRequest().flush({ slug: 'kiel-krakens-2', normalized: 'kiel-krakens-2', available: false, reason: 'Taken' });
    fixture.detectChanges();
    expect(nextBtn().disabled).toBe(true);
  });

  /**
   * A failed check leaves availability unknown, so the step stays blocked — which makes it
   * essential that the failure is both said out loud and recoverable. An error reaching the
   * subscriber would tear the subscription down and no later keystroke would ever be checked.
   */
  it('says the check failed and still checks the next handle typed', () => {
    fillNameAndHandle();
    slugRequest().error(new ProgressEvent('network error'));
    fixture.detectChanges();

    expect(nextBtn().disabled).toBe(true);
    expect(el('slug-check-failed')).not.toBeNull();

    type('team-slug', 'kiel-krakens-2');
    jest.advanceTimersByTime(300);
    slugRequest().flush({ slug: 'kiel-krakens-2', normalized: 'kiel-krakens-2', available: true, reason: null });
    fixture.detectChanges();

    expect(el('slug-check-failed')).toBeNull();
    expect(nextBtn().disabled).toBe(false);
  });

  // --- Step 2: type & city --------------------------------------------------

  /** A city team without a city is a refusal the server would make; don't let the step pass. */
  it('holds the type step until a city team has a city', () => {
    passBasics();

    expect(el('team-city')).not.toBeNull();
    expect(nextBtn().disabled).toBe(true);
    expect(el('city-required')).not.toBeNull();

    pickCity();
    expect(nextBtn().disabled).toBe(false);
  });

  /** A Mixteam has no city, so the city gate must not apply to it. */
  it('lets a Mixteam past the type step with no city', () => {
    passBasics();
    el('type-mix').click();
    fixture.detectChanges();

    expect(el('team-city')).toBeNull();
    expect(nextBtn().disabled).toBe(false);
  });

  // --- Moving between steps -------------------------------------------------

  it('keeps every answer when stepping back and forward again', () => {
    passBasics();
    pickCity();

    el('team-back').click();
    fixture.detectChanges();

    expect(el<HTMLInputElement>('team-name').value).toBe('Kiel Krakens');
    expect(el<HTMLInputElement>('team-slug').value).toBe('kiel-krakens');

    nextBtn().click();
    fixture.detectChanges();
    // The city survived the round trip, so the step is still passable.
    expect(nextBtn().disabled).toBe(false);
  });

  it('shows every answer on the review step', () => {
    reachReview();

    expect(el('review-name').textContent).toContain('Kiel Krakens');
    expect(el('review-slug').textContent).toContain('kiel-krakens');
    expect(el('review-city').textContent).toContain('Kiel');
    expect(createBtn()).not.toBeNull();
  });

  it('returns to the step that owns an answer from the review', () => {
    reachReview();

    el('review-edit-name').click();
    fixture.detectChanges();

    expect(el('team-name')).not.toBeNull();
    expect(el('team-review')).toBeNull();
  });

  // --- Creating -------------------------------------------------------------

  /**
   * FR-027 — the team the wizard creates must be indistinguishable from one created by the
   * single screen it replaces, and the payload is what makes that structural.
   */
  it('sends the same payload the single-screen form sent, and moves on to the logo step', () => {
    reachReview();

    createBtn().click();

    const request = httpMock.expectOne('/api/v1/teams');
    expect(request.request.body).toMatchObject({
      name: 'Kiel Krakens',
      slug: 'kiel-krakens',
      type: 'CityTeam',
    });
    expect(request.request.body.location).not.toBeNull();
    request.flush({ id: 't1', slug: 'kiel-krakens' });
    flushMembershipRefresh();
    fixture.detectChanges();

    expect(el('team-logo-step')).not.toBeNull();
  });

  /**
   * FR-009 — the handle was taken between the check and the create. The server makes this
   * structural: SlugTaken is the only 409. The wizard must hand the player back the step that
   * owns the handle, with everything else intact.
   */
  it('returns to the handle step on a 409 and keeps the other answers', () => {
    reachReview();

    createBtn().click();
    httpMock
      .expectOne('/api/v1/teams')
      .flush({ title: 'Team address taken' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();

    // Back on the first step, with the name still there and the refusal shown.
    expect(el<HTMLInputElement>('team-name').value).toBe('Kiel Krakens');
    expect(el('slug-bad')).not.toBeNull();
    // And held there: the stale "available" verdict must not survive the refusal.
    expect(nextBtn().disabled).toBe(true);

    // The city is still remembered — stepping forward again does not ask for it twice.
    type('team-slug', 'kiel-krakens-2');
    jest.advanceTimersByTime(300);
    slugRequest().flush({ slug: 'kiel-krakens-2', normalized: 'kiel-krakens-2', available: true, reason: null });
    fixture.detectChanges();
    nextBtn().click();
    fixture.detectChanges();
    expect(nextBtn().disabled).toBe(false);
  });

  /** Any other refusal is not about the handle, so it must not move the player anywhere. */
  it('stays on the review for a 400 and lets the create be pressed again', () => {
    reachReview();

    createBtn().click();
    httpMock
      .expectOne('/api/v1/teams')
      .flush({ detail: 'Invalid team' }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();

    expect(el('team-review')).not.toBeNull();
    expect(el('team-create-error')).not.toBeNull();
    expect(createBtn().disabled).toBe(false);
  });

  /**
   * Principle VII — a mutation on the browser hop is never retried automatically. A replayed
   * POST would create a second team.
   */
  it('does not retry a failed create by itself', () => {
    reachReview();

    createBtn().click();
    httpMock
      .expectOne('/api/v1/teams')
      .flush({ detail: 'nope' }, { status: 500, statusText: 'Server Error' });
    jest.advanceTimersByTime(30_000);
    fixture.detectChanges();

    httpMock.expectNone('/api/v1/teams');
  });

  // --- After the team exists ------------------------------------------------

  /** Walks all the way through a successful create, landing on the logo step. */
  function createTeam(): void {
    reachReview();
    createBtn().click();
    httpMock.expectOne('/api/v1/teams').flush({ id: 't1', slug: 'kiel-krakens' });
    flushMembershipRefresh();
    fixture.detectChanges();
  }

  function pickLogo(): void {
    const input = el<HTMLInputElement>('wizard-logo-input');
    Object.defineProperty(input, 'files', {
      value: [new File(['x'], 'crest.png', { type: 'image/png' })],
      configurable: true,
    });
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  }

  /**
   * FR-011 — the handle is immutable once created and the rest is settings now, so there is
   * nothing behind Back to return to. It is absent, not merely disabled.
   */
  it('renders no back control once the team exists', () => {
    createTeam();
    expect(el('team-back')).toBeNull();

    el('team-logo-next').click();
    fixture.detectChanges();
    expect(el('team-back')).toBeNull();
  });

  it('uploads a chosen logo immediately and shows it applied', () => {
    createTeam();
    expect(el('wizard-logo-preview')).toBeNull();

    pickLogo();
    const upload = httpMock.expectOne('/api/v1/teams/kiel-krakens/logo');
    expect(upload.request.method).toBe('PUT');
    upload.flush(null, { status: 204, statusText: 'No Content' });
    flushMembershipRefresh();
    fixture.detectChanges();

    expect(el('wizard-logo-preview')).not.toBeNull();
  });

  /** FR-018 — a refused upload leaves the team untouched and both routes forward open. */
  it('reports a refused logo and leaves the step usable', () => {
    createTeam();
    pickLogo();
    httpMock
      .expectOne('/api/v1/teams/kiel-krakens/logo')
      .flush({ detail: 'Too large' }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();

    expect(el('team-create-error')).not.toBeNull();
    expect(el('team-logo-step')).not.toBeNull();
    expect(el<HTMLButtonElement>('team-logo-next').disabled).toBe(false);
  });

  /** FR-019 — skipping uploads nothing at all. */
  it('uploads nothing when the logo step is skipped', () => {
    createTeam();

    el('team-logo-next').click();
    fixture.detectChanges();

    httpMock.expectNone('/api/v1/teams/kiel-krakens/logo');
    expect(el('team-invite-step')).not.toBeNull();
  });

  it('offers the invite search for the created team, and finishes on its page', () => {
    const router = TestBed.inject(Router);
    const navigate = jest.spyOn(router, 'navigate').mockResolvedValue(true);

    createTeam();
    el('team-logo-next').click();
    fixture.detectChanges();

    expect(el('user-search')).not.toBeNull();

    el('team-finish').click();
    fixture.detectChanges();

    expect(navigate).toHaveBeenCalledWith(['/t', 'kiel-krakens']);
  });

  /**
   * The whole wizard is one `<form>`, so Enter in the name field would otherwise submit it and
   * create a team from the first screen — before the type, the city or the review were seen.
   */
  it('advances rather than creating when the form is submitted from an early step', () => {
    fillNameAndHandle();
    slugRequest().flush({ slug: 'kiel-krakens', normalized: 'kiel-krakens', available: true, reason: null });
    fixture.detectChanges();

    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    httpMock.expectNone('/api/v1/teams');
    expect(el('team-city')).not.toBeNull();
  });
});
