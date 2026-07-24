import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { authInterceptor } from './auth.interceptor';
import { MAX_RETRIES, REQUEST_TIMEOUT_MS, retryInterceptor } from './retry.interceptor';

const USER = { id: 'u1', email: 'a@example.com', emailConfirmed: true, onboardingCompleted: true };

/**
 * Browser-hop resilience (feature 028; constitution Principle VII).
 *
 * Backoff uses real timers, so the retrying tests drive them with jest's fake clock: flush the
 * failure, advance past the jittered delay, then expect the next attempt. `Math.random` is pinned
 * to 1 so the jitter window is deterministic — full jitter picks within [0, ceiling), and pinning
 * the top of that range makes "advance far enough" exact rather than flaky.
 */
describe('retryInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;

  /** Registered in production order: auth outer, retry inner. */
  function configure(interceptors = [authInterceptor, retryInterceptor]) {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors(interceptors)),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  }

  beforeEach(() => {
    jest.useFakeTimers();
    jest.spyOn(Math, 'random').mockReturnValue(0.999);
    configure();
  });

  afterEach(() => {
    httpMock.verify();
    jest.useRealTimers();
    jest.restoreAllMocks();
  });

  /** Advance past any backoff the interceptor could have scheduled for this attempt. */
  function settleBackoff(): void {
    jest.advanceTimersByTime(5_000);
  }

  describe('safe reads recover from a transient fault (FR-002)', () => {
    it('retries a GET that failed with 503 and resolves with the eventual body', () => {
      let result: unknown;
      http.get('/api/v1/teams').subscribe((r) => (result = r));

      httpMock.expectOne('/api/v1/teams').flush(null, { status: 503, statusText: 'Unavailable' });
      settleBackoff();
      httpMock.expectOne('/api/v1/teams').flush({ ok: true });

      expect(result).toEqual({ ok: true });
    });

    it.each([0, 408, 502, 503, 504])('retries transient status %s', (status) => {
      let result: unknown;
      http.get('/api/v1/teams').subscribe((r) => (result = r));

      httpMock.expectOne('/api/v1/teams').flush(null, { status, statusText: 'Transient' });
      settleBackoff();
      httpMock.expectOne('/api/v1/teams').flush({ ok: true });

      expect(result).toEqual({ ok: true });
    });

    it('gives up after the configured maximum and surfaces the original error unchanged (FR-009)', () => {
      let error: { status?: number } | undefined;
      http.get('/api/v1/teams').subscribe({ error: (e) => (error = e) });

      // 1 initial attempt + MAX_RETRIES retries, and not one more.
      for (let i = 0; i <= MAX_RETRIES; i++) {
        httpMock.expectOne('/api/v1/teams').flush(null, { status: 503, statusText: 'Unavailable' });
        settleBackoff();
      }

      httpMock.expectNone('/api/v1/teams');
      expect(error?.status).toBe(503);
    });
  });

  describe('mutations are never retried (FR-004)', () => {
    // The most important assertion in this feature. A retried mutation can silently apply an
    // action twice, because a request that failed may still have been processed.
    it.each(['POST', 'PUT', 'PATCH', 'DELETE'])(
      'issues a %s exactly once even on a transient failure',
      (method) => {
        let error: { status?: number } | undefined;
        http
          .request(method, '/api/v1/teams', { body: {} })
          .subscribe({ error: (e) => (error = e) });

        httpMock.expectOne('/api/v1/teams').flush(null, { status: 503, statusText: 'Unavailable' });
        settleBackoff();

        httpMock.expectNone('/api/v1/teams');
        expect(error?.status).toBe(503);
      },
    );
  });

  describe('rejections fail fast (FR-005, FR-031)', () => {
    it.each([400, 403, 404, 409, 422, 500])('does not retry status %s', (status) => {
      let error: { status?: number } | undefined;
      http.get('/api/v1/teams').subscribe({ error: (e) => (error = e) });

      httpMock.expectOne('/api/v1/teams').flush(null, { status, statusText: 'Rejected' });
      settleBackoff();

      httpMock.expectNone('/api/v1/teams');
      expect(error?.status).toBe(status);
    });

    it('never retries a 429, because that is our own fail-closed rate limiter', () => {
      // Retrying here would defeat the limit rather than ride it out. Note this is deliberately
      // the OPPOSITE of the backend's outbound rule, where a 429 means a provider is throttling
      // us and backing off is correct.
      let error: { status?: number } | undefined;
      http.get('/api/v1/chat/messages').subscribe({ error: (e) => (error = e) });

      httpMock
        .expectOne('/api/v1/chat/messages')
        .flush(null, { status: 429, statusText: 'Too Many Requests' });
      settleBackoff();

      httpMock.expectNone('/api/v1/chat/messages');
      expect(error?.status).toBe(429);
    });
  });

  describe('nothing waits forever (FR-001)', () => {
    it('abandons a read that never responds, after a bounded number of attempts', () => {
      // The hung-backend case that used to spin forever. A timeout is itself a transient fault, so
      // the read IS retried — the guarantee is that the sequence terminates, not that it gives up
      // on the first attempt.
      let error: { name?: string } | undefined;
      http.get('/api/v1/teams').subscribe({ error: (e) => (error = e) });

      for (let i = 0; i <= MAX_RETRIES; i++) {
        httpMock.expectOne('/api/v1/teams');
        jest.advanceTimersByTime(REQUEST_TIMEOUT_MS + 1);
        settleBackoff();
      }

      // Bounded: no further attempt is made, and the caller is told rather than left waiting.
      httpMock.expectNone('/api/v1/teams');
      expect(error?.name).toBe('TimeoutError');
    });

    it('time-limits a mutation too, even though it will not be retried', () => {
      let error: { name?: string } | undefined;
      http.post('/api/v1/teams', {}).subscribe({ error: (e) => (error = e) });

      httpMock.expectOne('/api/v1/teams');
      jest.advanceTimersByTime(REQUEST_TIMEOUT_MS + 1);

      expect(error?.name).toBe('TimeoutError');
    });
  });

  describe('the session-refresh path is untouched (FR-007, FR-008)', () => {
    it('lets a 401 through to exactly one refresh, with no extra attempts', () => {
      // The regression guard for interceptor ordering. If retry were registered OUTSIDE auth,
      // this would show repeated refresh cycles.
      let result: unknown;
      http.get('/api/v1/widgets').subscribe((r) => (result = r));

      httpMock.expectOne('/api/v1/widgets').flush(null, { status: 401, statusText: 'Unauthorized' });
      httpMock.expectOne('/api/v1/auth/refresh').flush(USER);
      httpMock.expectOne('/api/v1/widgets').flush({ ok: true });
      settleBackoff();

      httpMock.expectNone('/api/v1/auth/refresh');
      expect(result).toEqual({ ok: true });
    });

    it('does not retry the auth endpoints themselves', () => {
      let error: { status?: number } | undefined;
      http.get('/api/v1/auth/me').subscribe({ error: (e) => (error = e) });

      httpMock.expectOne('/api/v1/auth/me').flush(null, { status: 503, statusText: 'Unavailable' });
      settleBackoff();

      httpMock.expectNone('/api/v1/auth/me');
      expect(error?.status).toBe(503);
    });
  });

  describe('abandoned work stops (FR-010)', () => {
    it('issues no further attempts once the caller unsubscribes', () => {
      const sub = http.get('/api/v1/teams').subscribe({ error: () => undefined });

      httpMock.expectOne('/api/v1/teams').flush(null, { status: 503, statusText: 'Unavailable' });
      sub.unsubscribe();
      settleBackoff();

      httpMock.expectNone('/api/v1/teams');
    });
  });
});
