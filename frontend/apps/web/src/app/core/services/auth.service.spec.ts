import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthUser } from '../models/auth.models';
import { AuthService } from './auth.service';

const USER: AuthUser = { id: 'u1', email: 'a@example.com', emailConfirmed: true, onboardingCompleted: true, handle: 'a-handle' };

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('register POSTs to /api/v1/auth/register', () => {
    service.register({ email: 'a@example.com', password: 'pw' }).subscribe();
    const req = httpMock.expectOne('/api/v1/auth/register');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ email: 'a@example.com', password: 'pw' });
    req.flush({ message: 'ok' });
  });

  it('verifyEmail and resendVerification POST to their endpoints', () => {
    service.verifyEmail({ userId: 'u1', token: 't' }).subscribe();
    httpMock.expectOne('/api/v1/auth/verify-email').flush({ message: 'ok' });

    service.resendVerification({ email: 'a@example.com' }).subscribe();
    httpMock.expectOne('/api/v1/auth/resend-verification').flush({ message: 'ok' });
  });

  it('login sets authenticated state', () => {
    expect(service.isAuthenticated()).toBe(false);

    service.login({ email: 'a@example.com', password: 'pw', rememberMe: true }).subscribe();
    httpMock.expectOne('/api/v1/auth/login').flush(USER);

    expect(service.isAuthenticated()).toBe(true);
    expect(service.currentUser()).toEqual(USER);
  });

  it('logout clears authenticated state', () => {
    service.login({ email: 'a@example.com', password: 'pw', rememberMe: false }).subscribe();
    httpMock.expectOne('/api/v1/auth/login').flush(USER);

    service.logout().subscribe();
    httpMock.expectOne('/api/v1/auth/logout').flush(null);

    expect(service.isAuthenticated()).toBe(false);
    expect(service.currentUser()).toBeNull();
  });

  it('loadSession hydrates from /me', () => {
    service.loadSession().subscribe();
    httpMock.expectOne('/api/v1/auth/me').flush(USER);
    expect(service.currentUser()).toEqual(USER);
  });

  it('loadSession falls back to refresh + /me on a 401', () => {
    let result: AuthUser | null | undefined;
    service.loadSession().subscribe((u) => (result = u));

    httpMock.expectOne('/api/v1/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    httpMock.expectOne('/api/v1/auth/refresh').flush(USER);
    httpMock.expectOne('/api/v1/auth/me').flush(USER);

    expect(result).toEqual(USER);
    expect(service.isAuthenticated()).toBe(true);
  });

  it('forgotPassword and resetPassword POST to their endpoints', () => {
    service.forgotPassword({ email: 'a@example.com' }).subscribe();
    httpMock.expectOne('/api/v1/auth/forgot-password').flush({ message: 'ok' });

    service.resetPassword({ userId: 'u1', token: 't', newPassword: 'pw' }).subscribe();
    const req = httpMock.expectOne('/api/v1/auth/reset-password');
    expect(req.request.method).toBe('POST');
    req.flush({ message: 'ok' });
  });

  it('refreshSession is single-flight: concurrent callers share one request', () => {
    service.refreshSession().subscribe();
    service.refreshSession().subscribe();

    // Only one /auth/refresh is in flight for both subscribers.
    const req = httpMock.expectOne('/api/v1/auth/refresh');
    req.flush(USER);

    // After it settles, a new call issues a fresh request.
    service.refreshSession().subscribe();
    httpMock.expectOne('/api/v1/auth/refresh').flush(USER);
  });
});
