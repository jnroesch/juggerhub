import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../../../core/services/auth.service';
import { ChatService } from '../../../../core/services/chat.service';
import { ProfileService } from '../../../../core/services/profile.service';
import { TeamService } from '../../../../core/services/team.service';
import { OwnerProfile, ProfileTeam } from '../../../../core/models/profile.models';
import { ChatSearchResult, PersonHit } from '../../../../core/models/chat.models';
import { InvitableUser, PagedResult, UserRelation } from '../../../../core/models/team.models';
import { ProfileQuickActionsComponent } from './profile-quick-actions.component';

// --- factories -------------------------------------------------------------

function owner(handle: string, teams: ProfileTeam[] = []): OwnerProfile {
  return {
    handle,
    displayName: handle,
    hometown: null,
    description: null,
    hasAvatar: false,
    pompfen: [],
    recentActivity: [],
    teams,
    badges: [],
    achievements: [],
  };
}

function team(slug: string, role: 'Admin' | 'Member'): ProfileTeam {
  return { slug, name: slug.toUpperCase(), type: 'CityTeam', city: null, role };
}

function person(userId: string, handle: string, existingConversationId: string | null = null): PersonHit {
  return { userId, displayName: handle, handle, avatarUrl: null, existingConversationId };
}

function chatResult(people: PersonHit[]): ChatSearchResult {
  return { messages: { items: [], totalCount: 0 }, people: { items: people, totalCount: people.length } };
}

function invitable(userId: string, handle: string, relation: UserRelation): InvitableUser {
  return { userId, handle, displayName: handle, city: null, relation };
}

function paged(items: InvitableUser[]): PagedResult<InvitableUser> {
  return { items, totalCount: items.length, skip: 0, take: 20 };
}

// --- mocks -----------------------------------------------------------------

let authenticated: boolean;
let mine: OwnerProfile;

const auth = { isAuthenticated: () => authenticated };
const profiles = { getMineCached: jest.fn(() => of(mine)) };
const chat = { search: jest.fn(), start: jest.fn() };
const teams = { searchUsers: jest.fn(), createTargetedInvite: jest.fn() };
const router = { navigate: jest.fn() };

function create(targetHandle: string) {
  TestBed.configureTestingModule({
    providers: [
      { provide: AuthService, useValue: auth },
      { provide: ProfileService, useValue: profiles },
      { provide: ChatService, useValue: chat },
      { provide: TeamService, useValue: teams },
      { provide: Router, useValue: router },
    ],
  });
  const fixture = TestBed.createComponent(ProfileQuickActionsComponent);
  fixture.componentRef.setInput('handle', targetHandle);
  fixture.detectChanges();
  return fixture;
}

function el(fixture: ReturnType<typeof create>, testid: string): HTMLElement | null {
  return fixture.nativeElement.querySelector(`[data-testid="${testid}"]`);
}

describe('ProfileQuickActionsComponent', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    authenticated = true;
    mine = owner('viewer');
    profiles.getMineCached.mockImplementation(() => of(mine));
    chat.search.mockReturnValue(of(chatResult([])));
    chat.start.mockReturnValue(of({ id: 'newconv' }));
    teams.searchUsers.mockReturnValue(of(paged([])));
    teams.createTargetedInvite.mockReturnValue(of({}));
  });

  // --- Foundational (visibility) -------------------------------------------

  it('renders nothing for an anonymous viewer', () => {
    authenticated = false;
    const fixture = create('alice');
    expect(el(fixture, 'profile-quick-actions')).toBeNull();
    expect(profiles.getMineCached).not.toHaveBeenCalled();
  });

  it('renders nothing on the viewer’s own profile', () => {
    mine = owner('alice');
    const fixture = create('alice');
    expect(el(fixture, 'profile-quick-actions')).toBeNull();
  });

  it('renders the actions for a signed-in, non-self viewer', () => {
    const fixture = create('bob');
    expect(el(fixture, 'profile-quick-actions')).not.toBeNull();
    expect(el(fixture, 'qa-message')).not.toBeNull();
  });

  // --- US1: Message --------------------------------------------------------

  it('opens the existing conversation when one already exists', () => {
    chat.search.mockReturnValue(of(chatResult([person('u-bob', 'bob', 'conv-1')])));
    const fixture = create('bob');
    (el(fixture, 'qa-message') as HTMLButtonElement).click();
    expect(router.navigate).toHaveBeenCalledWith(['/chat', 'conv-1']);
    expect(chat.start).not.toHaveBeenCalled();
  });

  it('routes to a compose draft when no conversation exists yet (feature 022 lazy creation)', () => {
    chat.search.mockReturnValue(of(chatResult([person('u-bob', 'bob', null)])));
    const fixture = create('bob');
    (el(fixture, 'qa-message') as HTMLButtonElement).click();
    expect(router.navigate).toHaveBeenCalledWith(['/chat/compose', 'bob'], {
      state: { userId: 'u-bob', displayName: 'bob' },
    });
    expect(chat.start).not.toHaveBeenCalled();
  });

  it('shows a friendly failure when the player cannot be resolved (e.g. blocked)', () => {
    chat.search.mockReturnValue(of(chatResult([])));
    const fixture = create('bob');
    (el(fixture, 'qa-message') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(el(fixture, 'qa-message-error')).not.toBeNull();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('shows a friendly failure when the search errors', () => {
    chat.search.mockReturnValue(throwError(() => new Error('boom')));
    const fixture = create('bob');
    (el(fixture, 'qa-message') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(el(fixture, 'qa-message-error')).not.toBeNull();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  // --- US2: Invite ---------------------------------------------------------

  it('does not show Invite when the viewer administers no team', () => {
    mine = owner('viewer', [team('rf', 'Member')]);
    const fixture = create('bob');
    expect(el(fixture, 'qa-invite')).toBeNull();
  });

  it('shows Invite disabled with a reason when no administered team is eligible', () => {
    mine = owner('viewer', [team('rf', 'Admin')]);
    teams.searchUsers.mockReturnValue(of(paged([invitable('u-bob', 'bob', 'Member')])));
    const fixture = create('bob');
    const btn = el(fixture, 'qa-invite') as HTMLButtonElement;
    expect(btn).not.toBeNull();
    expect(btn.disabled).toBe(true);
    expect(el(fixture, 'qa-invite-reason')).not.toBeNull();
  });

  it('invites directly when exactly one team is eligible', () => {
    mine = owner('viewer', [team('rf', 'Admin')]);
    teams.searchUsers.mockReturnValue(of(paged([invitable('u-bob', 'bob', 'Invitable')])));
    const fixture = create('bob');
    (el(fixture, 'qa-invite') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(teams.createTargetedInvite).toHaveBeenCalledWith('rf', 'u-bob');
    expect(el(fixture, 'qa-invited')).not.toBeNull();
  });

  it('opens a picker of eligible teams when several are eligible', () => {
    mine = owner('viewer', [team('rf', 'Admin'), team('bl', 'Admin')]);
    teams.searchUsers.mockImplementation((slug: string) =>
      of(paged([invitable('u-bob', 'bob', slug === 'rf' ? 'Invitable' : 'Invitable')])),
    );
    const fixture = create('bob');
    (el(fixture, 'qa-invite') as HTMLButtonElement).click();
    fixture.detectChanges();
    const picker = el(fixture, 'qa-invite-picker');
    expect(picker).not.toBeNull();
    expect(picker?.querySelectorAll('[role="menuitem"]').length).toBe(2);
  });

  it('excludes Member/Invited teams from the picker eligibility', () => {
    mine = owner('viewer', [team('rf', 'Admin'), team('bl', 'Admin')]);
    teams.searchUsers.mockImplementation((slug: string) =>
      of(paged([invitable('u-bob', 'bob', slug === 'rf' ? 'Invitable' : 'Invited')])),
    );
    const fixture = create('bob');
    // Only one eligible → clicking invites directly (no picker).
    (el(fixture, 'qa-invite') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(teams.createTargetedInvite).toHaveBeenCalledWith('rf', 'u-bob');
    expect(teams.createTargetedInvite).toHaveBeenCalledTimes(1);
  });
});
