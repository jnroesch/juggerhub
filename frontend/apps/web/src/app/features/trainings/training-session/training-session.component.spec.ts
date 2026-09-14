import { provideHttpClient, withXhr } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { TrainingSessionDetail } from '../../../core/models/trainings.models';
import { BrowseReturnService } from '../../../core/services/browse-return.service';
import { TrainingsService } from '../../../core/services/trainings.service';
import { translocoLocaleTestingProviders, translocoTestingModule } from '../../../../testing/transloco-testing';
import { TrainingSessionComponent } from './training-session.component';

const SESSION: TrainingSessionDetail = {
  sessionId: 'aaaaaaaa-0000-0000-0000-000000000001',
  trainingId: 'bbbbbbbb-0000-0000-0000-000000000002',
  teamSlug: 'rheinfeuer',
  teamName: 'Rheinfeuer',
  name: 'Tuesday Training',
  description: null,
  isOneOff: false,
  sessionDate: '2026-10-06',
  startTime: '19:00:00',
  endTime: '21:00:00',
  locationKind: 'InPerson',
  venueName: null,
  street: null,
  postalCode: null,
  location: null,
  locationLabel: 'Köln, Germany',
  virtualLink: null,
  weekday: 'Tuesday',
  interval: 'Weekly',
  endDate: null,
  visibility: 'Public',
  status: 'Scheduled',
  isPast: false,
  isDetached: false,
  viewerIsAdmin: false,
  viewerIsGuest: false,
  myAnswer: null,
  whosComing: {
    going: { count: 0, people: [] },
    maybe: { count: 0, people: [] },
    cant: { count: 0, people: [] },
  },
};

/**
 * "‹ Trainings" on a session page (GH #279). A session has two parents — the team's Trainings tab
 * for a member, the public trainings list for everyone else — and the link must lead to the one the
 * viewer can open. It used to lead every guest to the team tab, which 404s for them.
 */
describe('TrainingSessionComponent — back link', () => {
  const getSession = jest.fn();

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
        ...translocoLocaleTestingProviders(),
        { provide: TrainingsService, useValue: { getSession } },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ id: SESSION.sessionId })) } },
      ],
    });
  });

  function mount(session: TrainingSessionDetail): ComponentFixture<TrainingSessionComponent> {
    getSession.mockReturnValue(of(session));
    const fixture = TestBed.createComponent(TrainingSessionComponent);
    fixture.detectChanges();
    return fixture;
  }

  const backHref = (f: ComponentFixture<TrainingSessionComponent>) =>
    (f.nativeElement.querySelector('[data-testid="session-back"]') as HTMLAnchorElement).getAttribute('href');

  it("leads a member to their team's Trainings tab", () => {
    expect(backHref(mount(SESSION))).toBe('/t/rheinfeuer/trainings');
  });

  it('leads a guest to the public trainings list, not the team tab they cannot open', () => {
    expect(backHref(mount({ ...SESSION, viewerIsGuest: true }))).toBe('/browse/trainings');
  });

  it('reopens the public list with the search and filters the guest left it with', () => {
    TestBed.inject(BrowseReturnService).remember('/browse/trainings', { city: ['Köln'], q: ['open'] });
    const href = backHref(mount({ ...SESSION, viewerIsGuest: true }));

    expect(href).toContain('/browse/trainings?');
    expect(href).toContain('city=K%C3%B6ln');
    expect(href).toContain('q=open');
  });
});
