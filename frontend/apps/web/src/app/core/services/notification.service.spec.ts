import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AppNotification } from '../models/notification.models';
import { AuthService } from './auth.service';
import { NotificationService } from './notification.service';

/**
 * NotificationService (feature 010). Zoneless — no `fakeAsync` (the 014 convention).
 *
 * What is pinned here is the catch-up after a reconnect (GH #402). The server ends every hub
 * connection when its access token expires, about four times an hour, so the catch-up runs under
 * someone who may be half-way down the Alerts list and must not take it away from them.
 */
describe('NotificationService', () => {
  let service: NotificationService;
  let httpMock: HttpTestingController;
  const authed = signal(true);

  const FIRST_PAGE = '/api/v1/notifications?skip=0&take=20';

  /** `alert(7)` is older than `alert(8)`; the list and every page are newest-first. */
  const alert = (n: number, over: Partial<AppNotification> = {}): AppNotification =>
    ({ id: `n${String(n).padStart(2, '0')}`, isRead: false, ...over }) as AppNotification;

  /** Alerts `from` down to `to`, newest first. */
  const alerts = (from: number, to: number): AppNotification[] =>
    Array.from({ length: from - to + 1 }, (_, i) => alert(from - i));

  const ids = () => service.items().map((i) => i.id);

  const reconnect = () => {
    service['onReconnected']();
    httpMock.match('/api/v1/notifications/unread-count').forEach((r) => r.flush({ count: 0 }));
  };

  beforeEach(() => {
    authed.set(true);

    TestBed.configureTestingModule({
      providers: [
        NotificationService,
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { isAuthenticated: authed } },
      ],
    });

    service = TestBed.inject(NotificationService);
    httpMock = TestBed.inject(HttpTestingController);

    // The constructor effect seeds the badge; flush it so each test starts clean.
    TestBed.tick();
    httpMock.match('/api/v1/notifications/unread-count').forEach((r) => r.flush({ count: 0 }));
  });

  afterEach(() => httpMock.verify({ ignoreCancelled: true }));

  /** The Alerts page with 25 alerts on the server: the first page, then "load more". */
  const loadTwoPages = () => {
    service.loadFirstPage().subscribe();
    httpMock.expectOne(FIRST_PAGE).flush({ items: alerts(25, 6), totalCount: 25, skip: 0, take: 20 });
    service.loadMore().subscribe();
    httpMock
      .expectOne('/api/v1/notifications?skip=20&take=20')
      .flush({ items: alerts(5, 1), totalCount: 25, skip: 20, take: 20 });
  };

  it('keeps what the reader loaded further down', () => {
    loadTwoPages();

    reconnect();
    httpMock.expectOne(FIRST_PAGE).flush({ items: alerts(25, 6), totalCount: 25, skip: 0, take: 20 });

    // Replacing the list with the newest page — what a reconnect used to do — took the last five
    // away from under whoever was reading them.
    expect(ids()).toEqual(alerts(25, 1).map((a) => a.id));
    expect(service.hasMore()).toBe(false);
  });

  it('adds what arrived while the socket was down', () => {
    loadTwoPages();

    reconnect();
    httpMock.expectOne(FIRST_PAGE).flush({ items: alerts(26, 7), totalCount: 26, skip: 0, take: 20 });

    expect(ids()).toEqual(alerts(26, 1).map((a) => a.id));
    expect(service.total()).toBe(26);
  });

  it('takes removals and read states from the server for the range the newest page covers', () => {
    loadTwoPages();

    reconnect();
    // n24 was withdrawn and n25 read on another device, so the newest twenty now reach down to n05.
    httpMock.expectOne(FIRST_PAGE).flush({
      items: [alert(25, { isRead: true }), ...alerts(23, 5)],
      totalCount: 24,
      skip: 0,
      take: 20,
    });

    expect(ids()).toEqual(['n25', ...alerts(23, 1).map((a) => a.id)]);
    expect(service.items()[0].isRead).toBe(true);
    expect(service.total()).toBe(24);
  });

  it('is the newest page itself when nothing was loaded below it', () => {
    service.loadFirstPage().subscribe();
    httpMock.expectOne(FIRST_PAGE).flush({ items: alerts(3, 1), totalCount: 3, skip: 0, take: 20 });

    reconnect();
    httpMock.expectOne(FIRST_PAGE).flush({ items: [alert(4), alert(3), alert(1)], totalCount: 3, skip: 0, take: 20 });

    expect(ids()).toEqual(['n04', 'n03', 'n01']);
  });

  it('starts again from the newest page when nothing joins up with the list', () => {
    service.loadFirstPage().subscribe();
    httpMock.expectOne(FIRST_PAGE).flush({ items: alerts(20, 1), totalCount: 20, skip: 0, take: 20 });

    reconnect();
    httpMock.expectOne(FIRST_PAGE).flush({ items: alerts(45, 26), totalCount: 45, skip: 0, take: 20 });

    // More than a page arrived while away: a list with a gap in it would page wrongly.
    expect(ids()).toEqual(alerts(45, 26).map((a) => a.id));
    expect(service.hasMore()).toBe(true);
  });

  it('keeps an alert that was pushed while the answer was on its way', () => {
    loadTwoPages();

    reconnect();
    const catchUp = httpMock.expectOne(FIRST_PAGE);
    // Over the new socket, after the server had already read the page it is about to answer with.
    service['onCreated'](alert(26));
    catchUp.flush({ items: alerts(25, 6), totalCount: 25, skip: 0, take: 20 });

    // It is newer than the page, not missing from it.
    expect(ids()).toEqual(alerts(26, 1).map((a) => a.id));
    expect(service.total()).toBe(26);
  });

  it('holds nothing for a client that signed out while the answer was on its way', () => {
    loadTwoPages();

    reconnect();
    const catchUp = httpMock.expectOne(FIRST_PAGE);
    authed.set(false);
    TestBed.tick();
    catchUp.flush({ items: alerts(25, 6), totalCount: 25, skip: 0, take: 20 });

    expect(ids()).toEqual([]);
  });

  it('fetches nothing for an inbox that was never opened', () => {
    reconnect();

    httpMock.expectNone(FIRST_PAGE);
  });
});
