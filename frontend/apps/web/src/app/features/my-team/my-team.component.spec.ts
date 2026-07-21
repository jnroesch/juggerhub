import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { MembershipService } from '../../core/services/membership.service';
import { InvitationService } from '../../core/services/invitation.service';
import { TeamService } from '../../core/services/team.service';
import { MyTeam } from '../../core/models/home.models';
import { MyInvitation, PagedResult } from '../../core/models/team.models';
import { MyTeamComponent } from './my-team.component';

// --- factories -------------------------------------------------------------

function myTeam(slug: string): MyTeam {
  return { slug, name: slug.toUpperCase(), role: 'Member' };
}

function invite(slug: string, token = `tok-${slug}`): MyInvitation {
  return {
    token,
    teamName: slug.toUpperCase(),
    teamSlug: slug,
    teamType: 'CityTeam',
    city: 'Hamburg',
    memberCount: 12,
    inviterDisplayName: 'Lena',
    createdDate: '2026-07-18T09:12:00Z',
    expiresDate: '2026-07-25T09:12:00Z',
  };
}

function paged(items: MyInvitation[]): PagedResult<MyInvitation> {
  return { items, totalCount: items.length, skip: 0, take: 100 };
}

// --- mocks -----------------------------------------------------------------

const teamsSig = signal<MyTeam[]>([]);
const loadedSig = signal(true);

const membership = {
  teams: teamsSig.asReadonly(),
  loaded: loadedSig.asReadonly(),
  load: jest.fn(),
};
const invitations = { listMine: jest.fn() };
const teamApi = { acceptInvite: jest.fn(), declineInvite: jest.fn() };
let navSpy: jest.SpyInstance;

function create() {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: MembershipService, useValue: membership },
      { provide: InvitationService, useValue: invitations },
      { provide: TeamService, useValue: teamApi },
    ],
  });
  const fixture = TestBed.createComponent(MyTeamComponent);
  navSpy = jest.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  fixture.detectChanges();
  fixture.detectChanges(); // settle the teamless invite-load effect
  return fixture;
}

function el(fixture: ReturnType<typeof create>, testid: string): HTMLElement | null {
  return fixture.nativeElement.querySelector(`[data-testid="${testid}"]`);
}

describe('MyTeamComponent', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    teamsSig.set([]);
    loadedSig.set(true);
    invitations.listMine.mockReturnValue(of(paged([])));
    teamApi.acceptInvite.mockReturnValue(of({ teamSlug: 'rot' }));
    teamApi.declineInvite.mockReturnValue(of(undefined));
  });

  // --- US1: teamless home (find + create) ----------------------------------

  it('shows Find and Create with no invites section when teamless with no invites', () => {
    const fixture = create();
    expect(el(fixture, 'my-team-find')).not.toBeNull();
    expect(el(fixture, 'my-team-create')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid^="invite-"]')).toBeNull();
  });

  it('renders the existing chooser (and no invites) for a player on one or more teams', () => {
    teamsSig.set([myTeam('bloodhounds')]);
    invitations.listMine.mockReturnValue(of(paged([invite('rot')])));
    const fixture = create();
    expect(el(fixture, 'my-team-bloodhounds')).not.toBeNull();
    expect(el(fixture, 'my-team-find')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid^="invite-"]')).toBeNull();
    // The invites read is only made for teamless players.
    expect(invitations.listMine).not.toHaveBeenCalled();
  });

  // --- US2: invitations list + accept/decline ------------------------------

  it('lists a teamless player’s pending invitations', () => {
    invitations.listMine.mockReturnValue(of(paged([invite('rot'), invite('blitz')])));
    const fixture = create();
    expect(el(fixture, 'invite-rot')).not.toBeNull();
    expect(el(fixture, 'invite-blitz')).not.toBeNull();
    expect(el(fixture, 'accept-rot')).not.toBeNull();
    expect(el(fixture, 'decline-rot')).not.toBeNull();
  });

  it('declining removes the row without joining', () => {
    invitations.listMine.mockReturnValue(of(paged([invite('rot')])));
    const fixture = create();
    (el(fixture, 'decline-rot') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(teamApi.declineInvite).toHaveBeenCalledWith('tok-rot');
    expect(el(fixture, 'invite-rot')).toBeNull();
    expect(navSpy).not.toHaveBeenCalled();
  });

  it('a stale accept removes the row and surfaces a friendly notice', () => {
    invitations.listMine.mockReturnValue(of(paged([invite('rot')])));
    teamApi.acceptInvite.mockReturnValue(throwError(() => new Error('409')));
    const fixture = create();
    (el(fixture, 'accept-rot') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(el(fixture, 'invite-rot')).toBeNull();
    expect(el(fixture, 'my-team-notice')).not.toBeNull();
    expect(navSpy).not.toHaveBeenCalled();
  });

  // --- US3: post-accept transition -----------------------------------------

  it('accepting refreshes memberships and navigates into the joined team', () => {
    invitations.listMine.mockReturnValue(of(paged([invite('rot')])));
    teamApi.acceptInvite.mockReturnValue(of({ teamSlug: 'rot' }));
    const fixture = create();
    (el(fixture, 'accept-rot') as HTMLButtonElement).click();
    expect(teamApi.acceptInvite).toHaveBeenCalledWith('tok-rot');
    expect(membership.load).toHaveBeenCalled();
    expect(navSpy).toHaveBeenCalledWith('/t/rot');
  });
});
