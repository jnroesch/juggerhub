import { provideHttpClient, withXhr } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { SeriesEditResult, TrainingSessionDetail } from '../../../core/models/trainings.models';
import { TrainingsService } from '../../../core/services/trainings.service';
import { translocoTestingModule } from '../../../../testing/transloco-testing';
import { TrainingEditComponent } from './training-edit.component';

const ENTRY_SESSION_ID = 'aaaaaaaa-0000-0000-0000-000000000001';

const SESSION: TrainingSessionDetail = {
  sessionId: ENTRY_SESSION_ID,
  trainingId: 'bbbbbbbb-0000-0000-0000-000000000002',
  teamSlug: 'rheinfeuer',
  teamName: 'Rheinfeuer',
  name: 'Tuesday Training',
  description: null,
  isOneOff: false,
  sessionDate: '2026-09-01',
  startTime: '19:00:00',
  endTime: '21:00:00',
  locationKind: 'InPerson',
  venueName: 'Sportpark',
  street: 'Aachener Str. 999',
  postalCode: '50933',
  location: { externalId: 'osm:R:1', name: 'Köln', countryName: 'Germany', countryCode: 'DE', label: 'Köln, Germany' },
  locationLabel: 'Sportpark, Köln',
  virtualLink: null,
  weekday: 'Tuesday',
  interval: 'Weekly',
  endDate: '2026-09-29',
  visibility: 'TeamOnly',
  status: 'Scheduled',
  isPast: false,
  isDetached: false,
  viewerIsAdmin: true,
  viewerIsGuest: false,
  myAnswer: null,
  whosComing: {
    going: { count: 0, people: [] },
    maybe: { count: 0, people: [] },
    cant: { count: 0, people: [] },
  },
};

/**
 * The whole-series edit form (feature 018) — specifically where it sends the admin afterwards.
 *
 * The form is session-keyed and is entered from the series' next upcoming session, but a
 * weekday/interval/end-date change regenerates the future set and hard-deletes that very session, so
 * navigating back to the id it was opened with lands on "training not found" (GH #181).
 */
describe('TrainingEditComponent (post-save destination)', () => {
  let fixture: ComponentFixture<TrainingEditComponent>;
  let api: { getSession: jest.Mock; editSeries: jest.Mock };
  let navigate: jest.Mock;

  function build(result: SeriesEditResult): void {
    api = {
      getSession: jest.fn().mockReturnValue(of(SESSION)),
      editSeries: jest.fn().mockReturnValue(of(result)),
    };
    navigate = jest.fn().mockResolvedValue(true);

    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: TrainingsService, useValue: api },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ id: ENTRY_SESSION_ID }),
              queryParamMap: convertToParamMap({ scope: 'series' }),
            },
          },
        },
      ],
    });
    TestBed.inject(Router).navigate = navigate;

    fixture = TestBed.createComponent(TrainingEditComponent);
    fixture.detectChanges();
  }

  function saveSeries(): void {
    fixture.componentInstance['saveSeries']();
    fixture.detectChanges();
  }

  const result = (nextSessionId: string | null, removed = 0): SeriesEditResult => ({
    trainingId: SESSION.trainingId,
    addedSessions: removed,
    removedSessions: removed,
    keptSessions: 0,
    nextSessionId,
  });

  afterEach(() => TestBed.resetTestingModule());

  it('goes to the surviving next session, not the entry point the pattern change deleted', () => {
    const regenerated = 'cccccccc-0000-0000-0000-000000000003';
    build(result(regenerated, 4));

    fixture.componentInstance['weekday'] = 'Thursday';
    saveSeries();

    expect(api.editSeries).toHaveBeenCalledWith(SESSION.trainingId, expect.objectContaining({ weekday: 'Thursday' }));
    expect(navigate).toHaveBeenCalledWith(['/trainings/sessions', regenerated]);
    expect(navigate).not.toHaveBeenCalledWith(['/trainings/sessions', ENTRY_SESSION_ID]);
  });

  it('falls back to the team trainings tab when no upcoming session survives', () => {
    build(result(null));

    saveSeries();

    expect(navigate).toHaveBeenCalledWith(['/t', SESSION.teamSlug, 'trainings']);
  });

  it('still lands on the session it edited when nothing was regenerated', () => {
    build(result(ENTRY_SESSION_ID));

    saveSeries();

    expect(navigate).toHaveBeenCalledWith(['/trainings/sessions', ENTRY_SESSION_ID]);
  });
});
