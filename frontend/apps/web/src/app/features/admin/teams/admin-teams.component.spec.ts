import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AdminTeamsComponent } from './admin-teams.component';
import { AdminTeamListItem } from '../../../core/models/admin.models';

const TEAMS: AdminTeamListItem[] = [
  { slug: 'berlin-bison', name: 'Berlin Bison', city: 'Berlin', type: 'CityTeam', memberCount: 18, awardCount: 3 },
  { slug: 'rhein-raptors', name: 'Rhein Raptors', city: 'Köln', type: 'Mixteam', memberCount: 9, awardCount: 0 },
];

describe('AdminTeamsComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  function create(): ComponentFixture<AdminTeamsComponent> {
    const fixture = TestBed.createComponent(AdminTeamsComponent);
    fixture.detectChanges(); // constructor → load()
    const req = httpMock.expectOne((r) => r.url === '/api/v1/admin/teams');
    // Initial load pages from the start with no query.
    expect(req.request.params.get('skip')).toBe('0');
    expect(req.request.params.get('take')).toBe('20');
    expect(req.request.params.get('q')).toBeNull();
    req.flush({ items: TEAMS, totalCount: 2, skip: 0, take: 20 });
    fixture.detectChanges();
    return fixture;
  }

  it('lists teams with their counts', () => {
    const fixture = create();
    const rows = fixture.nativeElement.querySelectorAll('[data-testid="admin-teams-row"]');
    // Desktop table + mobile cards both carry the testid; both render the two teams.
    expect(rows.length).toBeGreaterThanOrEqual(2);
    expect(fixture.nativeElement.textContent).toContain('Berlin Bison');
    expect(fixture.nativeElement.textContent).toContain('Rhein Raptors');
  });
});
