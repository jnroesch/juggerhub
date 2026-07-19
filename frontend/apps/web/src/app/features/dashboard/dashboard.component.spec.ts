import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Home } from '../../core/models/home.models';
import { DashboardComponent } from './dashboard.component';

const EMPTY: Omit<Home, 'viewer' | 'teams'> = {
  upNext: [],
  openToEveryone: [],
  teamsActivity: [],
  news: [],
  tournaments: [],
  snapshots: [],
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
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
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
    // The market module (feature 017) mounts with the loaded dashboard and fetches its summary.
    httpMock.expectOne((r) => r.url === '/api/v1/market/mine').flush({ items: [], totalCount: 0, skip: 0, take: 20 });
    httpMock.expectOne((r) => r.url === '/api/v1/market/mine/listings').flush({ items: [], totalCount: 0, skip: 0, take: 20 });
    // …and so does the "Your trainings" agenda (feature 018).
    httpMock.expectOne((r) => r.url === '/api/v1/me/trainings').flush({ items: [], totalCount: 0, skip: 0, take: 10 });

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
    httpMock.expectOne((r) => r.url === '/api/v1/market/mine').flush({ items: [], totalCount: 0, skip: 0, take: 20 });
    httpMock.expectOne((r) => r.url === '/api/v1/market/mine/listings').flush({ items: [], totalCount: 0, skip: 0, take: 20 });
    // The "Your trainings" agenda (feature 018) loads for a team-less player too.
    httpMock.expectOne((r) => r.url === '/api/v1/me/trainings').flush({ items: [], totalCount: 0, skip: 0, take: 10 });

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
});
