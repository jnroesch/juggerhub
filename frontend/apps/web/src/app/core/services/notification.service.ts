import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, computed, effect, inject, signal } from '@angular/core';
import type { HubConnection } from '@microsoft/signalr';
import { Observable, tap } from 'rxjs';
import { AppNotification, PagedResult, UnreadCount } from '../models/notification.models';
import { AuthService } from './auth.service';
import { IndefiniteHubRetryPolicy } from './hub-reconnect.policy';

/**
 * In-app notifications client (feature 010). Owns the app-wide unread badge and the Alerts inbox
 * list as signals, seeded and paged over REST and kept live by a SignalR connection to
 * `/hubs/notifications`. The server is the boundary — this is UX state only.
 *
 * The realtime channel is best-effort: every value it delivers is also reachable over REST, so the
 * inbox and badge stay correct if the socket is down (it re-seeds on connect/reconnect and on
 * navigation to Alerts). The connection follows auth state — opened when signed in, closed on
 * sign-out — so an anonymous client never holds a stream.
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly base = '/api/v1/notifications';

  private readonly _unread = signal(0);
  private readonly _items = signal<AppNotification[]>([]);
  private readonly _total = signal(0);

  /** Unread count for the bell badge. */
  readonly unreadCount = this._unread.asReadonly();
  /** The loaded inbox items (first page + any paged-in more + realtime prepends). */
  readonly items = this._items.asReadonly();
  /** Total server-side count, so the inbox knows when there is more to load. */
  readonly total = this._total.asReadonly();
  readonly hasMore = computed(() => this._items().length < this._total());

  private hub?: HubConnection;
  private connecting = false;

  constructor() {
    // Follow auth state: connect + seed when signed in, tear down + clear on sign-out.
    effect(() => {
      if (this.auth.isAuthenticated()) {
        this.refreshUnread();
        void this.connect();
      } else {
        this.disconnect();
        this._items.set([]);
        this._total.set(0);
        this._unread.set(0);
      }
    });
  }

  // --- REST reads -----------------------------------------------------------

  /** (Re)load the newest page into the inbox, replacing what's there. */
  loadFirstPage(take = 20): Observable<PagedResult<AppNotification>> {
    return this.http
      .get<PagedResult<AppNotification>>(this.base, {
        params: new HttpParams().set('skip', 0).set('take', take),
      })
      .pipe(
        tap((page) => {
          this._items.set(page.items);
          this._total.set(page.totalCount);
        }),
      );
  }

  /** Append the next page (pagination is mandatory; never load unbounded). */
  loadMore(take = 20): Observable<PagedResult<AppNotification>> {
    const skip = this._items().length;
    return this.http
      .get<PagedResult<AppNotification>>(this.base, {
        params: new HttpParams().set('skip', skip).set('take', take),
      })
      .pipe(
        tap((page) => {
          this._items.update((current) => [...current, ...page.items]);
          this._total.set(page.totalCount);
        }),
      );
  }

  /** Re-seed the badge count from the server (used on init and on (re)connect). */
  refreshUnread(): void {
    this.http.get<UnreadCount>(`${this.base}/unread-count`).subscribe({
      next: (c) => this._unread.set(c.count),
      error: () => {
        /* leave the last known count; a later push/refresh reconciles */
      },
    });
  }

  // --- Mutations ------------------------------------------------------------

  markRead(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/read`, {}).pipe(
      tap(() => {
        this.patchItem(id, { isRead: true });
        // Optimistic; the server also pushes the authoritative count.
        this._unread.update((n) => Math.max(0, n - 1));
      }),
    );
  }

  markAllRead(): Observable<void> {
    return this.http.post<void>(`${this.base}/read-all`, {}).pipe(
      tap(() => {
        this._items.update((items) => items.map((i) => ({ ...i, isRead: true })));
        this._unread.set(0);
      }),
    );
  }

  /**
   * After an inline invite action (accept/decline via the invitation endpoints), the underlying
   * invite is resolved — reflect that locally and mark the row read without another round trip.
   */
  markInviteResolved(id: string): void {
    this.patchItem(id, { resolved: true, isRead: true });
    this._unread.update((n) => Math.max(0, n - 1));
  }

  private patchItem(id: string, patch: Partial<AppNotification>): void {
    this._items.update((items) => items.map((i) => (i.id === id ? { ...i, ...patch } : i)));
  }

  // --- Realtime -------------------------------------------------------------

  /**
   * Open the realtime connection. The SignalR client is loaded on demand (dynamic import) so its
   * ~50 kB stays out of the initial bundle — it's only pulled once a signed-in user needs it.
   */
  private async connect(): Promise<void> {
    if (this.hub || this.connecting) {
      return;
    }
    this.connecting = true;

    try {
      const { HubConnectionBuilder, LogLevel } = await import('@microsoft/signalr');

      // Sign-out may have raced the dynamic import — bail if we're no longer authenticated.
      if (!this.auth.isAuthenticated()) {
        return;
      }

      const hub = new HubConnectionBuilder()
        .withUrl('/hubs/notifications') // same-origin: the httpOnly auth cookie rides the handshake
        // Indefinite backoff rather than the default schedule, which gives up permanently after
        // ~42s and silently stops delivering (feature 028, FR-012).
        .withAutomaticReconnect(new IndefiniteHubRetryPolicy())
        .configureLogging(LogLevel.Warning)
        .build();

      hub.on('notificationCreated', (n: AppNotification) => this.onCreated(n));
      hub.on('unreadCountChanged', (count: number) => this._unread.set(count));
      hub.onreconnected(() => {
        this.refreshUnread();
        // A short outage may have missed creates; re-seed the newest page if the inbox is loaded.
        if (this._items().length > 0) {
          this.loadFirstPage().subscribe({ error: () => undefined });
        }
      });

      this.hub = hub;
      await hub.start().catch(() => {
        /* REST path stays correct; auto-reconnect will retry */
      });
    } catch {
      /* couldn't load/start the client — the REST path keeps everything correct */
    } finally {
      this.connecting = false;
    }
  }

  private disconnect(): void {
    this.hub?.stop().catch(() => undefined);
    this.hub = undefined;
  }

  private onCreated(n: AppNotification): void {
    this._items.update((items) => (items.some((i) => i.id === n.id) ? items : [n, ...items]));
    this._total.update((t) => t + 1);
    // The badge is set authoritatively by the paired unreadCountChanged push.
  }
}
