import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { UpNextItem } from '../../../core/models/home.models';
import { UpNextCardComponent } from './up-next-card.component';

function makeItem(partial: Partial<UpNextItem> = {}): UpNextItem {
  return {
    eventId: 'e1',
    title: 'Open training',
    typeLabel: 'Workshop',
    startsAt: '2026-07-12T14:00:00Z',
    endsAt: '2026-07-12T16:00:00Z',
    locationLabel: 'Berlin',
    spotsRemaining: 4,
    participationLimit: 20,
    mode: 'Individuals',
    viewerSignupId: null,
    viewerStatus: null,
    teamGoing: null,
    ...partial,
  };
}

describe('UpNextCardComponent', () => {
  let httpMock: HttpTestingController;

  function mount(item: UpNextItem): ComponentFixture<UpNextCardComponent> {
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

  it('shows an RSVP button for an individuals item the viewer has not joined', () => {
    const f = mount(makeItem());
    expect(q(f, 'rsvp')).toBeTruthy();
    expect(q(f, 'going-toggle')).toBeNull();
    expect(q(f, 'team-going')).toBeNull();
  });

  it('RSVP posts a sign-up and flips to a going state', () => {
    const f = mount(makeItem());
    q(f, 'rsvp')!.click();
    const req = httpMock.expectOne('/api/v1/events/e1/signup');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ teamId: null });
    req.flush({ id: 's1', status: 'Joined', joinedAt: '', userHandle: null, userDisplayName: null, teamSlug: null, teamName: null });
    f.detectChanges();
    expect(q(f, 'going-toggle')).toBeTruthy();
    expect(q(f, 'rsvp')).toBeNull();
  });

  it('a team-mode item is read-only "team going" with no RSVP', () => {
    const f = mount(makeItem({ mode: 'Teams', teamGoing: { slug: 'rooks', name: 'Rooks' }, viewerSignupId: null }));
    expect(q(f, 'team-going')).toBeTruthy();
    expect(q(f, 'rsvp')).toBeNull();
    expect(q(f, 'going-toggle')).toBeNull();
  });

  it('a going item withdraws through a confirm step', () => {
    const f = mount(makeItem({ viewerSignupId: 's1', viewerStatus: 'Joined' }));
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
});
