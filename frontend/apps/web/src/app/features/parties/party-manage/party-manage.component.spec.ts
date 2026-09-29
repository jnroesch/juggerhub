import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { Party } from '../../../core/models/party.models';
import { ChatService } from '../../../core/services/chat.service';
import { PartyService } from '../../../core/services/party.service';
import { translocoLocaleTestingProviders, translocoTestingModule } from '../../../../testing/transloco-testing';
import { PartyManageComponent } from './party-manage.component';

function party(overrides: Partial<Party> = {}): Party {
  return {
    id: 'party-1',
    eventId: 'event-1',
    eventName: 'Hamburg Cup',
    eventType: 'Tournament',
    startsAt: '2026-10-17T09:00:00Z',
    endsAt: '2026-10-18T18:00:00Z',
    teamId: 'team-1',
    teamSlug: 'rheinfeuer',
    teamName: 'Rheinfeuer',
    rosterCap: 8,
    inCount: 3,
    declinedCount: 1,
    noResponseCount: 2,
    isFull: false,
    status: 'Open',
    myState: 'In',
    myRole: 'Member',
    message: null,
    appliedGroup: null,
    readiness: { enoughToFieldTeam: false, spotsOpen: 5, unanswered: 2 },
    ...overrides,
  };
}

const page = <T>(items: T[]) => ({ items, totalCount: items.length, skip: 0, take: 50 });

/**
 * Feature 063 (GH #382) — the party page's way into the party chat. The server decides who may open
 * the chat; these pin who the page offers it to (the crew, in the viewer's own card), that it asks only
 * on the press, and how it answers.
 */
describe('PartyManageComponent — the party chat (feature 063)', () => {
  let parties: Record<string, jest.Mock>;
  let chat: { openPartyChat: jest.Mock };
  let navigate: jest.SpyInstance;

  function render(...views: Partial<Party>[]): ComponentFixture<PartyManageComponent> {
    TestBed.resetTestingModule();
    const getParty = jest.fn();
    for (const v of views) {
      getParty.mockReturnValueOnce(of(party(v)));
    }
    getParty.mockReturnValue(of(party(views[views.length - 1])));
    parties = {
      getParty,
      listMembers: jest.fn().mockReturnValue(of(page([]))),
      listNews: jest.fn().mockReturnValue(of(page([]))),
      join: jest.fn().mockReturnValue(of({})),
    };
    chat = { openPartyChat: jest.fn() };
    TestBed.configureTestingModule({
      imports: [PartyManageComponent, translocoTestingModule()],
      providers: [
        provideRouter([]),
        ...translocoLocaleTestingProviders(),
        { provide: PartyService, useValue: parties },
        { provide: ChatService, useValue: chat },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'party-1' }) } } },
      ],
    });
    navigate = jest.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(PartyManageComponent);
    fixture.detectChanges();
    return fixture;
  }

  const el = (fixture: ComponentFixture<PartyManageComponent>, selector: string) =>
    fixture.nativeElement.querySelector(selector) as HTMLElement | null;

  const textOf = (fixture: ComponentFixture<PartyManageComponent>, testId: string) =>
    el(fixture, `[data-testid="${testId}"]`)?.textContent?.trim();

  /** The test ids of the buttons in a card, in order. */
  const buttonsIn = (fixture: ComponentFixture<PartyManageComponent>, card: string) =>
    Array.from(fixture.nativeElement.querySelectorAll(`[data-testid="${card}"] button`) as NodeListOf<HTMLElement>).map(
      (b) => b.getAttribute('data-testid'),
    );

  function press(fixture: ComponentFixture<PartyManageComponent>, testId: string): void {
    const target = el(fixture, `[data-testid="${testId}"]`);
    if (!target) {
      throw new Error(`Nothing matches ${testId}`);
    }
    target.click();
    fixture.detectChanges();
  }

  // --- US1: a crew member ----------------------------------------------------------

  it('offers a crew member the party chat in their own card, before Leave', () => {
    const fixture = render({ myState: 'In', myRole: 'Member' });

    expect(buttonsIn(fixture, 'crew-card')).toEqual(['party-chat', 'leave-party']);
    expect(textOf(fixture, 'party-chat')).toBe('Party chat');
  });

  it('asks nothing about the chat while the page loads', () => {
    render({ myState: 'In' });

    expect(chat.openPartyChat).not.toHaveBeenCalled();
  });

  it('opens the chat the server names, on the press', () => {
    const fixture = render({ myState: 'In' });
    chat.openPartyChat.mockReturnValue(of({ conversationId: 'c9' }));

    press(fixture, 'party-chat');

    expect(chat.openPartyChat).toHaveBeenCalledWith('party-1');
    expect(navigate).toHaveBeenCalledWith(['/chat', 'c9']);
  });

  it('shows that it is working and takes no second press', () => {
    const fixture = render({ myState: 'In' });
    chat.openPartyChat.mockReturnValue(new Subject());

    press(fixture, 'party-chat');
    press(fixture, 'party-chat');

    const button = el(fixture, '[data-testid="party-chat"]') as HTMLButtonElement;
    expect(button.disabled).toBe(true);
    expect(button.textContent?.trim()).toBe('Opening…');
    expect(chat.openPartyChat).toHaveBeenCalledTimes(1);
  });

  it('says so, and shows the page as it now is, when the player is no longer in the crew', () => {
    const fixture = render({ myState: 'In' }, { myState: 'NoResponse' });
    chat.openPartyChat.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 })));

    press(fixture, 'party-chat');

    expect(textOf(fixture, 'party-chat-notice')).toBe("You're no longer in this crew.");
    expect(parties['getParty']).toHaveBeenCalledTimes(2);
    expect(el(fixture, '[data-testid="party-chat"]')).toBeNull();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('reports any other failure in the card, in its own words, and lets the player try again', () => {
    const fixture = render({ myState: 'In' });
    chat.openPartyChat.mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 500, error: { detail: 'Server-side English' } })),
    );

    press(fixture, 'party-chat');

    expect(el(fixture, '[data-testid="crew-card"] [data-testid="party-chat-error"]')?.textContent?.trim()).toBe(
      "We couldn't open the party chat just now.",
    );
    expect((el(fixture, '[data-testid="party-chat"]') as HTMLButtonElement).disabled).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
  });

  // --- US2: a party admin -------------------------------------------------------

  const admin: Partial<Party> = { myState: 'Admin', myRole: 'Admin' };

  it('offers a party admin the party chat beside Apply, as a secondary action', () => {
    const fixture = render({ ...admin, status: 'Open' });

    expect(buttonsIn(fixture, 'readiness-card')).toEqual(['apply-to-event', 'party-chat']);
    // Apply keeps the coral; Party chat never takes it (one coral CTA per view, DESIGN.md).
    expect(el(fixture, '[data-testid="apply-to-event"]')?.classList).toContain('bg-brand-strong');
    expect(el(fixture, '[data-testid="party-chat"]')?.classList).not.toContain('bg-brand-strong');
  });

  it('keeps it beside Withdraw once the party has applied', () => {
    const fixture = render({ ...admin, status: 'Applied', appliedGroup: 'Joined' });

    expect(buttonsIn(fixture, 'readiness-card')).toEqual(['withdraw-from-event', 'party-chat']);
  });

  it('shows an admin one Party chat, in the readiness card only', () => {
    const fixture = render(admin);

    expect(el(fixture, '[data-testid="crew-card"]')).toBeNull();
    expect(fixture.nativeElement.querySelectorAll('[data-testid="party-chat"]').length).toBe(1);
  });

  it("opens the chat from the admin's card too", () => {
    const fixture = render(admin);
    chat.openPartyChat.mockReturnValue(of({ conversationId: 'c9' }));

    press(fixture, 'party-chat');

    expect(chat.openPartyChat).toHaveBeenCalledWith('party-1');
    expect(navigate).toHaveBeenCalledWith(['/chat', 'c9']);
  });

  it("reports a failure inside the admin's card", () => {
    const fixture = render(admin);
    chat.openPartyChat.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 503 })));

    press(fixture, 'party-chat');

    expect(el(fixture, '[data-testid="readiness-card"] [data-testid="party-chat-error"]')?.textContent?.trim()).toBe(
      "We couldn't open the party chat just now.",
    );
  });

  // --- US3: team members outside the crew see the page as before ------------------------

  /** The labels of the buttons in the viewer's own card. */
  const crewCardLabels = (fixture: ComponentFixture<PartyManageComponent>) =>
    Array.from(fixture.nativeElement.querySelectorAll('[data-testid="crew-card"] button') as NodeListOf<HTMLElement>).map(
      (b) => b.textContent?.trim(),
    );

  it('offers no party chat to a team member who has not answered — only the request', () => {
    const fixture = render({ myState: 'NoResponse', myRole: null });

    expect(el(fixture, '[data-testid="party-chat"]')).toBeNull();
    expect(crewCardLabels(fixture)).toEqual(["I'm in", "Can't make it"]);
  });

  it('offers no party chat to a team member who declined', () => {
    const fixture = render({ myState: 'Declined', myRole: null });

    expect(el(fixture, '[data-testid="party-chat"]')).toBeNull();
    expect(crewCardLabels(fixture)).toEqual(["I'm in", "Can't make it"]);
  });

  it('offers no party chat when the party is full and the player is not in it', () => {
    const fixture = render({ myState: 'NoResponse', myRole: null, isFull: true });

    expect(el(fixture, '[data-testid="party-chat"]')).toBeNull();
    expect(crewCardLabels(fixture)).toEqual([]);
  });

  it('offers the party chat once the player says they are in', () => {
    const fixture = render({ myState: 'NoResponse', myRole: null }, { myState: 'In', myRole: 'Member' });

    const imIn = Array.from(fixture.nativeElement.querySelectorAll('[data-testid="crew-card"] button') as NodeListOf<HTMLElement>).find(
      (b) => b.textContent?.trim() === "I'm in",
    );
    imIn?.click();
    fixture.detectChanges();

    expect(parties['join']).toHaveBeenCalledWith('party-1');
    expect(buttonsIn(fixture, 'crew-card')).toEqual(['party-chat', 'leave-party']);
  });
});

/** GH #388 — where the party's entry landed on the event reads as words, never the raw status. */
describe('PartyManageComponent — the applied status', () => {
  function render(overrides: Partial<Party>): ComponentFixture<PartyManageComponent> {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [PartyManageComponent, translocoTestingModule()],
      providers: [
        provideRouter([]),
        ...translocoLocaleTestingProviders(),
        {
          provide: PartyService,
          useValue: {
            getParty: jest.fn().mockReturnValue(of(party(overrides))),
            listMembers: jest.fn().mockReturnValue(of(page([]))),
            listNews: jest.fn().mockReturnValue(of(page([]))),
          },
        },
        { provide: ChatService, useValue: { openPartyChat: jest.fn() } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'party-1' }) } } },
      ],
    });
    const fixture = TestBed.createComponent(PartyManageComponent);
    fixture.detectChanges();
    return fixture;
  }

  const status = (fixture: ComponentFixture<PartyManageComponent>) =>
    (fixture.nativeElement.querySelector('[data-testid="applied-status"]') as HTMLElement | null)?.textContent?.trim();

  it.each([
    ['Joined', 'applied · confirmed'],
    ['AwaitingApproval', 'applied · awaiting approval'],
    ['Waitlisted', 'applied · on the waiting list'],
  ] as const)('reads %s as "%s"', (appliedGroup, expected) => {
    expect(status(render({ status: 'Applied', appliedGroup }))).toBe(expected);
  });

  it('says only "applied" when the entry has no status to show', () => {
    expect(status(render({ status: 'Applied', appliedGroup: null }))).toBe('applied');
  });

  it('shows no applied status before the party applies', () => {
    expect(status(render({ status: 'Open', appliedGroup: null }))).toBeUndefined();
  });
});
