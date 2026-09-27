import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { ChatConversationComponent } from './chat-conversation.component';
import { ChatService } from '../../../core/services/chat.service';
import { ChatMessage, ConversationDetail } from '../../../core/models/chat.models';
import { translocoTestingModule } from '../../../../testing/transloco-testing';

/**
 * Only a message from someone else that lands while the thread is open `rise`s (DESIGN.md
 * "Motion vocabulary"). The thread opening, a page of older history and the reader's own send
 * all stay still — a thread that animated its whole history on open would be the noise the
 * vocabulary exists to rule out.
 */
describe('ChatConversationComponent — which messages rise', () => {
  let fixture: ComponentFixture<ChatConversationComponent>;

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
    messages.set([message({ id: 'm1', body: 'first' })]);

    const chat = {
      messages,
      hasMoreHistory: signal(false),
      loadingOlder: signal(false),
      typingHere: signal<readonly string[]>([]),
      getDetail: () => of(detail),
      openConversation: () => of(undefined),
      markReadToLatest: () => undefined,
      flushDeferredRead: () => undefined,
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
  });

  const update = async (next: (all: readonly ChatMessage[]) => readonly ChatMessage[]) => {
    messages.update(next);
    fixture.detectChanges();
    await fixture.whenStable();
  };

  /** The rendered row holding a body — the `<li>` is what carries the class. */
  const row = (body: string): HTMLElement => {
    const rows = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>('[data-testid="message-list"] > li'),
    );
    const found = rows.find((li) => li.textContent?.includes(body));
    if (!found) throw new Error(`no row for "${body}"`);
    return found;
  };

  it('leaves the messages the thread opened with still', () => {
    expect(row('first').classList).not.toContain('jh-rise');
  });

  it('raises a message from someone else that lands while the thread is open', async () => {
    await update((all) => [...all, message({ id: 'm2', body: 'second' })]);

    expect(row('second').classList).toContain('jh-rise');
    expect(row('first').classList).not.toContain('jh-rise');
  });

  it('does not raise the reader’s own send — it was already on screen', async () => {
    await update((all) => [...all, message({ id: 'm2', body: 'mine', isOwn: true, senderId: 'me' })]);

    expect(row('mine').classList).not.toContain('jh-rise');
  });

  it('does not raise a page of older history', async () => {
    await update((all) => [message({ id: 'm0', body: 'older' }), ...all]);

    expect(row('older').classList).not.toContain('jh-rise');
  });
});
