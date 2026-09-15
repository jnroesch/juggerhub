import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TeamPlacement } from '../../../../core/models/results.models';
import { translocoLocaleTestingProviders, translocoTestingModule } from '../../../../../testing/transloco-testing';
import { TeamPlacementsComponent } from './team-placements.component';

const row = (eventId: string, position = 3, rankedCount = 24): TeamPlacement => ({
  eventId,
  eventName: `Cup ${eventId}`,
  date: '2026-05-02',
  position,
  rankedCount,
});

describe('TeamPlacementsComponent (a team’s tournament results)', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([]), ...translocoLocaleTestingProviders()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function mount(): ComponentFixture<TeamPlacementsComponent> {
    const fixture = TestBed.createComponent(TeamPlacementsComponent);
    fixture.componentRef.setInput('slug', 'rigor-mortis');
    fixture.detectChanges();
    return fixture;
  }

  const page = (items: TeamPlacement[], totalCount: number, skip = 0) => ({ items, totalCount, skip, take: 10 });
  const rows = (f: ComponentFixture<TeamPlacementsComponent>) =>
    Array.from(f.nativeElement.querySelectorAll('[data-testid="placement-row"]')) as HTMLElement[];

  it('lists each placement as "place N of M", linking to the tournament', () => {
    const f = mount();
    http.expectOne((r) => r.url === '/api/v1/teams/rigor-mortis/placements').flush(page([row('e1', 1, 20)], 1));
    f.detectChanges();

    expect(rows(f)).toHaveLength(1);
    expect(rows(f)[0].textContent).toContain('Place 1 of 20');
    expect(rows(f)[0].querySelector('a')?.getAttribute('href')).toBe('/events/e1');
  });

  it('says so when the team has no results — and shows an error, not that, when loading fails', () => {
    const empty = mount();
    http.expectOne((r) => r.url.includes('/placements')).flush(page([], 0));
    empty.detectChanges();
    expect(empty.nativeElement.textContent).toContain('No tournament results recorded');

    const failed = mount();
    http.expectOne((r) => r.url.includes('/placements')).flush('x', { status: 500, statusText: 'Error' });
    failed.detectChanges();
    expect(failed.nativeElement.textContent).not.toContain('No tournament results recorded');
    expect(failed.nativeElement.querySelector('jh-alert')).not.toBeNull();
  });

  it('appends older results on request', () => {
    const f = mount();
    http.expectOne((r) => r.url.includes('/placements')).flush(page([row('e1'), row('e2')], 3));
    f.detectChanges();

    (f.nativeElement.querySelector('[data-testid="placements-more"]') as HTMLButtonElement).click();
    const next = http.expectOne((r) => r.url.includes('/placements'));
    expect(next.request.params.get('skip')).toBe('2');
    next.flush(page([row('e3')], 3, 2));
    f.detectChanges();

    expect(rows(f)).toHaveLength(3);
    expect(f.nativeElement.querySelector('[data-testid="placements-more"]')).toBeNull();
  });
});
