import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { NeedsYouItem, NeedsYouKind } from '../../../core/models/home.models';
import { NeedsYouCardComponent } from './needs-you-card.component';
import { translocoTestingModule } from '../../../../testing/transloco-testing';

function item(kind: NeedsYouKind, partial: Partial<NeedsYouItem> = {}): NeedsYouItem {
  return {
    kind,
    id: 'x1',
    params: { teamName: 'Hamburg Hammers', teamSlug: null, eventName: 'Summer Slam', playerName: null },
    linkTarget: null,
    occurredAt: '2026-07-20T10:00:00Z',
    ...partial,
  };
}

describe('NeedsYouCardComponent', () => {
  let httpMock: HttpTestingController;

  function mount(items: NeedsYouItem[]): {
    fixture: ComponentFixture<NeedsYouCardComponent>;
    resolved: string[];
    gone: string[];
  } {
    const fixture = TestBed.createComponent(NeedsYouCardComponent);
    const resolved: string[] = [];
    const gone: string[] = [];
    fixture.componentRef.setInput('items', items);
    fixture.componentInstance.resolved.subscribe((id) => resolved.push(id));
    fixture.componentInstance.gone.subscribe((id) => gone.push(id));
    fixture.detectChanges();
    return { fixture, resolved, gone };
  }

  const root = (f: ComponentFixture<NeedsYouCardComponent>) =>
    f.nativeElement.querySelector('[data-testid="needs-you"]') as HTMLElement | null;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([])],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('renders nothing when there are no items', () => {
    const { fixture } = mount([]);
    expect(root(fixture)).toBeNull();
  });

  it('renders the block with a count when there are items', () => {
    const { fixture } = mount([item('TeamInvite'), item('MarketApplication', { id: 'x2' })]);
    expect(root(fixture)).toBeTruthy();
  });

  it('accepting a team invite POSTs to the token accept endpoint and emits resolved', () => {
    const { fixture, resolved } = mount([item('TeamInvite', { id: 'tok-abc' })]);
    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();
    const req = httpMock.expectOne('/api/v1/invitations/tok-abc/accept');
    expect(req.request.method).toBe('POST');
    req.flush({});
    fixture.detectChanges();
    expect(resolved).toEqual(['tok-abc']);
  });

  it('"I\'m in" on a party request POSTs to the party join endpoint', () => {
    const { fixture } = mount([item('PartyRequest', { id: 'party-1' })]);
    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();
    const req = httpMock.expectOne('/api/v1/parties/party-1/join');
    expect(req.request.method).toBe('POST');
    req.flush({});
  });

  it('a market application is shown as pending with no action buttons', () => {
    const { fixture } = mount([item('MarketApplication', { id: 'app-1' })]);
    expect(fixture.nativeElement.querySelector('button')).toBeNull();
    // "Pending", not "pending": the chip used to shout it through a CSS `uppercase`,
    // so the string was written lower-case. The chip is one text step now (GH #301).
    expect(root(fixture)!.textContent).toContain('Pending');
  });

  // --- Feature 058: every word is the client's; join requests for admins ------------------

  const heading = (f: ComponentFixture<NeedsYouCardComponent>) =>
    (f.nativeElement.querySelector('li a, li p') as HTMLElement).textContent?.trim();

  it.each([
    ['TeamInvite', 'Hamburg Hammers invited you'],
    ['PartyCoAdminInvite', 'Co-admin a party for Summer Slam'],
    ['PartyRequest', 'Hamburg Hammers is fielding a party'],
    ['MarketInvite', 'Hamburg Hammers want you'],
    ['MarketApplication', 'You applied to Hamburg Hammers'],
  ] as [NeedsYouKind, string][])('composes the %s headline from the names the server sent', (kind, expected) => {
    const { fixture } = mount([item(kind)]);
    expect(heading(fixture)).toBe(expected);
  });

  it('keeps the invitation context in words and the others as the event or team', () => {
    const { fixture } = mount([item('TeamInvite'), item('MarketInvite', { id: 'x2' })]);
    const lines = Array.from(fixture.nativeElement.querySelectorAll('li p.text-caption')).map((p) =>
      (p as HTMLElement).textContent?.replace(/\s+/g, ' ').trim(),
    );
    expect(lines[0]).toMatch(/^to join the team · /);
    expect(lines[1]).toMatch(/^Summer Slam · /);
  });

  // --- Feature 062: a poll waiting for the viewer's answer ------------------------------------------

  const teamPoll = () =>
    item('TeamPoll', {
      id: 'poll-1',
      params: { teamName: 'Hamburg Hammers', teamSlug: 'hamburg-hammers', eventName: null, playerName: null, question: 'Which jersey colour?' },
      linkTarget: 'hamburg-hammers',
    });

  it('headlines a poll with the team and its question and links to the poll itself', () => {
    const { fixture } = mount([teamPoll()]);
    const link = fixture.nativeElement.querySelector('li a') as HTMLAnchorElement;
    expect(link.textContent?.trim()).toBe('Hamburg Hammers asks: Which jersey colour?');
    expect(link.getAttribute('href')).toBe('/t/hamburg-hammers#poll-poll-1');
  });

  it('answers a poll on the team page, never from here', () => {
    const { fixture } = mount([teamPoll()]);
    const answer = fixture.nativeElement.querySelector('[data-testid="needs-you-answer-poll"]') as HTMLAnchorElement;
    expect(answer.textContent?.trim()).toBe('Answer');
    expect(answer.getAttribute('href')).toBe('/t/hamburg-hammers#poll-poll-1');
    expect(fixture.nativeElement.querySelectorAll('li[data-kind="TeamPoll"] button')).toHaveLength(0);
  });

  const joinRequest = (partial: Partial<NeedsYouItem> = {}) =>
    item('JoinRequest', {
      id: 'req-1',
      params: { teamName: 'Hamburg Hammers', teamSlug: 'hamburg-hammers', eventName: null, playerName: 'Jonas Weber' },
      linkTarget: 'jonas',
      ...partial,
    });

  it('names the player who wants to join, links to their profile, and names the team', () => {
    const { fixture } = mount([joinRequest()]);
    const link = fixture.nativeElement.querySelector('li a') as HTMLAnchorElement;
    expect(link.textContent?.trim()).toBe('Jonas Weber wants to join');
    expect(link.getAttribute('href')).toBe('/u/jonas');
    expect(fixture.nativeElement.querySelector('li p.text-caption').textContent).toContain('Hamburg Hammers ·');
  });

  it('approves a join request through the team endpoint and emits resolved', () => {
    const { fixture, resolved } = mount([joinRequest()]);
    const [approve] = Array.from(fixture.nativeElement.querySelectorAll('li button')) as HTMLButtonElement[];
    expect(approve.textContent?.trim()).toBe('Approve');
    approve.click();
    const req = httpMock.expectOne('/api/v1/teams/hamburg-hammers/join-requests/req-1/approve');
    expect(req.request.method).toBe('POST');
    req.flush(null);
    fixture.detectChanges();
    expect(resolved).toEqual(['req-1']);
  });

  it('declines a join request through the team endpoint', () => {
    const { fixture } = mount([joinRequest()]);
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('li button')) as HTMLButtonElement[];
    buttons[1].click();
    const req = httpMock.expectOne('/api/v1/teams/hamburg-hammers/join-requests/req-1/decline');
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });

  it('lets a request another admin already answered go at once, and tells the host why', () => {
    const { fixture, resolved, gone } = mount([joinRequest(), item('TeamInvite', { id: 'tok-1' })]);
    (fixture.nativeElement.querySelector('li[data-kind="JoinRequest"] button') as HTMLButtonElement).click();
    httpMock
      .expectOne('/api/v1/teams/hamburg-hammers/join-requests/req-1/approve')
      .flush(null, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('li[data-kind="JoinRequest"]')).toBeNull();
    expect(fixture.nativeElement.querySelectorAll('li').length).toBe(1);
    // The host explains it and refreshes — the explanation must outlive this card (feature 058).
    expect(gone).toEqual(['req-1']);
    expect(resolved).toEqual([]);
  });
});
