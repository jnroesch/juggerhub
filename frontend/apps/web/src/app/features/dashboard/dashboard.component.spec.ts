import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Home } from '../../core/models/home.models';
import { DashboardComponent } from './dashboard.component';
import { translocoTestingModule } from '../../../testing/transloco-testing';

const EMPTY: Omit<Home, 'viewer' | 'teams'> = {
  needsYou: [],
  upNext: [],
  openToEveryone: [],
  news: [],
  activity: [],
};

describe('DashboardComponent', () => {
  let httpMock: HttpTestingController;

  function mount(): ComponentFixture<DashboardComponent> {
    const fixture = TestBed.createComponent(DashboardComponent);
    fixture.detectChanges(); // triggers ngOnInit → getHome()
    return fixture;
  }

  const q = (f: ComponentFixture<DashboardComponent>, id: string) =>
    f.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLElement | null;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([])],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('shows the team-member variant with a "Hi" greeting when the player has a team', () => {
    const f = mount();
    const home: Home = {
      viewer: { displayName: 'Mira', handle: 'mira', hasAvatar: false },
      teams: [{ slug: 'bloodhounds', name: 'Bloodhounds', role: 'Admin' }],
      ...EMPTY,
    };
    httpMock.expectOne('/api/v1/home').flush(home);
    f.detectChanges();
    // The reshaped home (feature 025) fetches only the composite — the removed market/trainings
    // rail modules no longer make their own requests; Needs-you/activity render from inputs.

    expect(q(f, 'home-greeting')!.textContent).toContain('Hi Mira');
    expect(q(f, 'up-next')).toBeTruthy();
    expect(q(f, 'find-a-team')).toBeNull();
  });

  it('shows the new-player variant with find-a-team when the player has no team', () => {
    const f = mount();
    const home: Home = {
      viewer: { displayName: 'Mira', handle: 'mira', hasAvatar: false },
      teams: [],
      ...EMPTY,
    };
    httpMock.expectOne('/api/v1/home').flush(home);
    f.detectChanges();

    expect(q(f, 'home-greeting')!.textContent).toContain('Welcome, Mira');
    expect(q(f, 'find-a-team')).toBeTruthy();
    expect(q(f, 'up-next')).toBeNull();
  });

  it('shows a retry on load failure without throwing', () => {
    const f = mount();
    httpMock.expectOne('/api/v1/home').flush('nope', { status: 500, statusText: 'Server Error' });
    f.detectChanges();
    expect(q(f, 'home-error')).toBeTruthy();
  });

  it('explains a join request that no longer waits, and refreshes so the greeting stops counting it (feature 058)', () => {
    const f = mount();
    const viewer = { displayName: 'Mira', handle: 'mira', hasAvatar: false };
    const teams = [{ slug: 'hh', name: 'Hamburg Hammers', role: 'Admin' as const, hasLogo: false }];
    httpMock.expectOne('/api/v1/home').flush({
      viewer,
      teams,
      ...EMPTY,
      needsYou: [
        {
          kind: 'JoinRequest',
          id: 'req-1',
          params: { teamName: 'Hamburg Hammers', teamSlug: 'hh', eventName: null, playerName: 'Jonas' },
          linkTarget: 'jonas',
          occurredAt: '2026-09-28T08:00:00Z',
        },
      ],
    });
    f.detectChanges();
    expect(q(f, 'home-greeting')?.textContent).toContain('1 thing');

    (f.nativeElement.querySelector('[data-testid="needs-you"] li button') as HTMLButtonElement).click();
    httpMock.expectOne('/api/v1/teams/hh/join-requests/req-1/approve').flush(null, { status: 404, statusText: 'Not Found' });
    httpMock.expectOne('/api/v1/home').flush({ viewer, teams, ...EMPTY });
    f.detectChanges();

    // The card went with its last item; the explanation did not, and nothing counts it any more.
    expect(q(f, 'needs-you')).toBeNull();
    expect(q(f, 'needs-you-notice')?.textContent?.trim()).toBe('This request was already answered or withdrawn.');
    expect(q(f, 'home-greeting')?.textContent).not.toContain('1 thing');
  });

  it('says to try again later when accepting an invite meets the joining limit, and keeps the item (feature 064)', () => {
    const f = mount();
    const viewer = { displayName: 'Mira', handle: 'mira', hasAvatar: false };
    httpMock.expectOne('/api/v1/home').flush({
      viewer,
      teams: [],
      ...EMPTY,
      needsYou: [
        {
          kind: 'TeamInvite',
          id: 'tok-1',
          params: { teamName: 'Rheinfeuer', teamSlug: 'rheinfeuer', eventName: null, playerName: 'Mara' },
          linkTarget: 'rheinfeuer',
          occurredAt: '2026-09-28T08:00:00Z',
        },
      ],
    });
    f.detectChanges();

    (f.nativeElement.querySelector('[data-testid="needs-you"] li button') as HTMLButtonElement).click();
    httpMock.expectOne('/api/v1/invitations/tok-1/accept').flush(null, { status: 429, statusText: 'Too Many Requests' });
    f.detectChanges();

    // No refresh (verify() would catch a second /home), the item stays, and the note says why.
    expect(q(f, 'needs-you-notice')?.textContent?.trim()).toBe(
      "You've joined a lot of teams in a short time. Try again in a while.",
    );
    expect(q(f, 'needs-you')).not.toBeNull();
  });
});
