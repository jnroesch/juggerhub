import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import type {
  HttpClient as HubHttpClient,
  HttpRequest as HubHttpRequest,
  HttpResponse as HubHttpResponse,
} from '@microsoft/signalr';
import { firstValueFrom } from 'rxjs';
import { AuthService } from './auth.service';

/** The parts of the SignalR client this needs. The client is loaded on demand, so it is handed in. */
type SignalR = Pick<typeof import('@microsoft/signalr'), 'HttpClient' | 'HttpError' | 'DefaultHttpClient' | 'NullLogger'>;

/**
 * Keeps the chat and notification hubs connected across the end of an access token (GH #402).
 *
 * The server closes a hub connection when the access token it was made with expires — about every
 * fifteen minutes in every open tab — and refuses a handshake made with an expired one. The client
 * reconnects by itself, but its handshake is SignalR's own request: it does not pass through the
 * HTTP interceptor, so nothing gives it the silent refresh every other call gets. Without this, a
 * signed-in member's live updates would stop at the first expiry and stay stopped until they
 * happened to click something.
 *
 * So the handshake gets the interceptor's rule: a 401 renews the session once and the request is
 * repeated. The refresh is the same single-flight call the interceptor uses, so the two hubs — which
 * are closed in the same second — and any REST call failing alongside them share one rotation.
 *
 * If the session cannot be renewed because it is over (signed out in another tab, a password reset,
 * a suspension or a ban), the client says so the way the interceptor does: local state is cleared,
 * which closes both hubs, and the tab goes to sign-in instead of sitting on a page it may no longer
 * read. Anything else — offline, a 5xx — leaves the session alone; the reconnect policy tries again.
 */
@Injectable({ providedIn: 'root' })
export class HubSessionService {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /**
   * The HTTP client a hub connection should make its requests with (`withUrl`'s `httpClient`).
   *
   * Only the negotiate request is repeated. It is where every connection attempt starts, and a
   * refused one has created nothing on the server. With WebSockets it is also the only request
   * there is; on the fallback transports a refused poll ends the connection, and the reconnect
   * that follows arrives here.
   */
  createHttpClient(
    signalR: SignalR,
    inner: HubHttpClient = new signalR.DefaultHttpClient(signalR.NullLogger.instance),
  ): HubHttpClient {
    const renew = () => this.renew();

    return new (class extends signalR.HttpClient {
      override async send(request: HubHttpRequest): Promise<HubHttpResponse> {
        try {
          return await inner.send(request);
        } catch (error) {
          const refused =
            error instanceof signalR.HttpError && error.statusCode === 401 && isNegotiate(request);
          if (!refused || !(await renew())) {
            throw error;
          }
          return inner.send(request);
        }
      }

      override getCookieString(url: string): string {
        return inner.getCookieString(url);
      }
    })();
  }

  /** Renews the session after a refused handshake. True when there is a fresh one to retry with. */
  private async renew(): Promise<boolean> {
    // Signed out while the handshake was in flight: nothing to renew, and nowhere to send anyone.
    if (!this.auth.isAuthenticated()) {
      return false;
    }

    try {
      await firstValueFrom(this.auth.refreshSession());
      return true;
    } catch (error) {
      // Only the server saying so ends the session. A refresh that failed for any other reason
      // (offline, a gateway error) says nothing about it, and a hub retries in the background for
      // as long as the network is down — signing the member out for that would be wrong.
      if (error instanceof HttpErrorResponse && error.status === 401) {
        this.auth.clearSession();
        void this.router.navigate(['/sign-in']);
      }
      return false;
    }
  }
}

function isNegotiate(request: HubHttpRequest): boolean {
  return request.url?.includes('/negotiate?') === true;
}
