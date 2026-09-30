import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, ParamMap, Router, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, Observable, Subject, of, throwError } from 'rxjs';
import { JoinRequest, TeamMember, TeamNews, TeamPublicDetail, TeamViewerRelation } from '../../../core/models/team.models';
import { AuthService } from '../../../core/services/auth.service';
import { ChatService } from '../../../core/services/chat.service';
import { PollService } from '../../../core/services/poll.service';
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
        // Feature 062: the member-only Polls card loads its own lists.
        { provide: PollService, useValue: { list: jest.fn().mockReturnValue(of(page([]))) } },
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

    expect(el(fixture, '[data-testid="confirm-dialog"]')?.getAttribute('aria-modal')).toBe('true');
    expect(document.activeElement).toBe(el(fixture, '[data-testid="confirm-dialog-keep"]'));
    click(fixture, '[data-testid="confirm-dialog-keep"]');
    expect(el(fixture, '[data-testid="confirm-dialog"]')).toBeNull();
    expect(service['deleteNews']).not.toHaveBeenCalled();
    expect(bodies(fixture)).toEqual(['One.']);
  });

  it('deletes on confirm, takes the post off the list and lands focus on the News heading', () => {
    const fixture = render('Admin', [post('p1', 'Wrong team.'), post('p2', 'Stays.')]);
    service['deleteNews'].mockReturnValue(of(undefined));

    openDeleteDialog(fixture, 'p1');
    click(fixture, '[data-testid="confirm-dialog-confirm"]');

    expect(service['deleteNews']).toHaveBeenCalledWith('rheinfeuer', 'p1');
    expect(el(fixture, '[data-testid="confirm-dialog"]')).toBeNull();
    expect(bodies(fixture)).toEqual(['Stays.']);
    expect(document.activeElement).toBe(el(fixture, '#team-news-heading'));
  });

  it('treats a post that is already gone as deleted, and says so', () => {
    const fixture = render('Admin', [post('p1', 'Gone.')]);
    service['deleteNews'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 })));

    openDeleteDialog(fixture, 'p1');
    click(fixture, '[data-testid="confirm-dialog-confirm"]');

    expect(el(fixture, '[data-testid="confirm-dialog"]')).toBeNull();
    expect(bodies(fixture)).toEqual([]);
    expect(el(fixture, '[data-testid="news-notice"]')?.textContent?.trim()).toBe('This post no longer exists.');
  });

  it('keeps the dialog open and says so when a delete fails', () => {
    const fixture = render('Admin', [post('p1', 'Still here.')]);
    service['deleteNews'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 503 })));

    openDeleteDialog(fixture, 'p1');
    click(fixture, '[data-testid="confirm-dialog-confirm"]');

    expect(el(fixture, '[data-testid="confirm-dialog"]')).not.toBeNull();
    expect(el(fixture, '[data-testid="confirm-dialog-error"]')?.textContent?.trim()).toBe("We couldn't delete the post. Try again.");
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

    expect(el(fixture, '[data-testid="confirm-dialog"]')).toBeNull();
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
    (fixture.nativeElement.querySelector('[data-testid="confirm-dialog-confirm"]') as HTMLElement).click();
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
    // The button that asked went with the reload; focus is on the note that says why (GH #392).
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector('[data-testid="request-error"]'));
  });

  it('says anything else plainly, in its own words', () => {
    const fixture = render();
    expect(ask(fixture, 500)).toBe("We couldn't send your request just now.");
    // The dialog closed; focus is back on the button that asked, not on the page body (GH #392).
    expect(fixture.nativeElement.querySelector('[data-testid="confirm-dialog"]')).toBeNull();
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector('[data-testid="request-to-join"]'));
  });
});

/**
 * GH #392 — the join / withdraw confirmation (feature 009) asks through the shared dialog. What that
 * buys, and what only the page can get wrong: the safe answer has the focus, nothing is sent twice,
 * and focus goes somewhere sensible when the dialog closes — it never falls to the page body.
 */
describe('TeamDetailComponent — the join confirmation (GH #392)', () => {
  let service: Record<string, jest.Mock>;

  function render(
    relation: TeamViewerRelation,
    paramMap: Observable<ParamMap> = of(convertToParamMap({ slug: 'rheinfeuer' })),
  ): ComponentFixture<TeamDetailComponent> {
    TestBed.resetTestingModule();
    service = {
      getPublicDetail: jest.fn().mockReturnValue(of(detail(relation))),
      logoUrl: jest.fn().mockReturnValue('/api/v1/teams/rheinfeuer/logo'),
      requestToJoin: jest.fn(),
      cancelJoinRequest: jest.fn(),
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
        { provide: ChatService, useValue: { openTeamChat: jest.fn() } },
        { provide: ActivatedRoute, useValue: { paramMap } },
      ],
    });
    const fixture = TestBed.createComponent(TeamDetailComponent);
    fixture.detectChanges();
    return fixture;
  }

  const el = <T extends HTMLElement = HTMLElement>(fixture: ComponentFixture<TeamDetailComponent>, testId: string): T | null =>
    fixture.nativeElement.querySelector(`[data-testid="${testId}"]`);

  function click(fixture: ComponentFixture<TeamDetailComponent>, testId: string): void {
    const target = el(fixture, testId);
    if (!target) {
      throw new Error(`Nothing matches ${testId}`);
    }
    target.click();
    fixture.detectChanges();
  }

  function escape(fixture: ComponentFixture<TeamDetailComponent>): void {
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();
  }

  it('asks by name, with the focus on the safe answer and a primary — not a red — acting answer', () => {
    const fixture = render('NonMember');

    click(fixture, 'request-to-join');

    const dialog = el(fixture, 'confirm-dialog');
    expect(dialog?.getAttribute('aria-modal')).toBe('true');
    expect(dialog?.textContent).toContain('Request to join Rheinfeuer?');
    expect(el(fixture, 'confirm-dialog-keep')?.textContent?.trim()).toBe('Not now');
    expect(document.activeElement).toBe(el(fixture, 'confirm-dialog-keep'));
    const send = el(fixture, 'confirm-dialog-confirm');
    expect(send?.textContent?.trim()).toBe('Send request');
    expect(send?.classList).toContain('bg-brand-strong');
    expect(send?.classList).not.toContain('text-danger-fg');
    // Both answers are touch targets: the default 44px size, not the 36px small one.
    expect(send?.classList).toContain('min-h-11');
    expect(service['requestToJoin']).not.toHaveBeenCalled();
  });

  it('sends nothing on Not now or on Escape, and hands focus back to the button that asked', () => {
    const fixture = render('NonMember');

    click(fixture, 'request-to-join');
    click(fixture, 'confirm-dialog-keep');
    expect(el(fixture, 'confirm-dialog')).toBeNull();
    expect(document.activeElement).toBe(el(fixture, 'request-to-join'));

    click(fixture, 'request-to-join');
    escape(fixture);
    expect(el(fixture, 'confirm-dialog')).toBeNull();
    expect(document.activeElement).toBe(el(fixture, 'request-to-join'));

    expect(service['requestToJoin']).not.toHaveBeenCalled();
  });

  it('sends the request once, takes no other answer meanwhile, then lands on the line that says it is in', () => {
    const fixture = render('NonMember');
    const sending = new Subject<void>();
    service['requestToJoin'].mockReturnValue(sending);
    // The reload answers later, as it does over a network: focus must wait for the page, not fire
    // into the loading line that stands in for it.
    const reload = new Subject<TeamPublicDetail>();
    service['getPublicDetail'].mockReturnValueOnce(reload);

    click(fixture, 'request-to-join');
    click(fixture, 'confirm-dialog-confirm');
    el(fixture, 'confirm-dialog-confirm')?.click();
    escape(fixture);

    expect(service['requestToJoin']).toHaveBeenCalledTimes(1);
    expect(el(fixture, 'confirm-dialog')).not.toBeNull();
    expect(el<HTMLButtonElement>(fixture, 'confirm-dialog-keep')?.disabled).toBe(true);

    sending.next();
    fixture.detectChanges();
    expect(el(fixture, 'confirm-dialog')).toBeNull();
    reload.next(detail('Requested'));
    fixture.detectChanges();

    expect(el(fixture, 'requested')).not.toBeNull();
    expect(document.activeElement).toBe(el(fixture, 'requested'));
  });

  it('asks before withdrawing, keeps the request on Keep request, and hands focus back', () => {
    const fixture = render('Requested');

    click(fixture, 'cancel-request');

    expect(el(fixture, 'confirm-dialog')?.textContent).toContain('Withdraw your request?');
    expect(document.activeElement).toBe(el(fixture, 'confirm-dialog-keep'));
    click(fixture, 'confirm-dialog-keep');
    expect(el(fixture, 'confirm-dialog')).toBeNull();
    expect(service['cancelJoinRequest']).not.toHaveBeenCalled();
    expect(document.activeElement).toBe(el(fixture, 'cancel-request'));
  });

  it('withdraws on confirm and lands on the button that offers the request again', () => {
    const fixture = render('Requested');
    service['cancelJoinRequest'].mockReturnValue(of(undefined));
    service['getPublicDetail'].mockReturnValue(of(detail('NonMember')));

    click(fixture, 'cancel-request');
    click(fixture, 'confirm-dialog-confirm');

    expect(service['cancelJoinRequest']).toHaveBeenCalledWith('rheinfeuer');
    expect(el(fixture, 'confirm-dialog')).toBeNull();
    expect(document.activeElement).toBe(el(fixture, 'request-to-join'));
  });

  it('says a withdrawal failed at the top of the page, with the focus back on the button that asked', () => {
    const fixture = render('Requested');
    service['cancelJoinRequest'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 500 })));

    click(fixture, 'cancel-request');
    click(fixture, 'confirm-dialog-confirm');

    expect(el(fixture, 'confirm-dialog')).toBeNull();
    expect(el(fixture, 'request-error')?.textContent?.trim()).toBe("We couldn't withdraw your request just now.");
    expect(document.activeElement).toBe(el(fixture, 'cancel-request'));
  });

  it('drops an open question when the page moves to another team', () => {
    // The component is reused from team to team: the question would otherwise ask, unprompted,
    // about the team the page moved to.
    const route = new BehaviorSubject<ParamMap>(convertToParamMap({ slug: 'rheinfeuer' }));
    const fixture = render('NonMember', route);

    click(fixture, 'request-to-join');
    expect(el(fixture, 'confirm-dialog')).not.toBeNull();

    route.next(convertToParamMap({ slug: 'nordlicht' }));
    fixture.detectChanges();

    expect(el(fixture, 'confirm-dialog')).toBeNull();
    expect(service['requestToJoin']).not.toHaveBeenCalled();
  });

  it('shows the focus ring on both places it sends the focus to', () => {
    // CodeRabbit on PR #398: a target that takes the focus without showing it leaves a keyboard user
    // not knowing where they are. `focus-visible`, like every control: a mouse user gets no ring.
    const ring = ['focus-visible:ring-2', 'focus-visible:ring-focus'];
    const sent = render('NonMember');
    service['requestToJoin'].mockReturnValue(of(undefined));
    service['getPublicDetail'].mockReturnValue(of(detail('Requested')));
    click(sent, 'request-to-join');
    click(sent, 'confirm-dialog-confirm');
    expect(document.activeElement).toBe(el(sent, 'requested'));
    expect(Array.from(el(sent, 'requested')?.classList ?? [])).toEqual(expect.arrayContaining(ring));

    const refused = render('NonMember');
    service['requestToJoin'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 409 })));
    click(refused, 'request-to-join');
    click(refused, 'confirm-dialog-confirm');
    expect(document.activeElement).toBe(el(refused, 'request-error'));
    expect(Array.from(el(refused, 'request-error')?.classList ?? [])).toEqual(expect.arrayContaining(ring));
  });

  it('drops an answer that arrives after the page moved to another team', () => {
    // CodeRabbit on PR #398: the component is reused from team to team, and an answer about the team
    // the page left would reload this one, move the focus on it, or pin a failure note to it.
    const route = new BehaviorSubject<ParamMap>(convertToParamMap({ slug: 'rheinfeuer' }));
    const fixture = render('NonMember', route);
    const sending = new Subject<void>();
    service['requestToJoin'].mockReturnValue(sending);

    click(fixture, 'request-to-join');
    click(fixture, 'confirm-dialog-confirm');
    route.next(convertToParamMap({ slug: 'nordlicht' }));
    fixture.detectChanges();
    const loads = service['getPublicDetail'].mock.calls.length;
    // The request on its way was the other team's: this page's own button is not held by it.
    expect(el<HTMLButtonElement>(fixture, 'request-to-join')?.disabled).toBe(false);
    const focusedBefore = document.activeElement;

    sending.error(new HttpErrorResponse({ status: 500 }));
    fixture.detectChanges();

    expect(el(fixture, 'request-error')).toBeNull();
    expect(service['getPublicDetail']).toHaveBeenCalledTimes(loads);
    expect(document.activeElement).toBe(focusedBefore);
  });

  it('drops a late withdrawal answer the same way', () => {
    const route = new BehaviorSubject<ParamMap>(convertToParamMap({ slug: 'rheinfeuer' }));
    const fixture = render('Requested', route);
    const withdrawing = new Subject<void>();
    service['cancelJoinRequest'].mockReturnValue(withdrawing);

    click(fixture, 'cancel-request');
    click(fixture, 'confirm-dialog-confirm');
    route.next(convertToParamMap({ slug: 'nordlicht' }));
    fixture.detectChanges();
    const loads = service['getPublicDetail'].mock.calls.length;

    withdrawing.next();
    fixture.detectChanges();

    // No reload of the team the page is now on, and its own withdraw button is not held.
    expect(service['getPublicDetail']).toHaveBeenCalledTimes(loads);
    expect(el<HTMLButtonElement>(fixture, 'cancel-request')?.disabled).toBe(false);
  });

  it('drops an old answer even when the page has come back to the team it was about', () => {
    // CodeRabbit on PR #398: A → B → A. The slug is the same again, so comparing slugs lets the old
    // answer through, and it would close the question the page is now waiting on and unlock it.
    const route = new BehaviorSubject<ParamMap>(convertToParamMap({ slug: 'rheinfeuer' }));
    const fixture = render('NonMember', route);
    const first = new Subject<void>();
    const second = new Subject<void>();
    service['requestToJoin'].mockReturnValueOnce(first).mockReturnValueOnce(second);

    click(fixture, 'request-to-join');
    click(fixture, 'confirm-dialog-confirm');
    route.next(convertToParamMap({ slug: 'nordlicht' }));
    route.next(convertToParamMap({ slug: 'rheinfeuer' }));
    fixture.detectChanges();
    click(fixture, 'request-to-join');
    click(fixture, 'confirm-dialog-confirm');
    expect(service['requestToJoin']).toHaveBeenCalledTimes(2);
    const loads = service['getPublicDetail'].mock.calls.length;

    first.error(new HttpErrorResponse({ status: 500 }));
    fixture.detectChanges();

    // The newer request is still under way: its question stays, locked, and nothing is said yet.
    expect(el(fixture, 'confirm-dialog')).not.toBeNull();
    expect(el<HTMLButtonElement>(fixture, 'confirm-dialog-keep')?.disabled).toBe(true);
    expect(el(fixture, 'request-error')).toBeNull();
    expect(service['getPublicDetail']).toHaveBeenCalledTimes(loads);

    // Its own answer still lands.
    second.next();
    fixture.detectChanges();
    expect(el(fixture, 'confirm-dialog')).toBeNull();
    expect(service['getPublicDetail']).toHaveBeenCalledTimes(loads + 1);
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
/**
 * Feature 061 — the About card: the team's description and links, for every viewer of the page.
 * What only the page can get wrong: the text is shown as text (never markup, never a link), the
 * links open outside the app with no hold on this page (`noopener`) and no referrer
 * (`noreferrer`), each beside the site it really leads to, and nothing at all is drawn for a team
 * that has neither.
 */
describe('TeamDetailComponent — about the team (feature 061)', () => {
  function render(overrides: Partial<TeamPublicDetail>, relation: TeamViewerRelation = 'NonMember'): ComponentFixture<TeamDetailComponent> {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [TeamDetailComponent, translocoTestingModule()],
      providers: [
        provideRouter([]),
        ...translocoLocaleTestingProviders(),
        {
          provide: TeamService,
          useValue: {
            getPublicDetail: jest.fn().mockReturnValue(of({ ...detail(relation), ...overrides })),
            getMembers: jest.fn().mockReturnValue(of(page([]))),
            getNews: jest.fn().mockReturnValue(of(page([]))),
            getHappenings: jest.fn().mockReturnValue(of([])),
            getJoinRequests: jest.fn().mockReturnValue(of(page([]))),
            logoUrl: jest.fn().mockReturnValue('/api/v1/teams/rheinfeuer/logo'),
          },
        },
        { provide: PartyService, useValue: { getTeamPartyRequests: jest.fn().mockReturnValue(of(page([]))) } },
        { provide: ResultsService, useValue: { getTeamPlacements: jest.fn().mockReturnValue(of(page([]))) } },
        { provide: AuthService, useValue: { currentUser: () => ({ id: ME }) } },
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

  it('shows the description to a player who is not on the team, as plain text with its line breaks', () => {
    const text = 'Gegründet 2019.\n\n<b>Stark</b> — mehr auf https://rheinfeuer.de';
    const fixture = render({ description: text });

    const paragraph = query(fixture, 'about-description');
    expect(query(fixture, 'about')).not.toBeNull();
    expect(paragraph?.textContent).toBe(text);
    // Never markup, never a link: the address in the text stays text (FR-014).
    expect(paragraph?.querySelector('b')).toBeNull();
    expect(paragraph?.querySelector('a')).toBeNull();
    expect(paragraph?.className).toContain('whitespace-pre-line');
  });

  it('draws nothing for a team with neither a description nor links', () => {
    const fixture = render({ description: null, links: [] }, 'Member');
    expect(query(fixture, 'about')).toBeNull();
  });

  it('lists the links in order, each opening outside the app beside the site it leads to', () => {
    const fixture = render({
      description: null,
      links: [
        { label: 'Website', url: 'https://www.rheinfeuer.de/' },
        { label: 'Instagram', url: 'https://instagram.com/rheinfeuer' },
      ],
    });

    const links = Array.from(fixture.nativeElement.querySelectorAll('[data-testid="about-link"]')) as HTMLAnchorElement[];
    expect(links.map((a) => a.getAttribute('href'))).toEqual(['https://www.rheinfeuer.de/', 'https://instagram.com/rheinfeuer']);
    expect(links.map((a) => a.textContent)).toEqual([expect.stringContaining('Website'), expect.stringContaining('Instagram')]);
    for (const a of links) {
      expect(a.getAttribute('target')).toBe('_blank');
      expect(a.getAttribute('rel')).toBe('noopener noreferrer nofollow ugc');
      expect(a.textContent).toContain('(opens in a new tab)');
    }
    const hosts = Array.from(fixture.nativeElement.querySelectorAll('[data-testid="about-link-host"]')) as HTMLElement[];
    expect(hosts.map((h) => h.textContent)).toEqual(['rheinfeuer.de', 'instagram.com']);
    // Links without a description: no empty paragraph above them.
    expect(query(fixture, 'about-description')).toBeNull();
  });

  it('shows a label that says one site beside the site the link really goes to', () => {
    const fixture = render({ description: null, links: [{ label: 'Instagram', url: 'https://іnstagram.com/x' }] });
    expect(query(fixture, 'about-link-host')?.textContent?.startsWith('xn--')).toBe(true);
  });
});

/**
 * Feature 064 (GH #385) — Remove in the roster menu asks first. What only the page can get wrong:
 * nothing is removed until the admin confirms, the safe answer is where focus lands, a second press
 * cannot remove twice, and every failure is told in our own words, never the server's.
 */
describe('TeamDetailComponent — removing a teammate asks first (feature 064)', () => {
  let service: Record<string, jest.Mock>;

  function render(
    paramMap: Observable<ParamMap> = of(convertToParamMap({ slug: 'rheinfeuer' })),
  ): ComponentFixture<TeamDetailComponent> {
    TestBed.resetTestingModule();
    service = {
      getPublicDetail: jest.fn().mockReturnValue(of(detail('Admin'))),
      getMembers: jest.fn().mockReturnValue(of(page([member(ME, 'Admin'), member(OTHER, 'Member')]))),
      getNews: jest.fn().mockReturnValue(of(page([]))),
      getHappenings: jest.fn().mockReturnValue(of([])),
      getJoinRequests: jest.fn().mockReturnValue(of(page([]))),
      logoUrl: jest.fn().mockReturnValue('/api/v1/teams/rheinfeuer/logo'),
      removeMember: jest.fn(),
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
        { provide: ChatService, useValue: { openTeamChat: jest.fn() } },
        { provide: PollService, useValue: { list: jest.fn().mockReturnValue(of(page([]))) } },
        { provide: ActivatedRoute, useValue: { paramMap } },
      ],
    });
    const fixture = TestBed.createComponent(TeamDetailComponent);
    fixture.detectChanges();
    return fixture;
  }

  const el = <T extends HTMLElement = HTMLElement>(fixture: ComponentFixture<TeamDetailComponent>, selector: string): T | null =>
    fixture.nativeElement.querySelector(selector);

  function click(fixture: ComponentFixture<TeamDetailComponent>, selector: string): void {
    const target = el(fixture, selector);
    if (!target) {
      throw new Error(`Nothing matches ${selector}`);
    }
    target.click();
    fixture.detectChanges();
  }

  function askToRemove(fixture: ComponentFixture<TeamDetailComponent>): void {
    click(fixture, `[data-member-menu="${OTHER}"]`);
    click(fixture, '[data-testid="remove-member"]');
  }

  const dialog = (fixture: ComponentFixture<TeamDetailComponent>) => el(fixture, '[data-testid="confirm-dialog"]');
  const failure = (status: number) =>
    throwError(() => new HttpErrorResponse({ status, error: { detail: 'Server wording, never shown' } }));

  it('opens the question naming the teammate and the team, and removes nobody yet', () => {
    const fixture = render();

    askToRemove(fixture);

    expect(dialog(fixture)?.textContent).toContain('Remove Player 2 from Rheinfeuer?');
    expect(dialog(fixture)?.textContent).toContain("We'll let them know.");
    expect(el(fixture, '[data-testid="confirm-dialog-keep"]')?.textContent?.trim()).toBe('Keep Player 2');
    expect(document.activeElement).toBe(el(fixture, '[data-testid="confirm-dialog-keep"]'));
    expect(service['removeMember']).not.toHaveBeenCalled();
    // The menu that asked is closed behind the dialog.
    expect(el(fixture, '[data-testid="remove-member"]')).toBeNull();
  });

  it('keeps the teammate on Keep and on Escape, and hands focus back to their menu button', () => {
    const fixture = render();

    askToRemove(fixture);
    click(fixture, '[data-testid="confirm-dialog-keep"]');
    expect(dialog(fixture)).toBeNull();
    expect(document.activeElement).toBe(el(fixture, `[data-member-menu="${OTHER}"]`));

    askToRemove(fixture);
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();
    expect(dialog(fixture)).toBeNull();

    expect(service['removeMember']).not.toHaveBeenCalled();
  });

  it('removes the teammate once on confirm, then shows the team as it now is', () => {
    const fixture = render();
    service['removeMember'].mockReturnValue(of(undefined));
    // The reload answers later, as it does over a network: until then the page is the loading line,
    // and focus must wait for the roster rather than fire into it (CodeRabbit on PR #394).
    const reload = new Subject<TeamPublicDetail>();
    service['getPublicDetail'].mockReturnValueOnce(reload);

    askToRemove(fixture);
    click(fixture, '[data-testid="confirm-dialog-confirm"]');
    expect(el(fixture, '#team-roster-heading')).toBeNull();
    reload.next(detail('Admin'));
    fixture.detectChanges();

    expect(service['removeMember']).toHaveBeenCalledTimes(1);
    expect(service['removeMember']).toHaveBeenCalledWith('rheinfeuer', OTHER);
    expect(dialog(fixture)).toBeNull();
    expect(service['getPublicDetail']).toHaveBeenCalledTimes(2);
    // Focus lands on the reloaded roster, not on the loading line that stood in for it.
    expect(document.activeElement).toBe(el(fixture, '#team-roster-heading'));
  });

  it('takes no second press while the removal is on its way', () => {
    const fixture = render();
    const pending = new Subject<void>();
    service['removeMember'].mockReturnValue(pending);

    askToRemove(fixture);
    click(fixture, '[data-testid="confirm-dialog-confirm"]');
    const confirm = el<HTMLButtonElement>(fixture, '[data-testid="confirm-dialog-confirm"]');
    confirm?.click();

    expect(service['removeMember']).toHaveBeenCalledTimes(1);
    expect(confirm?.disabled).toBe(true);
    expect(confirm?.textContent?.trim()).toBe('Removing…');
  });

  it('says the player is no longer on the team when they already left, and reloads', () => {
    const fixture = render();
    service['removeMember'].mockReturnValue(failure(404));

    askToRemove(fixture);
    click(fixture, '[data-testid="confirm-dialog-confirm"]');

    expect(dialog(fixture)).toBeNull();
    expect(el(fixture, '[data-testid="remove-notice"]')?.textContent).toContain('Player 2 is no longer on the team.');
    expect(document.activeElement).toBe(el(fixture, '[data-testid="remove-notice"]'));
    expect(fixture.nativeElement.textContent).not.toContain('Server wording');
    expect(service['getPublicDetail']).toHaveBeenCalledTimes(2);
  });

  it('says the viewer is no longer an admin when the server refuses, and reloads', () => {
    const fixture = render();
    service['removeMember'].mockReturnValue(failure(403));

    askToRemove(fixture);
    click(fixture, '[data-testid="confirm-dialog-confirm"]');

    expect(dialog(fixture)).toBeNull();
    expect(el(fixture, '[data-testid="remove-notice"]')?.textContent).toContain("You're no longer an admin of this team.");
    expect(service['getPublicDetail']).toHaveBeenCalledTimes(2);
  });

  it('drops a pending question when the page moves to another team, so it cannot remove anyone there', () => {
    // CodeRabbit on PR #394: the component is reused from team to team.
    const route = new BehaviorSubject<ParamMap>(convertToParamMap({ slug: 'rheinfeuer' }));
    const fixture = render(route);

    askToRemove(fixture);
    expect(dialog(fixture)).not.toBeNull();

    route.next(convertToParamMap({ slug: 'nordlicht' }));
    fixture.detectChanges();

    expect(dialog(fixture)).toBeNull();
    expect(service['removeMember']).not.toHaveBeenCalled();
  });

  it('drops an old answer even when the page has come back to the same team', () => {
    // CodeRabbit on PR #398: A → B → A. Comparing slugs cannot tell this from never having left, and
    // the old answer would close the question the page is now waiting on.
    const route = new BehaviorSubject<ParamMap>(convertToParamMap({ slug: 'rheinfeuer' }));
    const fixture = render(route);
    const first = new Subject<void>();
    const second = new Subject<void>();
    service['removeMember'].mockReturnValueOnce(first).mockReturnValueOnce(second);

    askToRemove(fixture);
    click(fixture, '[data-testid="confirm-dialog-confirm"]');
    route.next(convertToParamMap({ slug: 'nordlicht' }));
    route.next(convertToParamMap({ slug: 'rheinfeuer' }));
    fixture.detectChanges();
    askToRemove(fixture);
    click(fixture, '[data-testid="confirm-dialog-confirm"]');
    expect(service['removeMember']).toHaveBeenCalledTimes(2);
    const loads = service['getPublicDetail'].mock.calls.length;

    first.next();
    fixture.detectChanges();

    expect(dialog(fixture)).not.toBeNull();
    expect(el<HTMLButtonElement>(fixture, '[data-testid="confirm-dialog-confirm"]')?.disabled).toBe(true);
    expect(service['getPublicDetail']).toHaveBeenCalledTimes(loads);
  });

  it('keeps the question open on any other failure, in our words, and confirming again retries', () => {
    const fixture = render();
    service['removeMember'].mockReturnValueOnce(failure(500)).mockReturnValueOnce(of(undefined));

    askToRemove(fixture);
    click(fixture, '[data-testid="confirm-dialog-confirm"]');

    expect(dialog(fixture)).not.toBeNull();
    expect(el(fixture, '[data-testid="confirm-dialog-error"]')?.textContent).toContain("That didn't work.");
    expect(fixture.nativeElement.textContent).not.toContain('Server wording');

    click(fixture, '[data-testid="confirm-dialog-confirm"]');
    expect(service['removeMember']).toHaveBeenCalledTimes(2);
    expect(dialog(fixture)).toBeNull();
  });
});
