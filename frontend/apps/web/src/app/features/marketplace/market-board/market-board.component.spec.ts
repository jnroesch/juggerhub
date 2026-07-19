import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MarketBoardComponent } from './market-board.component';
import { MyMarket } from '../../../core/models/market.models';

describe('MarketBoardComponent', () => {
  let httpMock: HttpTestingController;

  const emptyPage = { items: [], totalCount: 0, skip: 0, take: 20 };

  function mount(authenticated: boolean, me?: MyMarket): ComponentFixture<MarketBoardComponent> {
    const fixture = TestBed.createComponent(MarketBoardComponent);
    fixture.componentRef.setInput('eventId', 'e1');
    fixture.componentRef.setInput('authenticated', authenticated);
    fixture.detectChanges();
    httpMock.expectOne((r) => r.url === '/api/v1/events/e1/market/free-agents').flush(emptyPage);
    httpMock.expectOne((r) => r.url === '/api/v1/events/e1/market/parties').flush(emptyPage);
    if (authenticated) {
      httpMock.expectOne((r) => r.url === '/api/v1/events/e1/market/me').flush(
        me ?? { userId: 'me1', mode: 'Teams', eligible: false, ineligibleReason: null, myListing: null, adminParties: [], invitesToAnswer: [], myApplications: [] },
      );
    }
    fixture.detectChanges();
    return fixture;
  }

  const q = (f: ComponentFixture<MarketBoardComponent>, id: string) =>
    f.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLElement | null;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('renders the two-sided board with empty states', () => {
    const f = mount(false);
    expect(q(f, 'market-board')).toBeTruthy();
    // Default side is parties → its empty state shows; no post affordance when unauthenticated.
    expect(q(f, 'post-yourself')).toBeNull();
  });

  it('offers "post yourself" when the viewer is eligible', () => {
    const f = mount(true, {
      userId: 'me1',
      mode: 'Teams',
      eligible: true,
      ineligibleReason: null,
      myListing: null,
      adminParties: [],
      invitesToAnswer: [],
      myApplications: [],
    });
    expect(q(f, 'post-yourself')).toBeTruthy();
  });

  it('folds edit/take-down into the viewer\'s own card in the free-agents column', () => {
    const fixture = TestBed.createComponent(MarketBoardComponent);
    fixture.componentRef.setInput('eventId', 'e1');
    fixture.componentRef.setInput('authenticated', true);
    fixture.detectChanges();
    httpMock.expectOne((r) => r.url === '/api/v1/events/e1/market/free-agents').flush({
      items: [{ userId: 'me1', handle: 'me', displayName: 'Me', hasAvatar: false, positions: ['Laeufer'], pitch: 'ready' }],
      totalCount: 1, skip: 0, take: 20,
    });
    httpMock.expectOne((r) => r.url === '/api/v1/events/e1/market/parties').flush(emptyPage);
    httpMock.expectOne((r) => r.url === '/api/v1/events/e1/market/me').flush({
      userId: 'me1', mode: 'Teams', eligible: false, ineligibleReason: null,
      myListing: { id: 'l1', eventId: 'e1', positions: ['Laeufer'], pitch: 'ready' },
      adminParties: [], invitesToAnswer: [], myApplications: [],
    });
    fixture.detectChanges();
    // The standalone block above the columns is gone; actions live on the card.
    expect(q(fixture, 'your-listing')).toBeNull();
    expect(q(fixture, 'my-listing-actions')).toBeTruthy();
  });

  it('shows the reason when the viewer is not eligible', () => {
    const f = mount(true, {
      userId: 'me1',
      mode: 'Teams',
      eligible: false,
      ineligibleReason: "You're already in a crew for this event.",
      myListing: null,
      adminParties: [],
      invitesToAnswer: [],
      myApplications: [],
    });
    expect(q(f, 'ineligible')).toBeTruthy();
    expect(q(f, 'post-yourself')).toBeNull();
  });
});
