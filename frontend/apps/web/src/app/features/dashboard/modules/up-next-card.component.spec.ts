import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AgendaItem } from '../../../core/models/home.models';
import { UpNextCardComponent } from './up-next-card.component';

function eventItem(partial: Partial<AgendaItem> = {}): AgendaItem {
  return {
    kind: 'Event',
    id: 'e1',
    title: 'Open training',
    startsAt: '2026-07-12T14:00:00Z',
    endsAt: '2026-07-12T16:00:00Z',
    locationLabel: 'Berlin',
    typeLabel: 'Workshop',
    spotsRemaining: 4,
    participationLimit: 20,
    mode: 'Individuals',
    viewerSignupId: null,
    viewerStatus: null,
    teamGoing: null,
    trainingName: null,
    startTime: null,
    isPublicGuest: null,
    myAnswer: null,
    ...partial,
  };
}

function trainingItem(partial: Partial<AgendaItem> = {}): AgendaItem {
  return {
    kind: 'Training',
    id: 's1',
    title: 'Thu drills',
    startsAt: '2026-07-16T19:00:00Z',
    endsAt: '2026-07-16T21:00:00Z',
    locationLabel: 'Halle Süd',
    typeLabel: null,
    spotsRemaining: null,
    participationLimit: null,
    mode: null,
    viewerSignupId: null,
    viewerStatus: null,
    teamGoing: null,
    trainingName: 'Thu drills',
    startTime: '19:00',
    isPublicGuest: false,
    myAnswer: 'Going',
    ...partial,
  };
}

describe('UpNextCardComponent', () => {
  let httpMock: HttpTestingController;

  function mount(item: AgendaItem): ComponentFixture<UpNextCardComponent> {
    const fixture = TestBed.createComponent(UpNextCardComponent);
    fixture.componentRef.setInput('item', item);
    fixture.detectChanges();
    return fixture;
  }

  const q = (f: ComponentFixture<UpNextCardComponent>, id: string) =>
    f.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLElement | null;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('shows an RSVP button for an individuals event the viewer has not joined', () => {
    const f = mount(eventItem());
    expect(q(f, 'rsvp')).toBeTruthy();
    expect(q(f, 'going-toggle')).toBeNull();
    expect(q(f, 'team-going')).toBeNull();
  });

  it('RSVP posts a sign-up and flips to a going state', () => {
    const f = mount(eventItem());
    q(f, 'rsvp')!.click();
    const req = httpMock.expectOne('/api/v1/events/e1/signup');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ teamId: null });
    req.flush({ id: 's1', status: 'Joined', joinedAt: '', userHandle: null, userDisplayName: null, teamSlug: null, teamName: null });
    f.detectChanges();
    expect(q(f, 'going-toggle')).toBeTruthy();
    expect(q(f, 'rsvp')).toBeNull();
  });

  it('a team-mode event is read-only "team going" with no RSVP', () => {
    const f = mount(eventItem({ mode: 'Teams', teamGoing: { slug: 'rooks', name: 'Rooks' }, viewerSignupId: null }));
    expect(q(f, 'team-going')).toBeTruthy();
    expect(q(f, 'rsvp')).toBeNull();
    expect(q(f, 'going-toggle')).toBeNull();
  });

  it('a going event withdraws through a confirm step', () => {
    const f = mount(eventItem({ viewerSignupId: 's1', viewerStatus: 'Joined' }));
    expect(q(f, 'going-toggle')).toBeTruthy();

    q(f, 'going-toggle')!.click();
    f.detectChanges();
    expect(q(f, 'confirm-withdraw')).toBeTruthy();

    q(f, 'confirm-withdraw')!.click();
    const req = httpMock.expectOne('/api/v1/events/e1/signup/s1');
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
    f.detectChanges();
    expect(q(f, 'rsvp')).toBeTruthy();
  });

  it('a training item shows going/maybe/can\'t and no event RSVP', () => {
    const f = mount(trainingItem());
    expect(q(f, 'training-going')).toBeTruthy();
    expect(q(f, 'training-maybe')).toBeTruthy();
    expect(q(f, 'training-cant')).toBeTruthy();
    expect(q(f, 'rsvp')).toBeNull();
  });

  it('answering a training PUTs to the session response endpoint', () => {
    const f = mount(trainingItem({ myAnswer: null }));
    q(f, 'training-maybe')!.click();
    const req = httpMock.expectOne('/api/v1/trainings/sessions/s1/response');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ answer: 'Maybe' });
    req.flush({ myAnswer: 'Maybe', goingCount: 3 });
    f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Maybe');
  });
});
