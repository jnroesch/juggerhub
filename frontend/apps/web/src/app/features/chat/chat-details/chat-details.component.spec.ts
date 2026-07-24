import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ChatService } from '../../../core/services/chat.service';
import { ChatMember, ConversationDetail } from '../../../core/models/chat.models';
import { ChatDetailsComponent } from './chat-details.component';

function member(userId: string, handle: string | null, extra: Partial<ChatMember> = {}): ChatMember {
  return {
    userId,
    displayName: userId.toUpperCase(),
    handle,
    avatarUrl: null,
    isYou: false,
    viaMarket: false,
    ...extra,
  };
}

const detail: ConversationDetail = {
  id: 'c-1',
  kind: 'Group',
  name: 'Rheinfeuer',
  avatar: { kind: 'Group', userId: null, teamId: null, url: null },
  state: 'Active',
  isMuted: false,
  isHidden: false,
  memberCount: 2,
  canLeave: true,
  canAddMembers: false,
  teamId: null,
  partyId: null,
};

const chat = {
  getDetail: jest.fn(),
  getMembers: jest.fn(),
  setState: jest.fn(),
  leave: jest.fn(),
  block: jest.fn(),
  loadInbox: jest.fn(),
};

function create(members: ChatMember[]) {
  chat.getDetail.mockReturnValue(of(detail));
  chat.getMembers.mockReturnValue(of({ items: members, totalCount: members.length, skip: 0, take: 20 }));

  TestBed.configureTestingModule({
    providers: [{ provide: ChatService, useValue: chat }, provideRouter([])],
  });
  const fixture = TestBed.createComponent(ChatDetailsComponent);
  fixture.componentRef.setInput('conversationId', 'c-1');
  fixture.detectChanges();
  return fixture;
}

function rows(fixture: ReturnType<typeof create>): HTMLElement[] {
  return Array.from(fixture.nativeElement.querySelectorAll('[data-testid="member-row"]'));
}

describe('ChatDetailsComponent (issue #68)', () => {
  beforeEach(() => jest.clearAllMocks());

  it('offers a back-to-chat control at every width, not just mobile', () => {
    const fixture = create([]);
    const back = fixture.nativeElement.querySelector('[data-testid="back-to-chat"]') as HTMLElement;

    expect(back).not.toBeNull();
    expect(back.getAttribute('href')).toBe('/chat/c-1');
    // The conversation's own back button is lg:hidden because the rail is already beside it;
    // details replaces the conversation, so hiding this one would strand desktop readers.
    expect(back.className).not.toContain('lg:hidden');
  });

  it('links each member with a handle to their profile', () => {
    const fixture = create([member('u-bob', 'bob'), member('u-ann', 'ann')]);
    const [bob, ann] = rows(fixture);

    expect(bob.tagName).toBe('A');
    expect(bob.getAttribute('href')).toBe('/u/bob');
    expect(ann.getAttribute('href')).toBe('/u/ann');
  });

  it('renders a member without a handle as a plain, non-linked row', () => {
    const fixture = create([member('u-guest', null, { viaMarket: true })]);
    const [guest] = rows(fixture);

    expect(guest.tagName).not.toBe('A');
    expect(guest.getAttribute('href')).toBeNull();
  });

  it('keeps the you / via-market annotations when the row becomes a link', () => {
    const fixture = create([member('u-me', 'me', { isYou: true, viaMarket: true })]);
    const [me] = rows(fixture);

    expect(me.tagName).toBe('A');
    expect(me.textContent).toContain('· you');
    expect(me.textContent).toContain('guest · via market');
  });
});
