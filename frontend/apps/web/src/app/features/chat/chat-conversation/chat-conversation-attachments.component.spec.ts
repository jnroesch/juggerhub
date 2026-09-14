import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { ChatConversationComponent } from './chat-conversation.component';
import { ChatService } from '../../../core/services/chat.service';
import { ChatAttachment, ChatMessage, ConversationDetail } from '../../../core/models/chat.models';
import { translocoTestingModule } from '../../../../testing/transloco-testing';

/**
 * Files in a conversation (feature 049 / #282): the `+` control, the tray of picked files, and how
 * attachments render in the thread.
 */
describe('ChatConversationComponent — attachments', () => {
  let fixture: ComponentFixture<ChatConversationComponent>;
  let sent: { body: string; files: readonly File[] } | null;

  const attachment = (over: Partial<ChatAttachment> = {}): ChatAttachment => ({
    id: 'a1',
    fileName: 'schedule.pdf',
    contentType: 'application/pdf',
    sizeBytes: 2048,
    width: null,
    height: null,
    ...over,
  });

  const message = (over: Partial<ChatMessage> = {}): ChatMessage => ({
    id: 'm1',
    kind: 'Member',
    senderId: 'u2',
    senderName: 'Ben R.',
    isOwn: false,
    body: 'hello',
    sentAt: '2026-09-13T19:38:00Z',
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

  const file = (name: string, size = 1024): File =>
    new File([new Uint8Array(size)], name, { type: 'application/pdf' });

  beforeEach(async () => {
    sent = null;
    messages.set([]);

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
      send: (_id: string, body: string, files: readonly File[] = []) => {
        sent = { body, files };
        return of(undefined);
      },
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

  const el = (testid: string): HTMLElement | null =>
    fixture.nativeElement.querySelector(`[data-testid="${testid}"]`);

  const pick = (...files: File[]): void => {
    const input = el('attachment-input') as HTMLInputElement;
    Object.defineProperty(input, 'files', { value: files, configurable: true });
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  };

  // --- The + control ---------------------------------------------------------

  it('offers an attach control that opens the hidden file picker', () => {
    const attach = el('attach') as HTMLButtonElement;
    const input = el('attachment-input') as HTMLInputElement;

    expect(attach).toBeTruthy();
    expect(input.type).toBe('file');
    expect(input.multiple).toBe(true);

    const clicked = jest.spyOn(input, 'click');
    attach.click();

    expect(clicked).toHaveBeenCalled();
  });

  /**
   * CHK002: the send button is the one coral CTA in this view. A second brand-coloured control
   * beside it would compete with the action people actually came to take.
   */
  it('does not make the attach control a second brand CTA', () => {
    expect(el('attach')?.className).not.toContain('bg-brand');
    expect(el('send')?.className).toContain('bg-brand');
  });

  // --- The tray --------------------------------------------------------------

  it('lists picked files and removes them one at a time', () => {
    pick(file('one.pdf'), file('two.pdf'));

    expect(el('pending-files')?.textContent).toContain('one.pdf');
    expect(el('pending-files')?.textContent).toContain('two.pdf');

    (fixture.nativeElement.querySelectorAll('[data-testid="remove-file"]')[0] as HTMLButtonElement).click();
    fixture.detectChanges();

    // Removing one leaves the other — a single wrong pick must not cost the whole selection.
    expect(el('pending-files')?.textContent).not.toContain('one.pdf');
    expect(el('pending-files')?.textContent).toContain('two.pdf');
  });

  it('refuses a file over the size limit without dropping the good ones', () => {
    pick(file('huge.pdf', 11 * 1024 * 1024), file('fine.pdf'));

    expect(el('pending-files')?.textContent).toContain('fine.pdf');
    expect(el('pending-files')?.textContent).not.toContain('huge.pdf');
  });

  it('refuses more than ten files', () => {
    pick(...Array.from({ length: 11 }, (_, i) => file(`f${i}.pdf`)));

    expect(fixture.nativeElement.querySelectorAll('[data-testid="remove-file"]').length).toBe(10);
  });

  // --- Sending ---------------------------------------------------------------

  /** FR-004: a photo is a perfectly good thing to say on its own. */
  it('can send files with no text at all', () => {
    pick(file('rules.pdf'));

    const send = el('send') as HTMLButtonElement;
    expect(send.disabled).toBe(false);

    send.click();
    fixture.detectChanges();

    expect(sent?.body).toBe('');
    expect(sent?.files.length).toBe(1);
  });

  it('clears the tray once the send succeeds', () => {
    pick(file('rules.pdf'));
    (el('send') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(el('pending-files')).toBeNull();
  });

  it('still refuses to send with neither text nor files', () => {
    expect((el('send') as HTMLButtonElement).disabled).toBe(true);
  });

  // --- Rendering in the thread ----------------------------------------------

  it('renders an image attachment as a picture, not a file name', () => {
    messages.set([
      message({
        body: '',
        attachments: [
          attachment({ id: 'a9', fileName: 'pitch.jpg', contentType: 'image/webp', width: 800, height: 600 }),
        ],
      }),
    ]);
    fixture.detectChanges();

    const img = fixture.nativeElement.querySelector('[data-testid="attachments"] img') as HTMLImageElement;

    expect(img).toBeTruthy();
    expect(img.getAttribute('src')).toBe('/api/v1/chat/attachments/a9');
    // Dimensions are carried so the thread reserves space rather than jumping as the image loads.
    expect(img.getAttribute('width')).toBe('800');
    expect(img.getAttribute('height')).toBe('600');
    // An accessible name, so a screen reader announces something other than "image" (FR-044).
    expect(img.getAttribute('alt')).toBe('pitch.jpg');
  });

  it('renders a document as a named row with its size', () => {
    messages.set([message({ body: '', attachments: [attachment()] })]);
    fixture.detectChanges();

    const row = el('attachment-file');

    expect(row?.textContent).toContain('schedule.pdf');
    expect(row?.textContent).toContain('2 KB');
    expect(row?.getAttribute('href')).toBe('/api/v1/chat/attachments/a1');
    expect(fixture.nativeElement.querySelector('[data-testid="attachments"] img')).toBeNull();
  });

  /**
   * The bug this guards: an attachment-only message has an empty body, so a template that renders
   * on `body` alone shows a blank bubble with the file invisible beneath it.
   */
  it('renders an attachment-only message rather than an empty bubble', () => {
    messages.set([message({ body: '', attachments: [attachment()] })]);
    fixture.detectChanges();

    expect(el('attachments')).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('schedule.pdf');
  });

  it('shows both the text and the files when a message has both', () => {
    messages.set([message({ body: 'schedule attached', attachments: [attachment()] })]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('schedule attached');
    expect(fixture.nativeElement.textContent).toContain('schedule.pdf');
  });

  /** FR-030: a withdrawn message leaves no file names behind — those are content too. */
  it('shows no attachments on a withdrawn message', () => {
    messages.set([message({ body: '', isDeleted: true, attachments: [] })]);
    fixture.detectChanges();

    expect(el('attachments')).toBeNull();
  });
});
