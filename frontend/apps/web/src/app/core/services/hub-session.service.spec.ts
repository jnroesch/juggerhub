import { HttpErrorResponse, provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import * as signalR from '@microsoft/signalr';
import { Observable, of, throwError } from 'rxjs';
import { AuthUser } from '../models/auth.models';
import { AuthService } from './auth.service';
import { HubSessionService } from './hub-session.service';

const USER: AuthUser = {
  id: 'u1',
  email: 'a@example.com',
  emailConfirmed: true,
  onboardingCompleted: true,
  handle: 'a-handle',
  hasAvatar: false,
  preferredLanguage: null,
};
const NEGOTIATE = 'http://localhost/hubs/chat/negotiate?negotiateVersion=1';

const refused = () => new signalR.HttpError('Unauthorized', 401);
const accepted = () => new signalR.HttpResponse(200, 'OK', '{}');

/** Lets the promise chain inside `send` run up to its next real wait. */
const settle = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 0));

/** Stands in for SignalR's own client: the requests it would have made, and what they answer. */
const transport = () => {
  const send = jest.fn<Promise<signalR.HttpResponse>, [signalR.HttpRequest]>();
  const inner = { send, getCookieString: () => '' } as unknown as signalR.HttpClient;
  return { send, inner };
};

/**
 * GH #402 — the server ends a hub connection when its access token expires and refuses a handshake
 * made with an expired one. SignalR's requests bypass the HTTP interceptor, so this is the only
 * thing between a signed-in member and live updates that stop after fifteen minutes.
 */
describe('HubSessionService', () => {
  describe('a refused handshake', () => {
    const authed = signal(true);
    let refresh: jest.Mock<Observable<AuthUser>, []>;
    let clearSession: jest.Mock;
    let navigate: jest.SpyInstance;
    let service: HubSessionService;

    beforeEach(() => {
      authed.set(true);
      refresh = jest.fn(() => of(USER));
      clearSession = jest.fn();

      TestBed.configureTestingModule({
        providers: [
          { provide: AuthService, useValue: { isAuthenticated: authed, refreshSession: refresh, clearSession } },
        ],
      });

      service = TestBed.inject(HubSessionService);
      navigate = jest.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    });

    it('passes an accepted request straight through', async () => {
      const { send, inner } = transport();
      send.mockResolvedValue(accepted());

      const response = await service.createHttpClient(signalR, inner).post(NEGOTIATE);

      expect(response.statusCode).toBe(200);
      expect(send).toHaveBeenCalledTimes(1);
      expect(refresh).not.toHaveBeenCalled();
    });

    it('renews the session and repeats the handshake', async () => {
      const { send, inner } = transport();
      send.mockRejectedValueOnce(refused()).mockResolvedValueOnce(accepted());

      const response = await service.createHttpClient(signalR, inner).post(NEGOTIATE);

      expect(response.statusCode).toBe(200);
      expect(refresh).toHaveBeenCalledTimes(1);
      expect(send).toHaveBeenCalledTimes(2);
      expect(clearSession).not.toHaveBeenCalled();
    });

    it('repeats it once, not for as long as it is refused', async () => {
      const { send, inner } = transport();
      send.mockRejectedValue(refused());

      await expect(service.createHttpClient(signalR, inner).post(NEGOTIATE)).rejects.toMatchObject({ statusCode: 401 });

      // The reconnect policy owns the next attempt, with its backoff.
      expect(send).toHaveBeenCalledTimes(2);
      expect(refresh).toHaveBeenCalledTimes(1);
      // A renewed session that is still refused is not a session that ended.
      expect(clearSession).not.toHaveBeenCalled();
    });

    it('ends the session when it cannot be renewed', async () => {
      const { send, inner } = transport();
      send.mockRejectedValue(refused());
      refresh.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 401 })));

      await expect(service.createHttpClient(signalR, inner).post(NEGOTIATE)).rejects.toMatchObject({ statusCode: 401 });

      // Signed out elsewhere, a password reset, a suspension, a ban: the tab stops showing a
      // signed-in page, and clearing the session is what closes both hubs.
      expect(clearSession).toHaveBeenCalledTimes(1);
      expect(navigate).toHaveBeenCalledWith(['/sign-in']);
      expect(send).toHaveBeenCalledTimes(1);
    });

    it.each([0, 500, 503])('keeps the session when the refresh itself fails with %i', async (status) => {
      const { send, inner } = transport();
      send.mockRejectedValue(refused());
      refresh.mockReturnValue(throwError(() => new HttpErrorResponse({ status })));

      await expect(service.createHttpClient(signalR, inner).post(NEGOTIATE)).rejects.toMatchObject({ statusCode: 401 });

      // Offline or a gateway error says nothing about the session. A hub retries in the background
      // for as long as the network is down; none of those attempts may sign the member out.
      expect(clearSession).not.toHaveBeenCalled();
      expect(navigate).not.toHaveBeenCalled();
    });

    it('renews nothing for a client that signed out while the handshake was in flight', async () => {
      const { send, inner } = transport();
      send.mockRejectedValue(refused());
      authed.set(false);

      await expect(service.createHttpClient(signalR, inner).post(NEGOTIATE)).rejects.toMatchObject({ statusCode: 401 });

      expect(refresh).not.toHaveBeenCalled();
      expect(navigate).not.toHaveBeenCalled();
    });

    it('leaves a handshake that failed for another reason to the reconnect policy', async () => {
      const { send, inner } = transport();
      send.mockRejectedValue(new signalR.HttpError('Bad Gateway', 502));

      await expect(service.createHttpClient(signalR, inner).post(NEGOTIATE)).rejects.toMatchObject({ statusCode: 502 });

      expect(refresh).not.toHaveBeenCalled();
      expect(send).toHaveBeenCalledTimes(1);
    });

    it('repeats only the handshake, never a transport request', async () => {
      const { send, inner } = transport();
      send.mockRejectedValue(refused());

      // A long-polling poll: refused, it ends the connection, and the reconnect negotiates again.
      await expect(
        service.createHttpClient(signalR, inner).get('http://localhost/hubs/chat?id=abc'),
      ).rejects.toMatchObject({ statusCode: 401 });

      expect(refresh).not.toHaveBeenCalled();
      expect(send).toHaveBeenCalledTimes(1);
    });
  });

  /**
   * Both hubs are closed in the same second, so both handshakes are refused together. The refresh
   * token rotates on use: two refreshes would race each other for it.
   */
  it('shares one refresh between the two hubs', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(withXhr()), provideHttpClientTesting()] });
    const httpMock = TestBed.inject(HttpTestingController);
    TestBed.inject(AuthService).login({ email: 'a@example.com', password: 'pw', rememberMe: false }).subscribe();
    httpMock.expectOne('/api/v1/auth/login').flush(USER);
    const service = TestBed.inject(HubSessionService);

    const chat = transport();
    const alerts = transport();
    chat.send.mockRejectedValueOnce(refused()).mockResolvedValueOnce(accepted());
    alerts.send.mockRejectedValueOnce(refused()).mockResolvedValueOnce(accepted());

    const both = Promise.all([
      service.createHttpClient(signalR, chat.inner).post(NEGOTIATE),
      service.createHttpClient(signalR, alerts.inner).post('http://localhost/hubs/notifications/negotiate?negotiateVersion=1'),
    ]);
    await settle();

    const refreshes = httpMock.match('/api/v1/auth/refresh');
    expect(refreshes.length).toBe(1);
    refreshes[0].flush(USER);

    expect((await both).map((r) => r.statusCode)).toEqual([200, 200]);
    httpMock.verify();
  });
});
