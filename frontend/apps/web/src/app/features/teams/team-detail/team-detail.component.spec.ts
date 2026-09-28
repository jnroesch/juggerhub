import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, ParamMap, Router, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, Observable, Subject, of, throwError } from 'rxjs';
import { JoinRequest, TeamMember, TeamNews, TeamPublicDetail, TeamViewerRelation } from '../../../core/models/team.models';
import { AuthService } from '../../../core/services/auth.service';
import { ChatService } from '../../../core/services/chat.service';
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
    description: null,
    links: [],
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
        // Feature 060 — the team page asks the chat client only when Team chat is pressed.
        { provide: ChatService, useValue: { openTeamChat: jest.fn() } },
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

  function render(
    relation: TeamViewerRelation,
    news: TeamNews[],
    paramMap: Observable<ParamMap> = of(convertToParamMap({ slug: 'rheinfeuer' })),
  ): ComponentFixture<TeamDetailComponent> {
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
        // Feature 060 — the team page asks the chat client only when Team chat is pressed.
        { provide: ChatService, useValue: { openTeamChat: jest.fn() } },
        { provide: ActivatedRoute, useValue: { paramMap } },
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
    const input = el<HTMLTextAreaElement>(fixture, '[data-testid="news-edit-input"]');
    if (!input) {
      throw new Error('The editor did not open');
    }
    return input;
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

  it('forgets an editor left open when the page switches to another team', () => {
    // The router reuses this component between team pages (back/forward, /t/a → /t/b).
    const params = new BehaviorSubject(convertToParamMap({ slug: 'rheinfeuer' }));
    const fixture = render('Admin', [post('p1', 'One.')], params);
    openEditor(fixture, 'p1');

    params.next(convertToParamMap({ slug: 'another-team' }));
    fixture.detectChanges();

    expect(el(fixture, '[data-testid="news-editor"]')).toBeNull();
    expect(el<HTMLButtonElement>(fixture, '[data-news-menu-trigger="p1"]')?.disabled).toBe(false);
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

/**
 * Feature 058 — answering a join request. An answer happens at most once: a request another admin
 * answered first (or the player withdrew) comes back 404, which the page explains in its own words.
 */
describe('TeamDetailComponent — answering join requests (feature 058)', () => {
  let service: Record<string, jest.Mock>;

  const request: JoinRequest = {
    id: '00000000-0000-7000-8000-0000000000c1',
    handle: 'jonas',
    displayName: 'Jonas Weber',
    hasAvatar: false,
    createdDate: '2026-09-28T08:00:00Z',
  };

  function render(): ComponentFixture<TeamDetailComponent> {
    TestBed.resetTestingModule();
    service = {
      getPublicDetail: jest.fn().mockReturnValue(of(detail('Admin'))),
      getMembers: jest.fn().mockReturnValue(of(page([]))),
      getNews: jest.fn().mockReturnValue(of(page([]))),
      getHappenings: jest.fn().mockReturnValue(of([])),
      getJoinRequests: jest.fn().mockReturnValue(of(page([request]))),
      logoUrl: jest.fn().mockReturnValue('/api/v1/teams/rheinfeuer/logo'),
      approveJoinRequest: jest.fn(),
      declineJoinRequest: jest.fn(),
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
        // Feature 060 — the team page asks the chat client only when Team chat is pressed.
        { provide: ChatService, useValue: { openTeamChat: jest.fn() } },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ slug: 'rheinfeuer' })) } },
      ],
    });
    const fixture = TestBed.createComponent(TeamDetailComponent);
    fixture.detectChanges();
    return fixture;
  }

  function click(fixture: ComponentFixture<TeamDetailComponent>, testId: string): void {
    (fixture.nativeElement.querySelector(`[data-testid="${testId}"]`) as HTMLElement).click();
    fixture.detectChanges();
  }

  const textOf = (fixture: ComponentFixture<TeamDetailComponent>, testId: string) =>
    (fixture.nativeElement.querySelector(`[data-testid="${testId}"]`) as HTMLElement | null)?.textContent?.trim();

  it('says a request no longer waits when another admin answered first, and shows the queue as it is now', () => {
    const fixture = render();
    service['approveJoinRequest'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 })));
    service['getJoinRequests'].mockReturnValue(of(page([])));

    click(fixture, 'approve');

    expect(textOf(fixture, 'join-notice')).toBe('This request was already answered or withdrawn.');
    expect(fixture.nativeElement.querySelector('[data-testid="join-queue"]')).toBeNull();
    expect(service['getPublicDetail']).toHaveBeenCalledTimes(2);
  });

  it('keeps the queue and says so in its own words when an answer fails', () => {
    const fixture = render();
    service['declineJoinRequest'].mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 500, error: { detail: 'Some English server text' } })),
    );

    click(fixture, 'decline');

    expect(textOf(fixture, 'answer-error')).toBe("We couldn't save your answer just now.");
    expect(fixture.nativeElement.textContent).not.toContain('Some English server text');
    expect(fixture.nativeElement.querySelector('[data-testid="join-queue"]')).not.toBeNull();
  });
});

/**
 * Feature 058 — asking to join, when the server says no. Each status gets the page's own sentence,
 * never the server's English text, and a 429 (our own limit, FR-023) says when to try again.
 */
describe('TeamDetailComponent — asking to join fails (feature 058)', () => {
  let service: Record<string, jest.Mock>;

  function render(): ComponentFixture<TeamDetailComponent> {
    TestBed.resetTestingModule();
    service = {
      getPublicDetail: jest.fn().mockReturnValue(of(detail('NonMember'))),
      getMembers: jest.fn().mockReturnValue(of(page([]))),
      getNews: jest.fn().mockReturnValue(of(page([]))),
      getHappenings: jest.fn().mockReturnValue(of([])),
      getJoinRequests: jest.fn().mockReturnValue(of(page([]))),
      logoUrl: jest.fn().mockReturnValue('/api/v1/teams/rheinfeuer/logo'),
      requestToJoin: jest.fn(),
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
        // Feature 060 — the team page asks the chat client only when Team chat is pressed.
        { provide: ChatService, useValue: { openTeamChat: jest.fn() } },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ slug: 'rheinfeuer' })) } },
      ],
    });
    const fixture = TestBed.createComponent(TeamDetailComponent);
    fixture.detectChanges();
    return fixture;
  }

  function ask(fixture: ComponentFixture<TeamDetailComponent>, status: number): string | undefined {
    service['requestToJoin'].mockReturnValue(
      throwError(() => new HttpErrorResponse({ status, error: { detail: 'Some English server text' } })),
    );
    (fixture.nativeElement.querySelector('[data-testid="request-to-join"]') as HTMLElement).click();
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('[data-testid="join-confirm-submit"]') as HTMLElement).click();
    fixture.detectChanges();
    return (fixture.nativeElement.querySelector('[data-testid="request-error"]') as HTMLElement | null)?.textContent?.trim();
  }

  it('says when to try again after too many requests, and sends nothing more', () => {
    const fixture = render();
    expect(ask(fixture, 429)).toBe("You've sent a lot of join requests in a short time. Try again in a little while.");
    expect(service['requestToJoin']).toHaveBeenCalledTimes(1);
    expect(fixture.nativeElement.textContent).not.toContain('Some English server text');
  });

  it('says the player is already on the team, and shows the page afresh', () => {
    const fixture = render();
    expect(ask(fixture, 409)).toBe("You're already on this team.");
    expect(service['getPublicDetail']).toHaveBeenCalledTimes(2);
  });

  it('says anything else plainly, in its own words', () => {
    const fixture = render();
    expect(ask(fixture, 500)).toBe("We couldn't send your request just now.");
  });
});

/**
 * Feature 060 (GH #362) — the team page's way into the team chat, and where a member's actions live.
 * The server decides who may open the chat; these pin what the page offers, when it asks, and how it
 * answers.
 */
describe('TeamDetailComponent — the team chat and the member card (feature 060)', () => {
  let service: Record<string, jest.Mock>;
  let chat: { openTeamChat: jest.Mock };
  let navigate: jest.SpyInstance;

  function render(relation: TeamViewerRelation): ComponentFixture<TeamDetailComponent> {
    TestBed.resetTestingModule();
    service = {
      getPublicDetail: jest.fn().mockReturnValue(of(detail(relation))),
      getMembers: jest.fn().mockReturnValue(of(page([]))),
      getNews: jest.fn().mockReturnValue(of(page([]))),
      getHappenings: jest.fn().mockReturnValue(of([])),
      getJoinRequests: jest.fn().mockReturnValue(of(page([]))),
      logoUrl: jest.fn().mockReturnValue('/api/v1/teams/rheinfeuer/logo'),
    };
    chat = { openTeamChat: jest.fn() };
    TestBed.configureTestingModule({
      imports: [TeamDetailComponent, translocoTestingModule()],
      providers: [
        provideRouter([]),
        ...translocoLocaleTestingProviders(),
        { provide: TeamService, useValue: service },
        { provide: PartyService, useValue: { getTeamPartyRequests: jest.fn().mockReturnValue(of(page([]))) } },
        { provide: ResultsService, useValue: { getTeamPlacements: jest.fn().mockReturnValue(of(page([]))) } },
        { provide: AuthService, useValue: { currentUser: () => (relation === 'Anonymous' ? null : { id: ME }) } },
        { provide: ChatService, useValue: chat },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ slug: 'rheinfeuer' })) } },
      ],
    });
    navigate = jest.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(TeamDetailComponent);
    fixture.detectChanges();
    return fixture;
  }

  const el = (fixture: ComponentFixture<TeamDetailComponent>, selector: string) =>
    fixture.nativeElement.querySelector(selector) as HTMLElement | null;

  const textOf = (fixture: ComponentFixture<TeamDetailComponent>, testId: string) =>
    el(fixture, `[data-testid="${testId}"]`)?.textContent?.trim();

  /** Buttons and links beside the team's name — what the page offers at the top. */
  const headerActions = (fixture: ComponentFixture<TeamDetailComponent>) =>
    Array.from(fixture.nativeElement.querySelectorAll('header button, header a') as NodeListOf<HTMLElement>).map(
      (e) => e.getAttribute('data-testid'),
    );

  function press(fixture: ComponentFixture<TeamDetailComponent>, testId: string): void {
    el(fixture, `[data-testid="${testId}"]`)!.click();
    fixture.detectChanges();
  }

  // --- US1: Team chat -------------------------------------------------------------------------

  it.each(['Admin', 'Member'] as const)('offers Team chat to a %s, in the card', (relation) => {
    const fixture = render(relation);
    expect(textOf(fixture, 'team-chat')).toBe('Team chat');
    expect(el(fixture, '[data-testid="team-tools"] [data-testid="team-chat"]')).not.toBeNull();
  });

  it.each(['NonMember', 'Requested', 'Anonymous'] as const)('offers no Team chat to a %s viewer', (relation) => {
    const fixture = render(relation);
    expect(el(fixture, '[data-testid="team-chat"]')).toBeNull();
  });

  it('does not look the chat up while the page loads (SC-004)', () => {
    render('Member');
    expect(chat.openTeamChat).not.toHaveBeenCalled();
  });

  it('opens the chat the server names for this team', () => {
    const fixture = render('Member');
    chat.openTeamChat.mockReturnValue(of({ conversationId: 'c9' }));

    press(fixture, 'team-chat');

    expect(chat.openTeamChat).toHaveBeenCalledWith(detail('Member').id);
    expect(navigate).toHaveBeenCalledWith(['/chat', 'c9']);
  });

  it('shows that it is working and takes no second press', () => {
    const fixture = render('Member');
    chat.openTeamChat.mockReturnValue(new Subject());

    press(fixture, 'team-chat');
    press(fixture, 'team-chat');

    const button = el(fixture, '[data-testid="team-chat"]') as HTMLButtonElement;
    expect(button.disabled).toBe(true);
    expect(button.textContent?.trim()).toBe('Opening…');
    expect(chat.openTeamChat).toHaveBeenCalledTimes(1);
  });

  it('says the player is no longer on the team, and shows the page as they now see it', () => {
    const fixture = render('Member');
    chat.openTeamChat.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 })));
    service['getPublicDetail'].mockReturnValue(of(detail('NonMember')));

    press(fixture, 'team-chat');

    expect(textOf(fixture, 'team-chat-notice')).toBe("You're no longer on this team.");
    expect(service['getPublicDetail']).toHaveBeenCalledTimes(2);
    expect(el(fixture, '[data-testid="team-tools"]')).toBeNull();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('says anything else went wrong in its own words, in the card, and can be pressed again', () => {
    const fixture = render('Member');
    chat.openTeamChat.mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 500, error: { detail: 'Some English server text' } })),
    );

    press(fixture, 'team-chat');

    expect(el(fixture, '[data-testid="team-tools"] [data-testid="team-chat-error"]')?.textContent?.trim()).toBe(
      "We couldn't open the team chat just now.",
    );
    expect(fixture.nativeElement.textContent).not.toContain('Some English server text');
    expect((el(fixture, '[data-testid="team-chat"]') as HTMLButtonElement).disabled).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
  });

  // --- US3: a member's actions sit together in the card ---------------------------------------

  it('gives a plain member Team chat, Contact admins and Manage in the card, and nothing at the top', () => {
    const fixture = render('Member');
    const card = Array.from(
      fixture.nativeElement.querySelectorAll('[data-testid="team-tools"] button, [data-testid="team-tools"] a') as NodeListOf<HTMLElement>,
    ).map((e) => e.getAttribute('data-testid'));

    expect(card).toEqual(['team-chat', 'contact-admins', 'manage-team']);
    expect(headerActions(fixture)).toEqual([]);
  });

  it('gives an admin no Contact admins, and nothing at the top', () => {
    const fixture = render('Admin');
    expect(el(fixture, '[data-testid="contact-admins"]')).toBeNull();
    expect(headerActions(fixture)).toEqual([]);
  });

  it('keeps Contact admins and Request to join at the top for a non-member', () => {
    const fixture = render('NonMember');
    expect(headerActions(fixture)).toEqual(['contact-admins', 'request-to-join']);
    expect(el(fixture, '[data-testid="team-tools"]')).toBeNull();
  });

  it('keeps a pending request and its withdrawal at the top', () => {
    const fixture = render('Requested');
    expect(headerActions(fixture)).toEqual(['contact-admins', 'cancel-request']);
    expect(el(fixture, '[data-testid="requested"]')).not.toBeNull();
  });

  it('keeps sign-in-to-join at the top for a signed-out visitor', () => {
    const fixture = render('Anonymous');
    expect(headerActions(fixture)).toEqual(['signin-to-join']);
  });

  it('opens Contact admins from the card exactly as it did from the top', () => {
    const fixture = render('Member');

    press(fixture, 'contact-admins');

    expect(navigate).toHaveBeenCalledWith(['/chat', 'contact', 'team', detail('Member').id], { state: { name: 'Rheinfeuer' } });
  });
});