import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { TeamMember, TeamNews, TeamPublicDetail, TeamViewerRelation } from '../../../core/models/team.models';
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

function post(id: string, body: string, editedDate: string | null = null): TeamNews {
  return {
    id,
    authorDisplayName: 'Player 2',
    authorHandle: 'u2',
    authorRole: 'Admin',
    createdDate: '2026-09-27T19:00:00Z',
    editedDate,
    body,
  };
}

/**
 * Feature 057 — any admin can edit or delete any news post, in place on the team page. The server
 * is the boundary; these pin what the page offers and how it answers.
 */
describe('TeamDetailComponent — editing and deleting news (feature 057)', () => {
  let service: Record<string, jest.Mock>;

  function render(relation: TeamViewerRelation, news: TeamNews[]): ComponentFixture<TeamDetailComponent> {
    TestBed.resetTestingModule();
    service = {
      getPublicDetail: jest.fn().mockReturnValue(of(detail(relation))),
      getMembers: jest.fn().mockReturnValue(of(page([]))),
      getNews: jest.fn().mockReturnValue(of(page(news))),
      getHappenings: jest.fn().mockReturnValue(of([])),
      getJoinRequests: jest.fn().mockReturnValue(of(page([]))),
      logoUrl: jest.fn().mockReturnValue('/api/v1/teams/rheinfeuer/logo'),
      editNews: jest.fn(),
      deleteNews: jest.fn(),
    };
    TestBed.configureTestingModule({
      imports: [TeamDetailComponent, translocoTestingModule()],
      providers: [
        provideRouter([]),
        ...translocoLocaleTestingProviders(),
        { provide: TeamService, useValue: service },
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

  function el<T extends HTMLElement = HTMLElement>(fixture: ComponentFixture<TeamDetailComponent>, selector: string): T | null {
    return fixture.nativeElement.querySelector(selector);
  }

  function click(fixture: ComponentFixture<TeamDetailComponent>, selector: string): void {
    const target = el(fixture, selector);
    if (!target) {
      throw new Error(`Nothing matches ${selector}`);
    }
    target.click();
    fixture.detectChanges();
  }

  function openEditor(fixture: ComponentFixture<TeamDetailComponent>, id: string): HTMLTextAreaElement {
    click(fixture, `[data-news-menu-trigger="${id}"]`);
    click(fixture, '[data-testid="news-edit"]');
    return el<HTMLTextAreaElement>(fixture, '[data-testid="news-edit-input"]')!;
  }

  function type(fixture: ComponentFixture<TeamDetailComponent>, input: HTMLTextAreaElement, text: string): void {
    input.value = text;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  const bodies = (fixture: ComponentFixture<TeamDetailComponent>) =>
    Array.from(fixture.nativeElement.querySelectorAll('[data-testid="news-body"]')).map((p) => (p as HTMLElement).textContent?.trim());

  it('offers a plain member no way to edit or delete a post', () => {
    const fixture = render('Member', [post('p1', 'Training moves to Thursday.')]);
    expect(el(fixture, '[data-news-menu-trigger]')).toBeNull();
  });

  it('offers an admin the menu on every post, whoever wrote it', () => {
    const fixture = render('Admin', [post('p1', 'One.'), post('p2', 'Two.')]);
    expect(fixture.nativeElement.querySelectorAll('[data-news-menu-trigger]').length).toBe(2);
    expect(el(fixture, '[data-news-menu-trigger="p1"]')?.getAttribute('aria-label')).toBe('Manage post');
  });

  it('edits a post in place: sends the trimmed text, then shows it marked edited', () => {
    const fixture = render('Admin', [post('p1', 'Training moves to Thursday.')]);
    service['editNews'].mockReturnValue(of(post('p1', 'Training moves to Friday.', '2026-09-28T08:00:00Z')));

    const input = openEditor(fixture, 'p1');
    expect(document.activeElement).toBe(input);
    expect(input.value).toBe('Training moves to Thursday.');
    type(fixture, input, '  Training moves to Friday.  ');
    click(fixture, '[data-testid="news-edit-save"]');

    expect(service['editNews']).toHaveBeenCalledWith('rheinfeuer', 'p1', 'Training moves to Friday.');
    expect(el(fixture, '[data-testid="news-editor"]')).toBeNull();
    expect(bodies(fixture)).toEqual(['Training moves to Friday.']);
    expect(el(fixture, '[data-testid="news-meta"]')?.textContent).toContain('edited');
  });

  it('marks nothing edited that was never edited', () => {
    const fixture = render('Member', [post('p1', 'Fresh.')]);
    expect(el(fixture, '[data-testid="news-meta"]')?.textContent).not.toContain('edited');
  });

  it('sends nothing when the admin cancels, or saves the text unchanged', () => {
    const fixture = render('Admin', [post('p1', 'Same.')]);

    openEditor(fixture, 'p1');
    click(fixture, '[data-testid="news-edit-cancel"]');
    expect(el(fixture, '[data-testid="news-editor"]')).toBeNull();

    const input = openEditor(fixture, 'p1');
    type(fixture, input, ' Same. ');
    click(fixture, '[data-testid="news-edit-save"]');

    expect(service['editNews']).not.toHaveBeenCalled();
    expect(el(fixture, '[data-testid="news-editor"]')).toBeNull();
  });

  it('keeps the typed text and says so when a save fails', () => {
    const fixture = render('Admin', [post('p1', 'Before.')]);
    service['editNews'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 503 })));

    const input = openEditor(fixture, 'p1');
    type(fixture, input, 'After.');
    click(fixture, '[data-testid="news-edit-save"]');

    expect(el<HTMLTextAreaElement>(fixture, '[data-testid="news-edit-input"]')?.value).toBe('After.');
    expect(el(fixture, '[data-testid="news-edit-error"]')?.textContent?.trim()).toBe("We couldn't save your changes. Try again.");
  });

  it('drops a post another admin deleted meanwhile, and says so', () => {
    const fixture = render('Admin', [post('p1', 'Gone soon.'), post('p2', 'Stays.')]);
    service['editNews'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 })));

    const input = openEditor(fixture, 'p1');
    type(fixture, input, 'Too late.');
    click(fixture, '[data-testid="news-edit-save"]');

    expect(bodies(fixture)).toEqual(['Stays.']);
    expect(el(fixture, '[data-testid="news-notice"]')?.textContent?.trim()).toBe('This post no longer exists.');
  });

  it('closes an open menu on Escape and hands focus back to its button', () => {
    const fixture = render('Admin', [post('p1', 'One.')]);
    click(fixture, '[data-news-menu-trigger="p1"]');
    expect(el(fixture, '[data-testid="news-menu"]')).not.toBeNull();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();

    expect(el(fixture, '[data-testid="news-menu"]')).toBeNull();
    expect(document.activeElement).toBe(el(fixture, '[data-news-menu-trigger="p1"]'));
  });

  function openDeleteDialog(fixture: ComponentFixture<TeamDetailComponent>, id: string): void {
    click(fixture, `[data-news-menu-trigger="${id}"]`);
    click(fixture, '[data-testid="news-delete"]');
  }

  it('asks before deleting, with the safe answer focused, and Keep changes nothing', () => {
    const fixture = render('Admin', [post('p1', 'One.')]);

    openDeleteDialog(fixture, 'p1');

    expect(el(fixture, '[data-testid="news-delete-confirm"]')?.getAttribute('aria-modal')).toBe('true');
    expect(document.activeElement).toBe(el(fixture, '[data-testid="news-delete-keep"]'));
    click(fixture, '[data-testid="news-delete-keep"]');
    expect(el(fixture, '[data-testid="news-delete-confirm"]')).toBeNull();
    expect(service['deleteNews']).not.toHaveBeenCalled();
    expect(bodies(fixture)).toEqual(['One.']);
  });

  it('deletes on confirm, takes the post off the list and lands focus on the News heading', () => {
    const fixture = render('Admin', [post('p1', 'Wrong team.'), post('p2', 'Stays.')]);
    service['deleteNews'].mockReturnValue(of(undefined));

    openDeleteDialog(fixture, 'p1');
    click(fixture, '[data-testid="news-delete-submit"]');

    expect(service['deleteNews']).toHaveBeenCalledWith('rheinfeuer', 'p1');
    expect(el(fixture, '[data-testid="news-delete-confirm"]')).toBeNull();
    expect(bodies(fixture)).toEqual(['Stays.']);
    expect(document.activeElement).toBe(el(fixture, '#team-news-heading'));
  });

  it('treats a post that is already gone as deleted, and says so', () => {
    const fixture = render('Admin', [post('p1', 'Gone.')]);
    service['deleteNews'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 })));

    openDeleteDialog(fixture, 'p1');
    click(fixture, '[data-testid="news-delete-submit"]');

    expect(el(fixture, '[data-testid="news-delete-confirm"]')).toBeNull();
    expect(bodies(fixture)).toEqual([]);
    expect(el(fixture, '[data-testid="news-notice"]')?.textContent?.trim()).toBe('This post no longer exists.');
  });

  it('keeps the dialog open and says so when a delete fails', () => {
    const fixture = render('Admin', [post('p1', 'Still here.')]);
    service['deleteNews'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 503 })));

    openDeleteDialog(fixture, 'p1');
    click(fixture, '[data-testid="news-delete-submit"]');

    expect(el(fixture, '[data-testid="news-delete-confirm"]')).not.toBeNull();
    expect(el(fixture, '[data-testid="news-delete-error"]')?.textContent?.trim()).toBe("We couldn't delete the post. Try again.");
    expect(bodies(fixture)).toEqual(['Still here.']);
  });

  it('closes the dialog on Escape and hands focus back to the post menu', () => {
    const fixture = render('Admin', [post('p1', 'One.')]);
    openDeleteDialog(fixture, 'p1');

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();

    expect(el(fixture, '[data-testid="news-delete-confirm"]')).toBeNull();
    expect(document.activeElement).toBe(el(fixture, '[data-news-menu-trigger="p1"]'));
    expect(service['deleteNews']).not.toHaveBeenCalled();
  });
});
