import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { TeamMember, TeamPublicDetail, TeamViewerRelation } from '../../../core/models/team.models';
import { AuthService } from '../../../core/services/auth.service';
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
  return { userId, handle: `u${userId.slice(-1)}`, displayName: `Player ${userId.slice(-1)}`, role, hasAvatar: false, pompfen: [] };
}

const page = <T>(items: T[]) => ({ items, totalCount: items.length, skip: 0, take: 50 });

/**
 * GH #361 — the team page's part of "a member can leave": every member reaches "Manage team",
 * and the roster's admin menu stays off the admin's own row (what it would offer there lives on
 * that page instead).
 */
describe('TeamDetailComponent — manage link and own roster row (GH #361)', () => {
  function render(relation: TeamViewerRelation, roster: TeamMember[]): ComponentFixture<TeamDetailComponent> {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [TeamDetailComponent, translocoTestingModule()],
      providers: [
        provideRouter([]),
        ...translocoLocaleTestingProviders(),
        {
          provide: TeamService,
          useValue: {
            getPublicDetail: jest.fn().mockReturnValue(of(detail(relation))),
            getMembers: jest.fn().mockReturnValue(of(page(roster))),
            getNews: jest.fn().mockReturnValue(of(page([]))),
            getHappenings: jest.fn().mockReturnValue(of([])),
            getJoinRequests: jest.fn().mockReturnValue(of(page([]))),
            logoUrl: jest.fn().mockReturnValue('/api/v1/teams/rheinfeuer/logo'),
          },
        },
        { provide: PartyService, useValue: { getTeamPartyRequests: jest.fn().mockReturnValue(of(page([]))) } },
        { provide: ResultsService, useValue: { getTeamPlacements: jest.fn().mockReturnValue(of(page([]))) } },
        { provide: AuthService, useValue: { currentUser: () => ({ id: ME }) } },
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

  function memberMenus(fixture: ComponentFixture<TeamDetailComponent>): number {
    return fixture.nativeElement.querySelectorAll('[data-testid="roster"] [aria-label="Manage member"]').length;
  }

  it('offers a plain member the Manage link, without the admin-only invites link', () => {
    const fixture = render('Member', [member(ME, 'Member'), member(OTHER, 'Admin')]);
    expect(query(fixture, 'manage-team')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="team-tools"] a[href="/t/rheinfeuer/invitations"]')).toBeNull();
  });

  it('offers no team tools to a non-member', () => {
    const fixture = render('NonMember', []);
    expect(query(fixture, 'team-tools')).toBeNull();
  });

  it('draws the roster menu on every row but the admin’s own', () => {
    const fixture = render('Admin', [member(ME, 'Admin'), member(OTHER, 'Member')]);
    expect(memberMenus(fixture)).toBe(1);
  });

  it('draws no roster menu for a plain member', () => {
    const fixture = render('Member', [member(ME, 'Member'), member(OTHER, 'Admin')]);
    expect(memberMenus(fixture)).toBe(0);
  });
});
