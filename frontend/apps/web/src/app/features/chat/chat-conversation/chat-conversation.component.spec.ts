import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { ChatConversationComponent } from './chat-conversation.component';
import { ChatService } from '../../../core/services/chat.service';
import { ChatMessage, ConversationDetail } from '../../../core/models/chat.models';
import { translocoTestingModule } from '../../../../testing/transloco-testing';

/**
 * A message the server could not decrypt renders as a neutral placeholder, and only that one
 * message does (feature 047 FR-009).
 *
 * The assertion that matters is the pair: the unavailable bubble must NOT read as deleted — nobody
 * withdrew it — and the readable messages either side of it must be untouched. A blank bubble would
 * pass a "does not show the body" check while telling the reader nothing at all.
 */
describe('ChatConversationComponent — an undecryptable message', () => {
  let fixture: ComponentFixture<ChatConversationComponent>;

  const message = (over: Partial<ChatMessage> = {}): ChatMessage => ({
    id: 'm1',
    kind: 'Member',
    senderId: 'u2',
    senderName: 'Ben R.',
    isOwn: false,
    body: 'hello',
    sentAt: '2026-09-09T19:38:00Z',
    isDeleted: false,
    isUnavailable: false,
    readState: null,
    systemEvent: null,
    systemSubjectName: null,
    linkCard: null,
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

  const messages = signal<readonly ChatMessage[]>([
    message({ id: 'm1', body: 'first message' }),
    message({ id: 'm2', body: '', isUnavailable: true }),
    message({ id: 'm3', body: 'third message' }),
  ]);

  beforeEach(async () => {
    const chat = {
      messages,
      hasMoreHistory: signal(false),
      loadingOlder: signal(false),
      typingHere: signal<readonly string[]>([]),
      getDetail: () => of(detail),
      openConversation: () => of(undefined),
      markReadToLatest: () => undefined,
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
  });

  it('renders the placeholder for the message that could not be decrypted', () => {
    const placeholder = fixture.nativeElement.querySelector('[data-testid="unavailable-message"]');

    expect(placeholder).toBeTruthy();
    expect((placeholder.textContent as string).trim()).toBe('This message can’t be displayed.');
  });

  it('does not call it deleted — nobody withdrew it', () => {
    const text = fixture.nativeElement.textContent as string;

    expect(text).not.toContain('Message deleted');
  });

  it('leaves every other message readable', () => {
    const text = fixture.nativeElement.textContent as string;

    expect(text).toContain('first message');
    expect(text).toContain('third message');
    expect(fixture.nativeElement.querySelectorAll('[data-testid="unavailable-message"]').length).toBe(1);
  });
});
