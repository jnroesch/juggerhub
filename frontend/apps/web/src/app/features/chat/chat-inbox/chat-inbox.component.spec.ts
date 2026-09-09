import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { Subject, of } from 'rxjs';
import { ChatService } from '../../../core/services/chat.service';
import { Conversation, ConversationKind } from '../../../core/models/chat.models';
import { ChatInboxComponent } from './chat-inbox.component';
import { translocoTestingModule } from '../../../../testing/transloco-testing';

/** A minimal ChatService double — the inbox reads two signals and calls loadInbox() on init. */
const chat = {
  conversations: signal<Conversation[]>([]),
  typing: signal<{ conversationId: string }[]>([]),
  loadInbox: jest.fn().mockReturnValue(of({ items: [], totalCount: 0, skip: 0, take: 20 })),
  searchInbox: jest.fn(),
};

function create() {
  TestBed.configureTestingModule({
    imports: [translocoTestingModule()],
    providers: [{ provide: ChatService, useValue: chat }, provideRouter([])],
  });
  const fixture = TestBed.createComponent(ChatInboxComponent);
  fixture.detectChanges();
  return fixture;
}

/** Reach the protected tagFor for assertion. */
function tagFor(fixture: ReturnType<typeof create>, kind: ConversationKind): string | null {
  const cmp = fixture.componentInstance as unknown as { tagFor: (c: Conversation) => string | null };
  return cmp.tagFor({ kind } as Conversation);
}

describe('ChatInboxComponent tagFor (feature 027)', () => {
  beforeEach(() => jest.clearAllMocks());

  it('tags both inquiry kinds as ADMINS', () => {
    const fixture = create();
    expect(tagFor(fixture, 'TeamInquiry')).toBe('Admins');
    expect(tagFor(fixture, 'EventInquiry')).toBe('Admins');
  });

  it('keeps the existing TEAM/PARTY tags and none for DMs/groups', () => {
    const fixture = create();
    expect(tagFor(fixture, 'Team')).toBe('Team');
    expect(tagFor(fixture, 'Party')).toBe('Party');
    expect(tagFor(fixture, 'Direct')).toBeNull();
    expect(tagFor(fixture, 'Group')).toBeNull();
  });
});

function conversation(kind: ConversationKind, url: string | null): Conversation {
  return {
    id: 'c-1',
    kind,
    name: 'Bob',
    avatar: { kind: kind === 'Direct' ? 'User' : 'Team', userId: null, teamId: null, url },
    lastMessage: null,
    unreadCount: 0,
    isMuted: false,
    state: 'Active',
    teamId: null,
    partyId: null,
  };
}

describe('ChatInboxComponent avatars (issue #193)', () => {
  beforeEach(() => jest.clearAllMocks());

  it("renders a DM partner's avatar when the row carries a URL", () => {
    chat.conversations.set([conversation('Direct', '/api/v1/profiles/bob/avatar')]);
    const fixture = create();

    const img = fixture.nativeElement.querySelector('[data-testid="conversation-c-1"] img') as HTMLImageElement;
    expect(img).not.toBeNull();
    expect(img.getAttribute('src')).toBe('/api/v1/profiles/bob/avatar');
  });

  it('falls back to the round placeholder for a DM with no avatar', () => {
    chat.conversations.set([conversation('Direct', null)]);
    const fixture = create();

    const row = fixture.nativeElement.querySelector('[data-testid="conversation-c-1"]') as HTMLElement;
    expect(row.querySelector('img')).toBeNull();
    expect(row.querySelector('.rounded-pill')).not.toBeNull();
  });
});

/**
 * Inbox search (feature 046): the field narrows the inbox to conversations whose members' or own
 * names match, rendered as the ordinary rows; message text is never searched and no "In your
 * messages" / "People" sections exist any more.
 */
describe('ChatInboxComponent search (feature 046)', () => {
  const page = (items: Conversation[]) => ({ items, totalCount: items.length, skip: 0, take: 20 });

  function type(fixture: ReturnType<typeof create>, value: string): void {
    const input = fixture.nativeElement.querySelector('[data-testid="chat-search"]') as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers();
    chat.conversations.set([conversation('Direct', null)]);
  });

  afterEach(() => jest.useRealTimers());

  it('narrows the list to the returned conversations after the debounce', () => {
    chat.searchInbox.mockReturnValue(of(page([{ ...conversation('Group', null), id: 'c-7', name: 'Tournament trip' }])));
    const fixture = create();

    type(fixture, 'tr');
    expect(chat.searchInbox).not.toHaveBeenCalled();

    jest.advanceTimersByTime(250);
    fixture.detectChanges();

    expect(chat.searchInbox).toHaveBeenCalledWith('tr');
    expect(fixture.nativeElement.querySelector('[data-testid="conversation-c-7"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="conversation-c-1"]')).toBeNull();
  });

  it('shows a plain empty state naming the term when nothing matches', () => {
    chat.searchInbox.mockReturnValue(of(page([])));
    const fixture = create();

    type(fixture, 'zz');
    jest.advanceTimersByTime(250);
    fixture.detectChanges();

    const empty = fixture.nativeElement.querySelector('[data-testid="search-empty"]') as HTMLElement;
    expect(empty).not.toBeNull();
    expect(empty.textContent).toContain('zz');
    expect(fixture.nativeElement.querySelector('[data-testid="chat-empty"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeNull();
  });

  it('restores the inbox when the term is cleared', () => {
    chat.searchInbox.mockReturnValue(of(page([])));
    const fixture = create();

    type(fixture, 'zz');
    jest.advanceTimersByTime(250);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="conversation-c-1"]')).toBeNull();

    type(fixture, '');

    expect(fixture.nativeElement.querySelector('[data-testid="conversation-c-1"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="search-empty"]')).toBeNull();
  });

  it('does not search on a single character', () => {
    const fixture = create();

    type(fixture, 'l');
    jest.advanceTimersByTime(250);
    fixture.detectChanges();

    expect(chat.searchInbox).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('[data-testid="conversation-c-1"]')).not.toBeNull();
  });

  it('announces the search while it is in flight', () => {
    chat.searchInbox.mockReturnValue(new Subject());
    const fixture = create();

    type(fixture, 'le');
    jest.advanceTimersByTime(250);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role="status"]')).not.toBeNull();
  });

  it('renders no message or people sections', () => {
    chat.searchInbox.mockReturnValue(of(page([{ ...conversation('Direct', null), id: 'c-2', name: 'Lena B.' }])));
    const fixture = create();

    type(fixture, 'le');
    jest.advanceTimersByTime(250);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('In your messages');
    expect(text).not.toContain('People');
    expect(fixture.nativeElement.querySelector('[data-testid="conversation-c-2"]')).not.toBeNull();
  });
});
