import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { ResultEditor } from '../../../core/models/results.models';
import { translocoLocaleTestingProviders, translocoTestingModule } from '../../../../testing/transloco-testing';
import { EventResultsPageComponent } from './event-results.component';

const EDITOR = '/api/v1/events/e1/results/editor';

function editor(overrides: Partial<ResultEditor> = {}): ResultEditor {
  return {
    eventId: 'e1',
    eventName: 'Hanse Cup',
    participantMode: 'Teams',
    isTournament: true,
    isCancelled: false,
    hasStarted: true,
    hasEnded: false,
    canRecord: true,
    source: 'None',
    importedAt: null,
    editedSinceImport: false,
    resultsChangedAt: null,
    tugeny: null,
    linkedElsewhere: false,
    placements: [],
    signedUpTeams: [
      { teamId: 't-alpha', teamSlug: 'alpha', teamName: 'Alpha' },
      { teamId: 't-bravo', teamSlug: 'bravo', teamName: 'Bravo' },
    ],
    ...overrides,
  };
}

describe('EventResultsPageComponent (the results page)', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
        ...translocoLocaleTestingProviders(),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'e1' }) } } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function mount(answer: ResultEditor | number): ComponentFixture<EventResultsPageComponent> {
    const fixture = TestBed.createComponent(EventResultsPageComponent);
    fixture.detectChanges();
    const req = http.expectOne(EDITOR);
    if (typeof answer === 'number') {
      req.flush({}, { status: answer, statusText: 'Refused' });
    } else {
      req.flush(answer);
    }
    fixture.detectChanges();
    // The team-list card loads the confirmed sign-ups for Tugeny.
    http.match((r) => r.url.includes('/participants')).forEach((r) => r.flush({ items: [], totalCount: 0, skip: 0, take: 100 }));
    fixture.detectChanges();
    return fixture;
  }

  const q = (f: ComponentFixture<EventResultsPageComponent>, testId: string) =>
    f.nativeElement.querySelector(`[data-testid="${testId}"]`) as HTMLElement | null;
  const all = (f: ComponentFixture<EventResultsPageComponent>, testId: string) =>
    Array.from(f.nativeElement.querySelectorAll(`[data-testid="${testId}"]`)) as HTMLElement[];

  it('tells a non-admin they cannot record results', () => {
    const f = mount(403);

    expect(q(f, 'results-not-admin')).not.toBeNull();
    expect(q(f, 'ranking-editor')).toBeNull();
  });

  it('explains that results wait for the start, while linking and the team list are already there', () => {
    const f = mount(editor({ hasStarted: false, canRecord: false }));

    expect(q(f, 'ranking-editor')?.textContent).toContain('once the tournament has started');
    expect(q(f, 'ranking-save')).toBeNull();
    expect(q(f, 'tugeny-card')).not.toBeNull();
    expect(q(f, 'tugeny-team-list')).not.toBeNull();
  });

  it('offers only teams with a confirmed sign-up, plus typing a name', () => {
    const f = mount(editor());

    const options = Array.from(q(f, 'row-team')!.querySelectorAll('option')).map((o) => o.textContent?.trim());
    expect(options).toEqual(['Type a name', 'Alpha', 'Bravo']);
  });

  it('has exactly one primary button', () => {
    const f = mount(editor());

    const primary = Array.from(f.nativeElement.querySelectorAll('button, a')).filter((el) =>
      (el as HTMLElement).classList.contains('bg-brand'),
    );
    expect(primary).toHaveLength(1);
    expect((primary[0] as HTMLElement).getAttribute('data-testid')).toBe('ranking-save');
  });

  it("keeps a row an admin connected read-only and sends it back unchanged, with its id", () => {
    const f = mount(
      editor({
        source: 'Manual',
        placements: [
          { id: 'p1', position: 1, name: 'Rigor Mortis', sourceName: 'Rigor', teamId: 't-rigor', teamSlug: 'rigor', connectedBy: 'Jan', connectedAt: '2026-05-01T00:00:00Z' },
          { id: 'p2', position: 2, name: 'Kiel Guests', sourceName: 'Kiel Guests', teamId: null, teamSlug: null, connectedBy: null, connectedAt: null },
        ],
      }),
    );

    expect(all(f, 'row-locked')).toHaveLength(1);
    expect(all(f, 'row-locked')[0].textContent).toContain('Rigor Mortis');

    q(f, 'ranking-save')!.click();
    const req = http.expectOne((r) => r.method === 'PUT' && r.url === '/api/v1/events/e1/results/ranking');
    expect(req.request.body.placements).toEqual([
      { id: 'p1', position: 1, name: 'Rigor Mortis', teamId: 't-rigor' },
      { id: 'p2', position: 2, name: 'Kiel Guests', teamId: null },
    ]);
    req.flush({});
    http.expectOne(EDITOR).flush(editor());
  });

  it('fills the editor from a Tugeny export with every row unconnected, and refuses junk untouched', () => {
    const f = mount(editor());

    q(f, 'paste-open')!.click();
    f.detectChanges();
    const box = q(f, 'paste-text') as HTMLTextAreaElement;
    box.value = 'hello';
    box.dispatchEvent(new Event('input'));
    q(f, 'paste-read')!.click();
    f.detectChanges();
    expect(q(f, 'paste-panel')?.textContent).toContain("couldn't read that");

    box.value = '[{"name":"Alpha","position":1},{"name":"Other","position":2}]';
    box.dispatchEvent(new Event('input'));
    q(f, 'paste-read')!.click();
    f.detectChanges();

    const names = all(f, 'row-name').map((i) => (i as HTMLInputElement).value);
    expect(names).toEqual(['Alpha', 'Other']);
    // Even "Alpha", which matches a signed-up team, is not connected by itself.
    expect(all(f, 'row-team').map((s) => (s as HTMLSelectElement).value)).toEqual(['', '']);
  });

  it('shows the linked Tugeny tournament and warns when it is linked to another event too', () => {
    const f = mount(
      editor({
        linkedElsewhere: true,
        tugeny: { tournamentId: 200, slug: 'x', name: '25. Deutsche Meisterschaft', startDate: '2024-09-21', liveUrl: 'l', tournamentUrl: 't', treeUrl: 'b' },
      }),
    );

    expect(q(f, 'tugeny-linked')?.textContent).toContain('25. Deutsche Meisterschaft');
    expect(q(f, 'tugeny-linked-elsewhere')).not.toBeNull();
  });

  it('explains when Tugeny has no final results yet', () => {
    const f = mount(editor({ tugeny: { tournamentId: 303, slug: 'y', name: 'Y', startDate: null, liveUrl: 'l', tournamentUrl: 't', treeUrl: 'b' } }));

    q(f, 'tugeny-import')!.click();
    http
      .expectOne('/api/v1/events/e1/results/tugeny-import')
      .flush({ type: 'https://juggerhub.com/problems/tugeny-not-finalized' }, { status: 422, statusText: 'Unprocessable' });
    f.detectChanges();

    expect(q(f, 'tugeny-not-finalized')).not.toBeNull();
  });

  it('never preselects a team in the import draft', () => {
    const f = mount(editor({ tugeny: { tournamentId: 200, slug: 'x', name: 'X', startDate: null, liveUrl: 'l', tournamentUrl: 't', treeUrl: 'b' } }));

    q(f, 'tugeny-import')!.click();
    http.expectOne('/api/v1/events/e1/results/tugeny-import').flush({
      tournamentName: 'X',
      placements: [
        { position: 1, name: 'Alpha', tugenyTeamId: 40 },
        { position: 2, name: 'Bravo', tugenyTeamId: 42 },
      ],
      matchCount: 3,
      signedUpTeams: editor().signedUpTeams,
      replacesExisting: false,
    });
    f.detectChanges();

    expect(all(f, 'preview-team').map((s) => (s as HTMLSelectElement).value)).toEqual(['', '']);

    q(f, 'tugeny-import-confirm')!.click();
    const commit = http.expectOne((r) => r.method === 'POST' && r.url === '/api/v1/events/e1/results/tugeny-import');
    expect(commit.request.body).toEqual({ connections: [] });
    commit.flush({});
    http.expectOne(EDITOR).flush(editor());
  });
});
