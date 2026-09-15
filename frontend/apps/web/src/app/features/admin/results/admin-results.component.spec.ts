import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AdminPlacement } from '../../../core/models/results.models';
import { translocoLocaleTestingProviders, translocoTestingModule } from '../../../../testing/transloco-testing';
import { AdminResultsComponent } from './admin-results.component';

const LIST = '/api/v1/admin/results/placements';

function placement(id: string, overrides: Partial<AdminPlacement> = {}): AdminPlacement {
  return {
    id,
    eventId: 'e1',
    eventName: '25. Deutsche Meisterschaft',
    eventDate: '2024-09-21',
    position: 3,
    rankedCount: 20,
    sourceName: 'Ecplise',
    name: 'Ecplise',
    team: null,
    connectedBy: null,
    connectedAt: null,
    fromTugeny: true,
    ...overrides,
  };
}

describe('AdminResultsComponent (the placement queue)', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([]), ...translocoLocaleTestingProviders()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function mount(items: AdminPlacement[]): ComponentFixture<AdminResultsComponent> {
    const fixture = TestBed.createComponent(AdminResultsComponent);
    const first = http.expectOne((r) => r.url === LIST);
    expect(first.request.params.get('connected')).toBe('false'); // the work queue is the default
    first.flush({ items, totalCount: items.length, skip: 0, take: 20 });
    fixture.detectChanges();
    return fixture;
  }

  const q = (f: ComponentFixture<AdminResultsComponent>, testId: string) =>
    f.nativeElement.querySelector(`[data-testid="${testId}"]`) as HTMLElement | null;

  it('shows the name as recorded next to where it placed', () => {
    const f = mount([placement('p1')]);

    const row = q(f, 'admin-results-row')!;
    expect(row.textContent).toContain('Ecplise');
    expect(row.textContent).toContain('Place 3 of 20');
    expect(row.textContent).toContain('Imported from Tugeny');
  });

  it('connects exactly the placement it was asked for', () => {
    const f = mount([placement('p1'), placement('p2', { sourceName: 'Ecplise' })]);

    (f.nativeElement.querySelectorAll('[data-testid="admin-results-connect"]')[0] as HTMLButtonElement).click();
    f.detectChanges();
    // The picker searches for the recorded name — a search, not a choice.
    http.expectOne((r) => r.url === '/api/v1/admin/teams').flush({
      items: [{ slug: 'eclipse', name: 'Eclipse', location: 'Basel', type: 'CityTeam', memberCount: 12, awardCount: 0 }],
      totalCount: 1,
      skip: 0,
      take: 10,
    });
    f.detectChanges();
    (q(f, 'pick-team-eclipse') as HTMLButtonElement).click();

    const put = http.expectOne((r) => r.method === 'PUT');
    expect(put.request.url).toBe(`${LIST}/p1/team`);
    expect(put.request.body).toEqual({ teamSlug: 'eclipse' });
    put.flush(placement('p1', { name: 'Eclipse', team: { slug: 'eclipse', name: 'Eclipse' }, connectedBy: 'Jan', connectedAt: '2026-09-15T10:00:00Z' }));
    f.detectChanges();

    http.expectNone((r) => r.method === 'PUT');
    expect(q(f, 'admin-results-connected-to')?.textContent).toContain('Eclipse');
  });

  it('asks before disconnecting', () => {
    const f = mount([placement('p1', { team: { slug: 'eclipse', name: 'Eclipse' }, connectedBy: 'Jan', connectedAt: '2026-09-15T10:00:00Z' })]);

    q(f, 'admin-results-disconnect')!.click();
    f.detectChanges();
    http.expectNone((r) => r.method === 'DELETE');

    q(f, 'admin-results-disconnect-confirm')!.click();
    http.expectOne((r) => r.method === 'DELETE' && r.url === `${LIST}/p1/team`).flush(placement('p1'));
  });
});
