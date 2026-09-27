import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { Translation, TranslocoService } from '@jsverse/transloco';
import { ChatConversationComponent } from './chat-conversation.component';
import { ChatService } from '../../../core/services/chat.service';
import { ChatMessage, ConversationDetail, ReadState } from '../../../core/models/chat.models';
import { translocoTestingModule } from '../../../../testing/transloco-testing';

const en: Translation = require('../../../../../public/i18n/en.json');
const de: Translation = require('../../../../../public/i18n/de.json');

/**
 * The read receipt arrives as a code ("Sent"/"Read") and used to be printed raw — English under
 * every own bubble in the German interface.
 */
describe('ChatConversationComponent — read receipt', () => {
  const ownMessage = (readState: ReadState): ChatMessage => ({
    id: 'm1',
    kind: 'Member',
    senderId: 'me',
    senderName: 'Me',
    isOwn: true,
    body: 'hallo',
    sentAt: '2026-09-26T19:38:00Z',
    isDeleted: false,
    isUnavailable: false,
    readState,
    systemEvent: null,
    systemSubjectName: null,
    linkCard: null,
    attachments: [],
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

  const render = async (readState: ReadState): Promise<string> => {
    const chat = {
      messages: signal<readonly ChatMessage[]>([ownMessage(readState)]),
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
      imports: [ChatConversationComponent, translocoTestingModule({ en, de })],
      providers: [provideRouter([]), { provide: ChatService, useValue: chat }],
    }).compileComponents();
    TestBed.inject(TranslocoService).setActiveLang('de');

    const fixture = TestBed.createComponent(ChatConversationComponent);
    fixture.componentRef.setInput('conversationId', 'c1');
    fixture.detectChanges();
    await fixture.whenStable();
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  };

  it('shows a sent receipt in the reader’s language', async () => {
    const text = await render('Sent');
    expect(text).toContain('Gesendet');
    expect(text).not.toContain('Sent');
  });

  it('shows a read receipt in the reader’s language', async () => {
    const text = await render('Read');
    expect(text).toContain('Gelesen');
    expect(text).not.toMatch(/\bRead\b/);
  });
});
