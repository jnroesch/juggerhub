import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { ChatConversationComponent } from './chat-conversation.component';
import { ChatService } from '../../../core/services/chat.service';
import { ChatMessage, ConversationDetail } from '../../../core/models/chat.models';
import { translocoTestingModule } from '../../../../testing/transloco-testing';

/**
 * A message the reader sees as it lands is read as it lands (GH #344).
 *
 * The case that mattered is the one jsdom gives for free: a thread with no layout never scrolls, so
 * the `scroll` event the old path relied on never fires — exactly a short DM in a real browser.
 * The divider/jump-pill path for a reader who has scrolled up (019 FR-021) must not change.
 */
describe('ChatConversationComponent — reading on arrival', () => {
  let fixture: ComponentFixture<ChatConversationComponent>;
  let markReadToLatest: jest.Mock;
  let flushDeferredRead: jest.Mock;

  const message = (over: Partial<ChatMessage> = {}): ChatMessage => ({
    id: 'm1',
    kind: 'Member',
    senderId: 'u2',
    senderName: 'Ben R.',
    isOwn: false,
    body: 'hello',
    sentAt: '2026-09-26T19:38:00Z',
    isDeleted: false,
    isUnavailable: false,
    readState: null,
    systemEvent: null,
    systemSubjectName: null,
    linkCard: null,
    attachments: [],
    ...over,
  });

  const detail: ConversationDetail = {
    id: 'c1',
    kind: 'Direct',
    name: 'Ben R.',
    avatar: { kind: 'Initials', userId: 'u2', teamId: null, url: null },
    state: 'Active',
    isMuted: false,
    isHidden: false,
    memberCount: 2,
    canLeave: false,
    canAddMembers: false,
    teamId: null,
    partyId: null,
  };

  const messages = signal<readonly ChatMessage[]>([]);

  beforeEach(async () => {
    messages.set([message({ id: 'm1' })]);
    markReadToLatest = jest.fn();
    flushDeferredRead = jest.fn();

    const chat = {
      messages,
      hasMoreHistory: signal(false),
      loadingOlder: signal(false),
      typingHere: signal<readonly string[]>([]),
      getDetail: () => of(detail),
      openConversation: () => of(undefined),
      markReadToLatest,
      flushDeferredRead,
      loadOlder: () => of(undefined),
      signalTyping: () => undefined,
      send: () => of(undefined),
      deleteMessage: () => of(undefined),
    };

    await TestBed.configureTestingModule({
      imports: [ChatConversationComponent, translocoTestingModule()],
      providers: [provideRouter([]), { provide: ChatService, useValue: chat }],
    }).compileComponents();

    fixture = TestBed.createComponent(ChatConversationComponent);
    fixture.componentRef.setInput('conversationId', 'c1');
    fixture.detectChanges();
    await fixture.whenStable();
    markReadToLatest.mockClear();
  });

  const arrive = async (m: ChatMessage) => {
    messages.update((all) => [...all, m]);
    fixture.detectChanges();
    await fixture.whenStable();
  };

  /** Put the reader somewhere in a thread that has real height, and let the scroll handler see it. */
  const scrollTo = (scrollTop: number) => {
    const el = (fixture.componentInstance as unknown as { scroller: { nativeElement: HTMLElement } }).scroller
      .nativeElement;
    Object.defineProperty(el, 'scrollHeight', { configurable: true, value: 2000 });
    Object.defineProperty(el, 'clientHeight', { configurable: true, value: 500 });
    el.scrollTop = scrollTop;
    el.dispatchEvent(new Event('scroll'));
  };

  it('marks a message read when it lands in a thread too short to scroll', async () => {
    await arrive(message({ id: 'm2' }));

    expect(markReadToLatest).toHaveBeenCalledWith('c1');
  });

  it('does not mark read for the reader’s own send — the server never counts it', async () => {
    await arrive(message({ id: 'm2', isOwn: true, senderId: 'me' }));

    expect(markReadToLatest).not.toHaveBeenCalled();
  });

  it('leaves a message unread behind the divider while the reader is scrolled up', async () => {
    scrollTo(200);
    markReadToLatest.mockClear();

    await arrive(message({ id: 'm2' }));

    expect(markReadToLatest).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('[data-testid="jump-pill"]')).toBeTruthy();
  });

  it('catches up a held-back read when the tab returns with the reader at the bottom', () => {
    document.dispatchEvent(new Event('visibilitychange'));

    expect(flushDeferredRead).toHaveBeenCalledWith('c1');
  });

  it('does not catch up on return while the reader is scrolled up', () => {
    scrollTo(200);

    document.dispatchEvent(new Event('visibilitychange'));

    expect(flushDeferredRead).not.toHaveBeenCalled();
  });
});
