import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { authInterceptor } from './auth.interceptor';

const USER = { id: 'u1', email: 'a@example.com', emailConfirmed: true, onboardingCompleted: true };
const unauthorized = { status: 401, statusText: 'Unauthorized' };

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => httpMock.verify());

  it('refreshes and retries once on a 401', () => {
    let result: unknown;
    http.get('/api/v1/widgets').subscribe((r) => (result = r));

    httpMock.expectOne('/api/v1/widgets').flush(null, unauthorized);
    httpMock.expectOne('/api/v1/auth/refresh').flush(USER);
    httpMock.expectOne('/api/v1/widgets').flush({ ok: true });

    expect(result).toEqual({ ok: true });
  });

  it('redirects to sign-in when the refresh fails', () => {
    const navigate = jest.spyOn(router, 'navigate').mockResolvedValue(true);

    http.get('/api/v1/widgets').subscribe({ next: () => undefined, error: () => undefined });
    httpMock.expectOne('/api/v1/widgets').flush(null, unauthorized);
    httpMock.expectOne('/api/v1/auth/refresh').flush(null, unauthorized);

    expect(navigate).toHaveBeenCalledWith(['/sign-in']);
  });

  it('does not attempt refresh for the auth endpoints themselves', () => {
    let errored = false;
    http.post('/api/v1/auth/login', {}).subscribe({ next: () => undefined, error: () => (errored = true) });

    httpMock.expectOne('/api/v1/auth/login').flush(null, unauthorized);
    httpMock.expectNone('/api/v1/auth/refresh');
    expect(errored).toBe(true);
  });

  it('single-flights one refresh across concurrent 401s', () => {
    http.get('/api/v1/a').subscribe({ next: () => undefined, error: () => undefined });
    http.get('/api/v1/b').subscribe({ next: () => undefined, error: () => undefined });

    httpMock.expectOne('/api/v1/a').flush(null, unauthorized);
    httpMock.expectOne('/api/v1/b').flush(null, unauthorized);

    const refreshes = httpMock.match('/api/v1/auth/refresh');
    expect(refreshes.length).toBe(1);
    refreshes[0].flush(USER);

    httpMock.expectOne('/api/v1/a').flush({});
    httpMock.expectOne('/api/v1/b').flush({});
  });
});
