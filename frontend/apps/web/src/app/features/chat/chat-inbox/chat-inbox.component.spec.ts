import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { ChatService } from '../../../core/services/chat.service';
import { Conversation, ConversationKind } from '../../../core/models/chat.models';
import { ChatInboxComponent } from './chat-inbox.component';
import { translocoTestingModule } from '../../../../testing/transloco-testing';

/** A minimal ChatService double — the inbox reads two signals and calls loadInbox() on init. */
const chat = {
  conversations: signal<Conversation[]>([]),
  typing: signal<{ conversationId: string }[]>([]),
  loadInbox: jest.fn().mockReturnValue(of({ items: [], totalCount: 0, skip: 0, take: 20 })),
  search: jest.fn(),
  start: jest.fn(),
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
