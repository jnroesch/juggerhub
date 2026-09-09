import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { signal } from '@angular/core';
import { ChatService } from './chat.service';
import { AuthService } from './auth.service';
import { ChatMessage, Conversation } from '../models/chat.models';

/**
 * ChatService (feature 019). Angular 21 zoneless — no `fakeAsync` (the 014 convention).
 *
 * The service is UX state only; the server is the boundary. What these tests pin is that the state
 * stays coherent — most importantly that REST alone is enough, since realtime is an enhancement and
 * never the source of truth (FR-023).
 */
describe('ChatService', () => {
  let service: ChatService;
  let httpMock: HttpTestingController;
  const authed = signal(true);

  const conversation = (over: Partial<Conversation> = {}): Conversation => ({
    id: 'c1',
    kind: 'Direct',
    name: 'Ben R.',
    avatar: { kind: 'User', userId: 'u2', teamId: null, url: null },
    lastMessage: null,
    unreadCount: 0,
    isMuted: false,
    state: 'Active',
    teamId: null,
    partyId: null,
    ...over,
  });

  const message = (over: Partial<ChatMessage> = {}): ChatMessage => ({
    id: 'm1',
    kind: 'Member',
    senderId: 'u2',
    senderName: 'Ben R.',
    isOwn: false,
    body: 'hello',
    sentAt: '2026-07-16T19:38:00Z',
    isDeleted: false,
    readState: null,
    systemEvent: null,
    systemSubjectName: null,
    linkCard: null,
    ...over,
  });

  beforeEach(() => {
    authed.set(true);

    TestBed.configureTestingModule({
      providers: [
        ChatService,
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { isAuthenticated: authed } },
      ],
    });

    service = TestBed.inject(ChatService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify({ ignoreCancelled: true }));

  /** The constructor effect fires an unread refresh; flush it so each test starts clean. */
  const flushInitialUnread = (count = 0) => {
    const reqs = httpMock.match('/api/v1/chat/conversations/unread-count');
    reqs.forEach((r) => r.flush({ unreadCount: count }));
  };

  it('seeds the unread badge when signed in', () => {
    TestBed.tick();
    const req = httpMock.expectOne('/api/v1/chat/conversations/unread-count');
    req.flush({ unreadCount: 8 });

    expect(service.unreadCount()).toBe(8);
  });

  it('loads the inbox', () => {
    TestBed.tick();
    flushInitialUnread();

    service.loadInbox().subscribe();

    const req = httpMock.expectOne('/api/v1/chat/conversations?skip=0&take=20');
    expect(req.request.method).toBe('GET');
    req.flush({ items: [conversation()], totalCount: 1, skip: 0, take: 20 });

    expect(service.conversations().length).toBe(1);
    expect(service.conversations()[0].name).toBe('Ben R.');
  });

  it('reverses the keyset page so the thread reads oldest-first', () => {
    TestBed.tick();
    flushInitialUnread();

    service.openConversation('c1').subscribe();

    // The API returns newest-first.
    httpMock.expectOne('/api/v1/chat/conversations/c1/messages').flush({
      items: [message({ id: 'm2', body: 'second' }), message({ id: 'm1', body: 'first' })],
      nextBefore: null,
    });

    // Marking read fires on open.
    httpMock.expectOne('/api/v1/chat/conversations/c1/read').flush(null);
    httpMock.match('/api/v1/chat/conversations/unread-count').forEach((r) => r.flush({ unreadCount: 0 }));

    expect(service.messages().map((m) => m.body)).toEqual(['first', 'second']);
  });

  /** Open 'c1' with one page of history and a cursor to page back from. Leaves nothing outstanding. */
  const openWithMoreHistory = (nextBefore = 'm1') => {
    service.openConversation('c1').subscribe();
    httpMock
      .expectOne('/api/v1/chat/conversations/c1/messages')
      .flush({ items: [message({ id: 'm2', body: 'second' }), message({ id: 'm1', body: 'first' })], nextBefore });
    httpMock.expectOne('/api/v1/chat/conversations/c1/read').flush(null);
    httpMock.match('/api/v1/chat/conversations/unread-count').forEach((r) => r.flush({ unreadCount: 0 }));
  };

  it('pages history one request at a time however often the scroller asks (GH #220)', () => {
    TestBed.tick();
    flushInitialUnread();
    openWithMoreHistory();

    // A fling to the top fires many scroll events before the first response lands.
    service.loadOlder('c1').subscribe();
    service.loadOlder('c1').subscribe();
    service.loadOlder('c1').subscribe();

    // One request, not three — three would carry the identical `before` cursor and each prepend the
    // same page again.
    const req = httpMock.expectOne('/api/v1/chat/conversations/c1/messages?before=m1');
    expect(service.loadingOlder()).toBe(true);
    req.flush({ items: [message({ id: 'm0', body: 'older' })], nextBefore: null });

    expect(service.messages().map((m) => m.id)).toEqual(['m0', 'm1', 'm2']);
    expect(service.loadingOlder()).toBe(false);
    expect(service.hasMoreHistory()).toBe(false);

    // And the guard releases: the next scroll to the top may page again.
    service.loadOlder('c1').subscribe();
    httpMock.expectOne('/api/v1/chat/conversations/c1/messages').flush({ items: [], nextBefore: null });
  });

  it('never prepends a message id the thread already holds', () => {
    TestBed.tick();
    flushInitialUnread();
    openWithMoreHistory();

    // A page that overlaps what is on screen — e.g. a message that also arrived live.
    service.loadOlder('c1').subscribe();
    httpMock
      .expectOne('/api/v1/chat/conversations/c1/messages?before=m1')
      .flush({ items: [message({ id: 'm1', body: 'first' }), message({ id: 'm0', body: 'older' })], nextBefore: null });

    // Duplicate ids would give the `track m.id` loop repeated keys (NG0955) and render twice.
    expect(service.messages().map((m) => m.id)).toEqual(['m0', 'm1', 'm2']);
  });

  it('discards a history page for a conversation that is no longer open', () => {
    TestBed.tick();
    flushInitialUnread();
    openWithMoreHistory();

    service.loadOlder('c1').subscribe();
    const stale = httpMock.expectOne('/api/v1/chat/conversations/c1/messages?before=m1');

    // The reader switches threads while the page is still in flight.
    service.openConversation('c2').subscribe();
    httpMock
      .expectOne('/api/v1/chat/conversations/c2/messages')
      .flush({ items: [message({ id: 'n1', body: 'other thread' })], nextBefore: 'n1' });
    httpMock.expectOne('/api/v1/chat/conversations/c2/read').flush(null);
    httpMock.match('/api/v1/chat/conversations/unread-count').forEach((r) => r.flush({ unreadCount: 0 }));

    stale.flush({ items: [message({ id: 'm0', body: 'older' })], nextBefore: null });

    // c1's history stays out of c2's thread, and the guard is free for the new conversation.
    expect(service.messages().map((m) => m.id)).toEqual(['n1']);
    expect(service.loadingOlder()).toBe(false);
    expect(service.hasMoreHistory()).toBe(true);
  });

  it('appends a sent message to the open thread', () => {
    TestBed.tick();
    flushInitialUnread();

    // Seed the inbox so the send can also refresh the row (matches real use: the conversation is listed).
    service.loadInbox().subscribe();
    httpMock
      .expectOne('/api/v1/chat/conversations?skip=0&take=20')
      .flush({ items: [conversation({ id: 'c1' })], totalCount: 1, skip: 0, take: 20 });

    service.openConversation('c1').subscribe();
    httpMock.expectOne('/api/v1/chat/conversations/c1/messages').flush({ items: [], nextBefore: null });

    service.send('c1', 'hey').subscribe();
    httpMock
      .expectOne((r) => r.url === '/api/v1/chat/conversations/c1/messages' && r.method === 'POST')
      .flush(message({ id: 'm9', body: 'hey', isOwn: true }));

    expect(service.messages().map((m) => m.body)).toEqual(['hey']);
  });

  it('refreshes the inbox row preview when the sender sends (server only pushes to others)', () => {
    TestBed.tick();
    flushInitialUnread();

    // A freshly-created group with no messages yet — the left rail shows "no messages" (lastMessage null).
    service.loadInbox().subscribe();
    httpMock.expectOne('/api/v1/chat/conversations?skip=0&take=20').flush({
      items: [conversation({ id: 'g1', kind: 'Group', name: 'Weekend crew', lastMessage: null })],
      totalCount: 1,
      skip: 0,
      take: 20,
    });
    expect(service.conversations()[0].lastMessage).toBeNull();

    // Send the first message without the conversation open (the rail must still update).
    service.send('g1', 'first!').subscribe();
    httpMock
      .expectOne((r) => r.url === '/api/v1/chat/conversations/g1/messages' && r.method === 'POST')
      .flush(message({ id: 'm9', body: 'first!', isOwn: true }));

    const row = service.conversations()[0];
    expect(row.id).toBe('g1');
    expect(row.lastMessage?.preview).toBe('first!');
    expect(row.lastMessage?.isOwn).toBe(true);
  });

  it('tombstones a deleted message rather than removing it', () => {
    TestBed.tick();
    flushInitialUnread();

    service.openConversation('c1').subscribe();
    httpMock
      .expectOne('/api/v1/chat/conversations/c1/messages')
      .flush({ items: [message({ id: 'm1', body: 'oops', isOwn: true })], nextBefore: null });
    httpMock.expectOne('/api/v1/chat/conversations/c1/read').flush(null);
    httpMock.match('/api/v1/chat/conversations/unread-count').forEach((r) => r.flush({ unreadCount: 0 }));

    service.deleteMessage('m1').subscribe();
    httpMock.expectOne('/api/v1/chat/messages/m1').flush(null);

    // The message keeps its place in the order; only its content goes.
    expect(service.messages().length).toBe(1);
    expect(service.messages()[0].isDeleted).toBe(true);
    expect(service.messages()[0].body).toBe('');
  });

  it('debounces the typing signal', () => {
    TestBed.tick();
    flushInitialUnread();

    service.signalTyping('c1');
    service.signalTyping('c1');
    service.signalTyping('c1');

    // Three keystrokes, one request — a request per keypress would be abusive.
    const reqs = httpMock.match('/api/v1/chat/conversations/c1/typing');
    expect(reqs.length).toBe(1);
    reqs.forEach((r) => r.flush(null));
  });

  it('drops all state on sign-out so an anonymous client holds nothing', () => {
    TestBed.tick();
    flushInitialUnread(5);
    expect(service.unreadCount()).toBe(5);

    authed.set(false);
    TestBed.tick();

    expect(service.unreadCount()).toBe(0);
    expect(service.conversations()).toEqual([]);
    expect(service.messages()).toEqual([]);
  });

  it('hides a conversation locally when it is hidden on the server', () => {
    TestBed.tick();
    flushInitialUnread();

    service.loadInbox().subscribe();
    httpMock
      .expectOne('/api/v1/chat/conversations?skip=0&take=20')
      .flush({ items: [conversation({ id: 'c1' })], totalCount: 1, skip: 0, take: 20 });

    service.setState('c1', { isHidden: true }).subscribe();
    httpMock.expectOne('/api/v1/chat/conversations/c1/state').flush(null);

    expect(service.conversations()).toEqual([]);
  });

  it('keeps a muted conversation listed', () => {
    TestBed.tick();
    flushInitialUnread();

    service.loadInbox().subscribe();
    httpMock
      .expectOne('/api/v1/chat/conversations?skip=0&take=20')
      .flush({ items: [conversation({ id: 'c1' })], totalCount: 1, skip: 0, take: 20 });

    service.setState('c1', { isMuted: true }).subscribe();
    httpMock.expectOne('/api/v1/chat/conversations/c1/state').flush(null);
    httpMock.match('/api/v1/chat/conversations/unread-count').forEach((r) => r.flush({ unreadCount: 0 }));

    // Muting silences the badge; it does not remove the row (FR-028).
    expect(service.conversations().length).toBe(1);
    expect(service.conversations()[0].isMuted).toBe(true);
  });

  it('searches the inbox by name without touching the live conversation list (feature 046)', () => {
    TestBed.tick();
    flushInitialUnread();

    service.loadInbox().subscribe();
    httpMock
      .expectOne('/api/v1/chat/conversations?skip=0&take=20')
      .flush({ items: [conversation()], totalCount: 1, skip: 0, take: 20 });

    let page: Conversation[] = [];
    service.searchInbox('len').subscribe((r) => (page = [...r.items]));

    const req = httpMock.expectOne('/api/v1/chat/conversations?q=len&skip=0&take=20');
    expect(req.request.method).toBe('GET');
    req.flush({ items: [conversation({ id: 'c9', name: 'Lena B.' })], totalCount: 1, skip: 0, take: 20 });

    expect(page.map((c) => c.id)).toEqual(['c9']);
    // The list SignalR keeps current is untouched, so clearing the term restores it instantly.
    expect(service.conversations().map((c) => c.id)).toEqual(['c1']);
  });

  it('searches people to start a chat with (people only since feature 046)', () => {
    TestBed.tick();
    flushInitialUnread();

    service.search('kofi').subscribe((r) => {
      expect(r.people.items.length).toBe(1);
      expect('messages' in r).toBe(false);
    });

    httpMock.expectOne('/api/v1/chat/search?q=kofi&skip=0&take=20').flush({
      people: { items: [{ userId: 'u9' }], totalCount: 1 },
    });
  });
});
