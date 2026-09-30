import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, computed, effect, inject, signal } from '@angular/core';
import type { HubConnection } from '@microsoft/signalr';
import { Observable, tap } from 'rxjs';
import { AppNotification, PagedResult, UnreadCount } from '../models/notification.models';
import { AuthService } from './auth.service';
import { IndefiniteHubRetryPolicy } from './hub-reconnect.policy';
import { HubSessionService } from './hub-session.service';

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
  private readonly hubSession = inject(HubSessionService);
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
  /**
   * Whether the Alerts list has been loaded in this session. Not "holds any alerts": a loaded list
   * can be empty, and that is exactly the one an alert raised during a reconnect must reach.
   */
  private inboxLoaded = false;
  /** How often the list has been loaded from scratch. A catch-up only merges into the list it asked for. */
  private listLoads = 0;

  constructor() {
    // Follow auth state: connect + seed when signed in, tear down + clear on sign-out.
    effect(() => {
      if (this.auth.isAuthenticated()) {
        this.refreshUnread();
        void this.connect();
      } else {
        this.disconnect();
        this.inboxLoaded = false;
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
          this.inboxLoaded = true;
          this.listLoads++;
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
      const signalR = await import('@microsoft/signalr');

      // Sign-out may have raced the dynamic import — bail if we're no longer authenticated.
      if (!this.auth.isAuthenticated()) {
        return;
      }

      const hub = new signalR.HubConnectionBuilder()
        // Same-origin: the httpOnly auth cookie rides the handshake. The server ends the connection
        // when that cookie's token expires, so the handshake renews the session when it is refused
        // (GH #402) — see HubSessionService.
        .withUrl('/hubs/notifications', { httpClient: this.hubSession.createHttpClient(signalR) })
        // Indefinite backoff rather than the default schedule, which gives up permanently after
        // ~42s and silently stops delivering (feature 028, FR-012).
        .withAutomaticReconnect(new IndefiniteHubRetryPolicy())
        .configureLogging(signalR.LogLevel.Warning)
        .build();

      hub.on('notificationCreated', (n: AppNotification) => this.onCreated(n));
      hub.on('unreadCountChanged', (count: number) => this._unread.set(count));
      hub.onreconnected(() => this.onReconnected());

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

  /**
   * Back after the socket was down: re-seed what it would have carried.
   *
   * **It has to be invisible.** A reconnect is routine, not an outage: the server ends every
   * connection when the access token it was made with expires (GH #402), so this runs about four
   * times an hour in every open tab — including under someone reading the Alerts list.
   */
  private onReconnected(): void {
    this.refreshUnread();
    if (this.inboxLoaded) {
      this.catchUpInbox();
    }
  }

  /**
   * Bring the loaded inbox up to date from the newest page, keeping what was paged in below it.
   *
   * Deliberately NOT {@link loadFirstPage}, which this used to call: that replaces the list with
   * the newest page, so everything the reader had loaded further down disappeared under them.
   *
   * The newest page is the truth for its own range — down to the oldest alert it shares with the
   * list. That range is taken from the server as it is (new alerts in, removed ones out, read and
   * resolved states current); what the list holds below it stays. With nothing in common, or
   * nothing held below, the page simply is the list, as before.
   *
   * One thing the page cannot know: an alert pushed over the new socket while this answer was on
   * its way. It is newer than the page, so it stays on top rather than being taken for a removal.
   */
  private catchUpInbox(): void {
    const askedAs = this.auth.currentUser()?.id;
    const loads = this.listLoads;
    const heldWhenAsked = new Set(this._items().map((i) => i.id));

    this.http
      .get<PagedResult<AppNotification>>(this.base, { params: new HttpParams().set('skip', 0).set('take', 20) })
      .subscribe({
        next: (page) => {
          // An answer belongs to the account that asked. Signed out while it was on its way, or
          // signed in as someone else: an anonymous client holds nothing, and nobody holds
          // another account's alerts.
          if (!askedAs || this.auth.currentUser()?.id !== askedAs) {
            return;
          }

          // The list was loaded again while this answer was on its way. That list is as current
          // as the answer, and it is no longer the list the answer was asked for.
          if (this.listLoads !== loads) {
            return;
          }

          // From here the list is the one that was held when asking, plus only two kinds of
          // addition: alerts pushed meanwhile, which go on top, and a page of older ones, which
          // goes at the end. So what sits above the first alert it already held — everything, if
          // it held none — arrived meanwhile.
          const held = this._items();
          const inPage = new Set(page.items.map((i) => i.id));
          const firstOld = held.findIndex((i) => heldWhenAsked.has(i.id));
          const arrivedMeanwhile = (firstOld < 0 ? held : held.slice(0, firstOld)).filter((i) => !inPage.has(i.id));
          const asItWas = firstOld < 0 ? [] : held.slice(firstOld);

          // The oldest alert the page shares with the list as it was marks the end of the page's
          // range. With nothing held below it, the page simply is the list.
          let lastShared = page.items.length - 1;
          while (lastShared >= 0 && !heldWhenAsked.has(page.items[lastShared].id)) {
            lastShared--;
          }

          const below =
            lastShared < 0
              ? []
              : asItWas.slice(asItWas.findIndex((i) => i.id === page.items[lastShared].id) + 1);
          const range = below.length > 0 ? page.items.slice(0, lastShared + 1) : page.items;

          this._items.set([...arrivedMeanwhile, ...range, ...below]);
          this._total.set(page.totalCount + arrivedMeanwhile.length);
        },
        error: () => undefined,
      });
  }

  private onCreated(n: AppNotification): void {
    this._items.update((items) => (items.some((i) => i.id === n.id) ? items : [n, ...items]));
    this._total.update((t) => t + 1);
    // The badge is set authoritatively by the paired unreadCountChanged push.
  }
}
