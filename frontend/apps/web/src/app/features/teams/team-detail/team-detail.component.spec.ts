import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { TeamMember, TeamPublicDetail, TeamViewerRelation } from '../../../core/models/team.models';
import { AuthService } from '../../../core/services/auth.service';
import { MembershipService } from '../../../core/services/membership.service';
import { PartyService } from '../../../core/services/party.service';
import { ResultsService } from '../../../core/services/results.service';
import { TeamService } from '../../../core/services/team.service';
import { TeamDetailComponent } from './team-detail.component';
import { translocoLocaleTestingProviders, translocoTestingModule } from '../../../../testing/transloco-testing';

const ME = '00000000-0000-7000-8000-000000000001';
const OTHER = '00000000-0000-7000-8000-000000000002';

function detail(viewerRelation: TeamViewerRelation): TeamPublicDetail {
  return {
    id: '00000000-0000-7000-8000-0000000000aa',
    slug: 'rheinfeuer',
    name: 'Rheinfeuer',
    type: 'CityTeam',
    location: null,
    memberCount: 2,
    beginnersWelcome: false,
    isActive: true,
    viewerRelation,
    hasLogo: false,
    roster: [],
    recentActivity: [],
    badges: [],
    achievements: [],
  };
}

function member(userId: string, role: TeamMember['role']): TeamMember {
  return { userId, handle: `u-${userId.slice(-1)}`, displayName: `Player ${userId.slice(-1)}`, role, hasAvatar: false, pompfen: [] };
}

const page = <T>(items: T[]) => ({ items, totalCount: items.length, skip: 0, take: 50 });

/**
 * GH #361 — a member leaves the team from the team page. The server owns the rule
 * (`MutateMembershipAsync`: self-removal is allowed, the last admin is refused); these pin what
 * only the page can get wrong: who is offered the button, that the sole admin gets an explanation
 * and never a dead button, and that leaving refreshes the cached memberships before landing on
 * "My team".
 */
describe('TeamDetailComponent — leave team (GH #361)', () => {
  let teams: { removeMember: jest.Mock; getMembers: jest.Mock };
  let membership: { load: jest.Mock };

  function render(relation: TeamViewerRelation, roster: TeamMember[]): ComponentFixture<TeamDetailComponent> {
    teams = {
      getPublicDetail: jest.fn().mockReturnValue(of(detail(relation))),
      getMembers: jest.fn().mockReturnValue(of(page(roster))),
      getNews: jest.fn().mockReturnValue(of(page([]))),
      getHappenings: jest.fn().mockReturnValue(of([])),
      getJoinRequests: jest.fn().mockReturnValue(of(page([]))),
      removeMember: jest.fn().mockReturnValue(of(undefined)),
      logoUrl: jest.fn().mockReturnValue('/api/v1/teams/rheinfeuer/logo'),
    } as unknown as typeof teams;
    membership = { load: jest.fn() };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [TeamDetailComponent, translocoTestingModule()],
      providers: [
        provideRouter([]),
        ...translocoLocaleTestingProviders(),
        { provide: TeamService, useValue: teams },
        { provide: PartyService, useValue: { getTeamPartyRequests: jest.fn().mockReturnValue(of(page([]))) } },
        { provide: ResultsService, useValue: { getTeamPlacements: jest.fn().mockReturnValue(of(page([]))) } },
        { provide: AuthService, useValue: { currentUser: () => ({ id: ME }) } },
        { provide: MembershipService, useValue: membership },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ slug: 'rheinfeuer' })) } },
      ],
    });
    const fixture = TestBed.createComponent(TeamDetailComponent);
    fixture.detectChanges();
    return fixture;
  }

  function query(fixture: ComponentFixture<TeamDetailComponent>, testId: string): HTMLElement | null {
    return fixture.nativeElement.querySelector(`[data-testid="${testId}"]`);
  }

  function click(fixture: ComponentFixture<TeamDetailComponent>, testId: string): void {
    const el = query(fixture, testId);
    expect(el).not.toBeNull();
    (el as HTMLElement).click();
    fixture.detectChanges();
  }

  it('offers a plain member the leave button', () => {
    const fixture = render('Member', [member(ME, 'Member'), member(OTHER, 'Admin')]);
    expect(query(fixture, 'leave-team')).not.toBeNull();
    expect(query(fixture, 'sole-admin-note')).toBeNull();
  });

  it('offers an admin the leave button when another admin remains', () => {
    const fixture = render('Admin', [member(ME, 'Admin'), member(OTHER, 'Admin')]);
    expect(query(fixture, 'leave-team')).not.toBeNull();
    expect(query(fixture, 'sole-admin-note')).toBeNull();
  });

  it('explains instead of rendering a dead button for the sole admin', () => {
    const fixture = render('Admin', [member(ME, 'Admin'), member(OTHER, 'Member')]);
    expect(query(fixture, 'leave-team')).toBeNull();
    expect(query(fixture, 'sole-admin-note')).not.toBeNull();
  });

  it('offers nothing to a non-member', () => {
    const fixture = render('NonMember', []);
    expect(query(fixture, 'membership')).toBeNull();
    expect(query(fixture, 'leave-team')).toBeNull();
  });

  it('asks first, and "Stay" leaves the membership untouched', () => {
    const fixture = render('Member', [member(ME, 'Member'), member(OTHER, 'Admin')]);
    click(fixture, 'leave-team');
    expect(query(fixture, 'leave-confirm')).not.toBeNull();

    const dismiss = query(fixture, 'leave-confirm')?.querySelector('button') as HTMLButtonElement;
    dismiss.click();
    fixture.detectChanges();

    expect(query(fixture, 'leave-confirm')).toBeNull();
    expect(teams.removeMember).not.toHaveBeenCalled();
  });

  it('removes the viewer themselves, refreshes the cached memberships and lands on My team', () => {
    const fixture = render('Member', [member(ME, 'Member'), member(OTHER, 'Admin')]);
    const navigate = jest.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    click(fixture, 'leave-team');
    click(fixture, 'leave-confirm-submit');

    expect(teams.removeMember).toHaveBeenCalledWith('rheinfeuer', ME);
    expect(membership.load).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith(['/my-team']);
  });

  it('shows the server reason and re-reads the roster when the leave is refused', () => {
    const fixture = render('Admin', [member(ME, 'Admin'), member(OTHER, 'Admin')]);
    const navigate = jest.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    teams.removeMember.mockReturnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 409,
            error: { detail: 'Make someone else an admin before you step down or leave.' },
          }),
      ),
    );
    // The other admin stepped down while the modal was open.
    teams.getMembers.mockReturnValue(of(page([member(ME, 'Admin'), member(OTHER, 'Member')])));

    click(fixture, 'leave-team');
    click(fixture, 'leave-confirm-submit');

    expect(navigate).not.toHaveBeenCalled();
    expect(membership.load).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('[role="alert"]')?.textContent).toContain('Make someone else an admin');
    expect(query(fixture, 'leave-confirm')).toBeNull();
    expect(query(fixture, 'leave-team')).toBeNull();
    expect(query(fixture, 'sole-admin-note')).not.toBeNull();
  });
});
