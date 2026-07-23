import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { ChatService } from '../../../core/services/chat.service';
import { Conversation, ConversationKind } from '../../../core/models/chat.models';
import { ChatInboxComponent } from './chat-inbox.component';

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
