import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, computed, effect, inject, signal } from '@angular/core';
import type { HubConnection } from '@microsoft/signalr';
import { Observable, tap } from 'rxjs';
import { PagedResult } from '../models/notification.models';
import {
  BlockedUser,
  ChatMember,
  ChatMessage,
  ChatSearchResult,
  Conversation,
  ConversationDetail,
  DirectMessageSent,
  InquiryMessageSent,
  InquiryThreadRef,
  MessagePage,
  TypingSignal,
} from '../models/chat.models';
import { AuthService } from './auth.service';

/** Client-side debounce for the typing signal: at most one call per this many ms while composing. */
const TYPING_DEBOUNCE_MS = 3000;

/** How long a received typing signal stays live locally if no refresh arrives. */
const TYPING_EXPIRY_MS = 5000;

/**
 * Chat client (feature 019). Owns the Chat nav badge, the inbox and the open conversation as signals,
 * seeded and paged over REST and kept live by a SignalR connection to `/hubs/chat`.
 *
 * Mirrors `NotificationService` (feature 010) deliberately, including the dynamic import and the
 * follow-auth-state lifecycle.
 *
 * The realtime channel is **best-effort**: every value it delivers is also reachable over REST, so the
 * inbox, badge and history stay correct with the socket down (it re-seeds on connect/reconnect). The
 * server is the boundary — everything here is UX state only, and none of it is trusted for access.
 */
@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly base = '/api/v1/chat';

  private readonly _unread = signal(0);
  private readonly _conversations = signal<Conversation[]>([]);
  private readonly _messages = signal<ChatMessage[]>([]);
  private readonly _openId = signal<string | null>(null);
  private readonly _nextBefore = signal<string | null>(null);
  private readonly _typing = signal<TypingSignal[]>([]);

  /** Total unread for the Chat nav badge. */
  readonly unreadCount = this._unread.asReadonly();
  /** The inbox rows, most recently active first. */
  readonly conversations = this._conversations.asReadonly();
  /** Messages of the open conversation, oldest first (the API returns newest first; we reverse for rendering). */
  readonly messages = this._messages.asReadonly();
  readonly openConversationId = this._openId.asReadonly();
  /** True while older history remains to page in. */
  readonly hasMoreHistory = computed(() => this._nextBefore() !== null);

  /** Who is typing right now, expiry already applied. */
  readonly typing = computed(() => {
    const now = Date.now();
    return this._typing().filter((t) => t.expiresAt > now);
  });

  /** Names of people typing in the open conversation. */
  readonly typingHere = computed(() => {
    const id = this._openId();
    return id ? this.typing().filter((t) => t.conversationId === id) : [];
  });

  private hub?: HubConnection;
  private connecting = false;
  private lastTypingSentAt = 0;
  private typingSweep?: ReturnType<typeof setInterval>;

  constructor() {
    // Follow auth state: connect + seed when signed in, tear down + clear on sign-out, so an
    // anonymous client never holds a stream or stale state.
    effect(() => {
      if (this.auth.isAuthenticated()) {
        this.refreshUnread();
        void this.connect();
        this.startTypingSweep();
      } else {
        this.disconnect();
        this._conversations.set([]);
        this._messages.set([]);
        this._openId.set(null);
        this._unread.set(0);
        this._typing.set([]);
      }
    });
  }

  // --- Inbox ----------------------------------------------------------------

  loadInbox(take = 20): Observable<PagedResult<Conversation>> {
    return this.http
      .get<PagedResult<Conversation>>(`${this.base}/conversations`, {
        params: new HttpParams().set('skip', 0).set('take', take),
      })
      .pipe(tap((page) => this._conversations.set([...page.items])));
  }

  refreshUnread(): void {
    this.http
      .get<{ unreadCount: number }>(`${this.base}/conversations/unread-count`)
      .subscribe({
        next: (r) => this._unread.set(r.unreadCount),
        error: () => undefined,
      });
  }

  getDetail(conversationId: string): Observable<ConversationDetail> {
    return this.http.get<ConversationDetail>(`${this.base}/conversations/${conversationId}`);
  }

  getMembers(conversationId: string, take = 50): Observable<PagedResult<ChatMember>> {
    return this.http.get<PagedResult<ChatMember>>(`${this.base}/conversations/${conversationId}/members`, {
      params: new HttpParams().set('skip', 0).set('take', take),
    });
  }

  // --- Conversations --------------------------------------------------------

  /** One participant → a DM; two or more → a named group. The server decides and returns which. */
  start(participantUserIds: string[], name: string | null): Observable<Conversation> {
    return this.http
      .post<Conversation>(`${this.base}/conversations`, { participantUserIds, name })
      .pipe(tap((c) => this.upsertConversation(c)));
  }

  /**
   * Send the FIRST message to a player, creating the direct conversation on the fly (feature 022 —
   * lazy DM creation). This is how a new DM comes into existence: opening a compose view persists
   * nothing; only this call creates the conversation. Returns the (possibly newly created)
   * conversation id + the message so the caller can navigate into the real thread.
   */
  sendDirect(targetUserId: string, body: string): Observable<DirectMessageSent> {
    return this.http
      .post<DirectMessageSent>(`${this.base}/direct/${targetUserId}/messages`, { body })
      // Drop the new thread into the inbox rail immediately (mirrors start()), so it shows without a reload.
      .pipe(tap((r) => this.upsertConversation(r.conversation)));
  }

  /**
   * Send the FIRST message to a team's admins, creating the inquiry thread on the fly (feature 027 —
   * contact the admins). Like {@link sendDirect}, opening the entry point persists nothing; only this
   * call creates the thread. Returns the (possibly newly created) conversation + message.
   */
  sendInquiryToTeam(teamId: string, body: string): Observable<InquiryMessageSent> {
    return this.http
      .post<InquiryMessageSent>(`${this.base}/contact/team/${teamId}/messages`, { body })
      .pipe(tap((r) => this.upsertConversation(r.conversation)));
  }

  /** Send the FIRST message to an event's admins (feature 027). See {@link sendInquiryToTeam}. */
  sendInquiryToEvent(eventId: string, body: string): Observable<InquiryMessageSent> {
    return this.http
      .post<InquiryMessageSent>(`${this.base}/contact/event/${eventId}/messages`, { body })
      .pipe(tap((r) => this.upsertConversation(r.conversation)));
  }

  /** The caller's existing inquiry thread id for a team, or null (feature 027). Creates nothing. */
  findTeamInquiry(teamId: string): Observable<InquiryThreadRef> {
    return this.http.get<InquiryThreadRef>(`${this.base}/contact/team/${teamId}`);
  }

  /** The caller's existing inquiry thread id for an event, or null (feature 027). Creates nothing. */
  findEventInquiry(eventId: string): Observable<InquiryThreadRef> {
    return this.http.get<InquiryThreadRef>(`${this.base}/contact/event/${eventId}`);
  }

  addMembers(conversationId: string, userIds: string[]): Observable<void> {
    return this.http.post<void>(`${this.base}/conversations/${conversationId}/members`, { userIds });
  }

  leave(conversationId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/conversations/${conversationId}/members/me`).pipe(
      tap(() => this._conversations.update((cs) => cs.filter((c) => c.id !== conversationId))),
    );
  }

  setState(conversationId: string, patch: { isMuted?: boolean; isHidden?: boolean }): Observable<void> {
    return this.http.patch<void>(`${this.base}/conversations/${conversationId}/state`, patch).pipe(
      tap(() => {
        if (patch.isHidden) {
          this._conversations.update((cs) => cs.filter((c) => c.id !== conversationId));
        } else if (patch.isMuted !== undefined) {
          this.patchConversation(conversationId, { isMuted: patch.isMuted });
        }
      }),
    );
  }

  // --- Messages -------------------------------------------------------------

  /** Open a conversation: replace the thread with its newest page and mark it read. */
  openConversation(conversationId: string): Observable<MessagePage> {
    this._openId.set(conversationId);
    this._messages.set([]);
    this._nextBefore.set(null);

    return this.http.get<MessagePage>(`${this.base}/conversations/${conversationId}/messages`).pipe(
      tap((page) => {
        // The API pages newest-first (keyset); the thread renders oldest-first.
        this._messages.set([...page.items].reverse());
        this._nextBefore.set(page.nextBefore);
        this.markReadToLatest(conversationId);
      }),
    );
  }

  /** Page further back. Keyset on the message id — a message arriving mid-scroll cannot shift a page. */
  loadOlder(conversationId: string): Observable<MessagePage> {
    const before = this._nextBefore();
    let params = new HttpParams();
    if (before) {
      params = params.set('before', before);
    }

    return this.http
      .get<MessagePage>(`${this.base}/conversations/${conversationId}/messages`, { params })
      .pipe(
        tap((page) => {
          this._messages.update((existing) => [...[...page.items].reverse(), ...existing]);
          this._nextBefore.set(page.nextBefore);
        }),
      );
  }

  send(conversationId: string, body: string): Observable<ChatMessage> {
    return this.http
      .post<ChatMessage>(`${this.base}/conversations/${conversationId}/messages`, { body })
      .pipe(
        tap((message) => {
          if (this._openId() === conversationId) {
            this.appendMessage(message);
          }
          // Refresh the inbox row's preview + ordering for the sender too. The server only pushes the
          // new message to the OTHER members, so without this our own left rail keeps showing the old
          // preview (e.g. "no messages yet" on a group we just created) until a reload.
          this.bumpConversationWithMessage(conversationId, message);
        }),
      );
  }

  deleteMessage(messageId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/messages/${messageId}`).pipe(
      tap(() => this.tombstone(messageId)),
    );
  }

  markRead(conversationId: string, lastReadMessageId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/conversations/${conversationId}/read`, { lastReadMessageId });
  }

  /** Mark read up to whatever is currently newest in the open thread. */
  markReadToLatest(conversationId: string): void {
    const all = this._messages();
    const latest = all.length > 0 ? all[all.length - 1] : undefined;
    if (!latest) {
      return;
    }

    this.markRead(conversationId, latest.id).subscribe({
      next: () => {
        this.patchConversation(conversationId, { unreadCount: 0 });
        this.refreshUnread();
      },
      error: () => undefined,
    });
  }

  /**
   * Tell the server we're typing, at most once per {@link TYPING_DEBOUNCE_MS}. Debounced here rather
   * than sending per keystroke — a request per keypress would be abusive, and the signal carries an
   * expiry so it does not need to be continuous.
   */
  signalTyping(conversationId: string): void {
    const now = Date.now();
    if (now - this.lastTypingSentAt < TYPING_DEBOUNCE_MS) {
      return;
    }
    this.lastTypingSentAt = now;

    this.http.post<void>(`${this.base}/conversations/${conversationId}/typing`, {}).subscribe({
      error: () => undefined, // typing is cosmetic; never surface a failure
    });
  }

  // --- Search & blocks ------------------------------------------------------

  search(term: string, take = 20): Observable<ChatSearchResult> {
    return this.http.get<ChatSearchResult>(`${this.base}/search`, {
      params: new HttpParams().set('q', term).set('skip', 0).set('take', take),
    });
  }

  listBlocks(take = 50): Observable<PagedResult<BlockedUser>> {
    return this.http.get<PagedResult<BlockedUser>>(`${this.base}/blocks`, {
      params: new HttpParams().set('skip', 0).set('take', take),
    });
  }

  block(userId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/blocks`, { userId });
  }

  unblock(userId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/blocks/${userId}`);
  }

  // --- Local state helpers --------------------------------------------------

  private appendMessage(message: ChatMessage): void {
    this._messages.update((ms) => (ms.some((m) => m.id === message.id) ? ms : [...ms, message]));
  }

  private tombstone(messageId: string): void {
    this._messages.update((ms) =>
      ms.map((m) => (m.id === messageId ? { ...m, isDeleted: true, body: '', linkCard: null } : m)),
    );
  }

  private upsertConversation(c: Conversation): void {
    this._conversations.update((cs) => {
      const rest = cs.filter((x) => x.id !== c.id);
      return [c, ...rest];
    });
  }

  private patchConversation(id: string, patch: Partial<Conversation>): void {
    this._conversations.update((cs) => cs.map((c) => (c.id === id ? { ...c, ...patch } : c)));
  }

  // --- Realtime -------------------------------------------------------------

  /**
   * Open the realtime connection. The SignalR client is loaded on demand (dynamic import) so its
   * ~50 kB stays out of the initial bundle — matching the notifications client.
   */
  private async connect(): Promise<void> {
    if (this.hub || this.connecting) {
      return;
    }
    this.connecting = true;

    try {
      const { HubConnectionBuilder, LogLevel } = await import('@microsoft/signalr');

      // Sign-out may have raced the dynamic import.
      if (!this.auth.isAuthenticated()) {
        return;
      }

      const hub = new HubConnectionBuilder()
        .withUrl('/hubs/chat') // same-origin: the httpOnly auth cookie rides the handshake
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();

      hub.on('chatMessageCreated', (e: { conversationId: string; message: ChatMessage }) =>
        this.onMessageCreated(e.conversationId, e.message),
      );
      hub.on('chatMessageDeleted', (e: { conversationId: string; messageId: string }) =>
        this.tombstone(e.messageId),
      );
      hub.on('chatUnreadCountChanged', (e: { unreadCount: number }) => this._unread.set(e.unreadCount));
      hub.on('chatConversationUpserted', (e: { conversation: Conversation }) =>
        this.upsertConversation(e.conversation),
      );
      hub.on('chatTyping', (e: { conversationId: string; userId: string; displayName: string; expiresInMs: number }) =>
        this.onTyping(e),
      );

      hub.onreconnected(() => {
        // A short outage may have missed events — re-seed everything the socket would have carried.
        // This is what makes "live is an enhancement, never the source of truth" true on the client.
        this.refreshUnread();
        if (this._conversations().length > 0) {
          this.loadInbox().subscribe({ error: () => undefined });
        }
        const open = this._openId();
        if (open) {
          this.openConversation(open).subscribe({ error: () => undefined });
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

  private onMessageCreated(conversationId: string, message: ChatMessage): void {
    if (this._openId() === conversationId) {
      this.appendMessage(message);
    }

    // Keep the row's preview and ordering fresh even when the conversation isn't open.
    this.bumpConversationWithMessage(conversationId, message);
  }

  /**
   * Update a conversation's inbox row from a new message: refresh its preview + time, move it to the
   * top, and bump the unread count unless the message is the caller's own or the conversation is open.
   * Shared by the caller's own sends and messages arriving over the socket, so the left rail stays in
   * step without a reload. If the row isn't known yet, re-seed the inbox.
   */
  private bumpConversationWithMessage(conversationId: string, message: ChatMessage): void {
    this._conversations.update((cs) => {
      const found = cs.find((c) => c.id === conversationId);
      if (!found) {
        // A conversation we don't know about yet (someone just started one with us) — re-seed.
        this.loadInbox().subscribe({ error: () => undefined });
        return cs;
      }

      const isOpen = this._openId() === conversationId;
      const updated: Conversation = {
        ...found,
        lastMessage: {
          preview: message.isDeleted ? '' : message.body,
          at: message.sentAt,
          senderName: message.senderName,
          isOwn: message.isOwn,
          isSystem: message.kind === 'System',
        },
        // An open conversation is being read as it arrives, so it never accrues a badge.
        unreadCount: isOpen || message.isOwn ? found.unreadCount : found.unreadCount + 1,
      };

      return [updated, ...cs.filter((c) => c.id !== conversationId)];
    });
  }

  private onTyping(e: { conversationId: string; userId: string; displayName: string; expiresInMs: number }): void {
    const expiresAt = Date.now() + (e.expiresInMs || TYPING_EXPIRY_MS);
    this._typing.update((ts) => [
      ...ts.filter((t) => !(t.conversationId === e.conversationId && t.userId === e.userId)),
      { conversationId: e.conversationId, userId: e.userId, displayName: e.displayName, expiresAt },
    ]);
  }

  /**
   * Drop expired typing signals on a timer.
   *
   * The `typing` computed already filters by expiry, but a signal alone never re-evaluates as time
   * passes — without this the indicator would linger until some other event happened to nudge it. This
   * is what makes a typist who closes their tab mid-word stop typing.
   */
  private startTypingSweep(): void {
    this.typingSweep ??= setInterval(() => {
      const now = Date.now();
      this._typing.update((ts) => (ts.some((t) => t.expiresAt <= now) ? ts.filter((t) => t.expiresAt > now) : ts));
    }, 1000);
  }

  private disconnect(): void {
    void this.hub?.stop().catch(() => undefined);
    this.hub = undefined;

    if (this.typingSweep) {
      clearInterval(this.typingSweep);
      this.typingSweep = undefined;
    }
  }
}
