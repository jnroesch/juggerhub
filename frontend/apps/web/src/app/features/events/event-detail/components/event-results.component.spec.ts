import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TournamentMatch, TournamentResult } from '../../../../core/models/results.models';
import { translocoLocaleTestingProviders, translocoTestingModule } from '../../../../../testing/transloco-testing';
import { EventResultsComponent } from './event-results.component';

const PAST = '2026-05-02T18:00:00Z';

function result(overrides: Partial<TournamentResult> = {}): TournamentResult {
  return {
    source: 'Manual',
    importedAt: null,
    editedSinceImport: false,
    resultsChangedAt: '2026-05-03T10:00:00Z',
    tugeny: null,
    placements: [
      { id: 'p1', position: 1, name: 'Rigor Mortis', teamSlug: 'rigor-mortis' },
      { id: 'p2', position: 2, name: 'Eclipse', teamSlug: null },
      { id: 'p3', position: 2, name: 'Seven Sins', teamSlug: null },
      { id: 'p4', position: 4, name: 'Kiel Guests', teamSlug: null },
    ],
    rankedCount: 4,
    matchCount: 0,
    viewer: { canEdit: false },
    ...overrides,
  };
}

function match(id: string, stage: string | null, winner: TournamentMatch['winner']): TournamentMatch {
  return {
    id,
    stage,
    name: `Match ${id}`,
    first: { name: 'Rigor Mortis', teamSlug: null },
    second: { name: 'Eclipse', teamSlug: null },
    firstScores: [5, 3],
    secondScores: [2, 5],
    winner,
  };
}

describe('EventResultsComponent (the results card on the event page)', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([]), ...translocoLocaleTestingProviders()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function mount(answer: TournamentResult | 'error', endsAt = PAST): ComponentFixture<EventResultsComponent> {
    const fixture = TestBed.createComponent(EventResultsComponent);
    fixture.componentRef.setInput('eventId', 'e1');
    fixture.componentRef.setInput('endsAt', endsAt);
    fixture.detectChanges();
    const req = http.expectOne('/api/v1/events/e1/results');
    if (answer === 'error') {
      req.flush('boom', { status: 500, statusText: 'Server Error' });
    } else {
      req.flush(answer);
    }
    fixture.detectChanges();
    return fixture;
  }

  const q = (f: ComponentFixture<EventResultsComponent>, testId: string) =>
    f.nativeElement.querySelector(`[data-testid="${testId}"]`) as HTMLElement | null;

  it('calls out the winner and shows ties as they were recorded', () => {
    const f = mount(result());

    expect(q(f, 'results-winner')?.textContent).toContain('Rigor Mortis');
    const positions = Array.from(f.nativeElement.querySelectorAll('[data-position]')).map((li) =>
      (li as HTMLElement).getAttribute('data-position'),
    );
    expect(positions).toEqual(['1', '2', '2', '4']);
    expect(q(f, 'results-meta')?.textContent).toContain('4 teams ranked');
  });

  it('names every team that shares first place', () => {
    const f = mount(
      result({
        placements: [
          { id: 'a', position: 1, name: 'Rigor Mortis', teamSlug: null },
          { id: 'b', position: 1, name: 'Eclipse', teamSlug: null },
        ],
        rankedCount: 2,
      }),
    );

    const winner = q(f, 'results-winner')?.textContent ?? '';
    expect(winner).toContain('Shared first place');
    expect(winner).toContain('Rigor Mortis');
    expect(winner).toContain('Eclipse');
  });

  it('renders nothing when there are no results and no live link', () => {
    const f = mount(result({ source: 'None', placements: [], rankedCount: 0 }));

    expect(q(f, 'results')).toBeNull();
  });

  it('shows an error with a way to retry — never the empty look — when the load fails', () => {
    const f = mount('error');

    expect(q(f, 'results')?.querySelector('jh-alert')).not.toBeNull();
    q(f, 'results-retry')?.click();
    http.expectOne('/api/v1/events/e1/results').flush(result());
    f.detectChanges();
    expect(q(f, 'results-winner')).not.toBeNull();
  });

  const tugeny = {
    tournamentId: 200,
    slug: 'x',
    name: 'X',
    startDate: null,
    liveUrl: 'https://tugeny.org/tournaments/x/live-view',
    tournamentUrl: 'https://tugeny.org/tournaments/x/all-teams',
    treeUrl: 'https://tugeny.org/tournaments/x/tournament-tree',
  };

  it('offers the live view on Tugeny while the tournament is still on', () => {
    const f = mount(result({ source: 'None', placements: [], rankedCount: 0, tugeny }), '2999-01-01T00:00:00Z');

    expect(q(f, 'tugeny-live')?.getAttribute('href')).toBe(tugeny.liveUrl);
    expect(q(f, 'tugeny-tree')).toBeNull();
  });

  it('keeps linking to the bracket on Tugeny once it is over, even with nothing recorded yet', () => {
    const f = mount(result({ source: 'None', placements: [], rankedCount: 0, tugeny }));

    expect(q(f, 'tugeny-tree')?.getAttribute('href')).toBe(tugeny.treeUrl);
    expect(q(f, 'results')?.textContent).toContain('The full bracket and every match are on Tugeny.');
    expect(q(f, 'tugeny-live')).toBeNull();
  });

  it('links the bracket below a recorded ranking once it is over', () => {
    const f = mount(result({ tugeny }));

    expect(q(f, 'results-winner')).not.toBeNull();
    expect(q(f, 'tugeny-tree')?.getAttribute('href')).toBe(tugeny.treeUrl);
    expect(q(f, 'results')?.textContent).not.toContain('The full bracket and every match are on Tugeny.');
  });

  it('credits Tugeny for imported results, and says when they were edited since', () => {
    const f = mount(
      result({
        source: 'TugenyImport',
        editedSinceImport: true,
        tugeny: { ...tugeny, tournamentUrl: 'https://tugeny.org/t' },
      }),
    );

    const provenance = q(f, 'results-provenance')?.textContent ?? '';
    expect(provenance).toContain('Results from Tugeny');
    expect(provenance).toContain('edited since');
  });

  it('loads matches only when asked, grouped by stage with the knockout rounds last', () => {
    const f = mount(result({ matchCount: 3 }));
    http.expectNone((r) => r.url.includes('/matches'));

    q(f, 'results-show-matches')?.click();
    http
      .expectOne((r) => r.url === '/api/v1/events/e1/results/matches')
      .flush({ items: [match('1', null, 'First'), match('2', 'Group 1', 'Draw'), match('3', 'Group 2', 'Second')], totalCount: 3, skip: 0, take: 50 });
    f.detectChanges();

    const stages = Array.from(f.nativeElement.querySelectorAll('[data-testid="match-stage"]')).map((h) => (h as HTMLElement).textContent?.trim());
    expect(stages).toEqual(['Group 1', 'Group 2', 'Knockout']);
    const draw = f.nativeElement.querySelector('[data-winner="Draw"]') as HTMLElement;
    expect(draw.textContent).toContain('draw');
  });
});
