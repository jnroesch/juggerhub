import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MarketService } from './market.service';

describe('MarketService', () => {
  let service: MarketService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(MarketService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('freeAgents GETs the board free-agents side with a position filter', () => {
    service.freeAgents('e1', 'Schild').subscribe();
    const req = httpMock.expectOne(
      (r) => r.url === '/api/v1/events/e1/market/free-agents' && r.params.get('position') === 'Schild',
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], totalCount: 0, skip: 0, take: 20 });
  });

  it('recruitingParties GETs the board parties side', () => {
    service.recruitingParties('e1').subscribe();
    const req = httpMock.expectOne((r) => r.url === '/api/v1/events/e1/market/parties');
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], totalCount: 0, skip: 0, take: 20 });
  });

  it('postListing POSTs the caller listing', () => {
    service.postListing('e1', { positions: ['Laeufer'], pitch: 'hi' }).subscribe();
    const req = httpMock.expectOne('/api/v1/events/e1/market/listing');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ positions: ['Laeufer'], pitch: 'hi' });
    req.flush({ id: 'l1', eventId: 'e1', positions: ['Laeufer'], pitch: 'hi' });
  });

  it('apply POSTs to the party applications endpoint', () => {
    service.apply('p1', { positions: [] }).subscribe();
    const req = httpMock.expectOne('/api/v1/parties/p1/market/applications');
    expect(req.request.method).toBe('POST');
    req.flush({});
  });

  it('invite POSTs to the party invites endpoint', () => {
    service.invite('p1', { userId: 'u9', positions: [] }).subscribe();
    const req = httpMock.expectOne('/api/v1/parties/p1/market/invites');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ userId: 'u9', positions: [] });
    req.flush({});
  });

  it('setRecruiting PUTs the recruiting settings', () => {
    service.setRecruiting('p1', { isRecruiting: true, spotsAdvertised: 2, positionsNeeded: ['Kette'], blurb: null }).subscribe();
    const req = httpMock.expectOne('/api/v1/parties/p1/recruiting');
    expect(req.request.method).toBe('PUT');
    req.flush({});
  });

  it('accept POSTs to the request accept action', () => {
    service.accept('r1').subscribe();
    const req = httpMock.expectOne('/api/v1/market/requests/r1/accept');
    expect(req.request.method).toBe('POST');
    req.flush({});
  });

  it('revoke POSTs to the request revoke action', () => {
    service.revoke('r1').subscribe();
    const req = httpMock.expectOne('/api/v1/market/requests/r1/revoke');
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });

  it('searchUsers passes the query for a direct invite', () => {
    service.searchUsers('p1', 'mara').subscribe();
    const req = httpMock.expectOne(
      (r) => r.url === '/api/v1/parties/p1/market/user-search' && r.params.get('query') === 'mara',
    );
    req.flush({ items: [], totalCount: 0, skip: 0, take: 20 });
  });

  it('mine GETs the dashboard request summary', () => {
    service.mine().subscribe();
    const req = httpMock.expectOne((r) => r.url === '/api/v1/market/mine');
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], totalCount: 0, skip: 0, take: 20 });
  });

  it('myListings GETs the caller active listings for the dashboard', () => {
    service.myListings().subscribe();
    const req = httpMock.expectOne((r) => r.url === '/api/v1/market/mine/listings');
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], totalCount: 0, skip: 0, take: 20 });
  });
});
