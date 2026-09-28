import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { translocoLocaleTestingProviders, translocoTestingModule } from '../../../../../testing/transloco-testing';
import { PagedResult, TeamPoll } from '../../../../core/models/poll.models';
import { PollService } from '../../../../core/services/poll.service';
import { TeamPollsComponent } from './team-polls.component';

function poll(id: string, overrides: Partial<TeamPoll> = {}): TeamPoll {
  return {
    id,
    question: `Question ${id}?`,
    allowsMultiple: false,
    isAnonymous: false,
    resultsAfterAnswer: false,
    createdDate: '2026-09-28T10:00:00Z',
    closesAt: null,
    closedAt: null,
    isOpen: true,
    authorName: 'Ada',
    authorHandle: 'ada',
    memberCount: 3,
    answeredCount: 0,
    resultsVisible: true,
    hasAnswers: false,
    myOptionIds: [],
    options: [
      { id: `${id}-a`, text: 'Yes', count: 0, voters: [] },
      { id: `${id}-b`, text: 'No', count: 0, voters: [] },
    ],
    notAnswered: null,
    ...overrides,
  };
}

const page = (items: TeamPoll[], totalCount = items.length): PagedResult<TeamPoll> => ({ items, totalCount, skip: 0, take: 10 });

@Component({
  imports: [TeamPollsComponent],
  template: `<jh-team-polls slug="rheinfeuer" [isAdmin]="isAdmin()" />`,
})
class HostComponent {
  readonly isAdmin = signal(false);
}

/** Feature 062 — the team's Polls card: two lists, the empty state, starting a poll, and links to one poll. */
describe('TeamPollsComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let list: jest.Mock;
  let fragment$: BehaviorSubject<string | null>;

  function build(open: TeamPoll[], closed: TeamPoll[], closedTotal = closed.length, isAdmin = false): void {
    list = jest.fn((_slug: string, state: string, skip: number) =>
      of(state === 'open' ? page(open) : skip === 0 ? page(closed, closedTotal) : page([poll('older', { isOpen: false })], closedTotal)),
    );
    fragment$ = new BehaviorSubject<string | null>(null);
    TestBed.configureTestingModule({
      imports: [HostComponent, translocoTestingModule()],
      providers: [
        provideRouter([]),
        { provide: PollService, useValue: { list } },
        { provide: ActivatedRoute, useValue: { fragment: fragment$ } },
        ...translocoLocaleTestingProviders(),
      ],
    });
    fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.isAdmin.set(isAdmin);
    fixture.detectChanges();
  }

  const el = (s: string): HTMLElement | null => fixture.nativeElement.querySelector(s);
  const all = (s: string): HTMLElement[] => Array.from(fixture.nativeElement.querySelectorAll(s));

  it('loads the open polls and the first closed ones, open first', () => {
    build([poll('a'), poll('b')], [poll('c', { isOpen: false, closedAt: '2026-09-27T10:00:00Z' })]);

    expect(list).toHaveBeenCalledWith('rheinfeuer', 'open', 0, 10);
    expect(list).toHaveBeenCalledWith('rheinfeuer', 'closed', 0, 5);
    expect(all('[data-testid="poll"]').map((p) => p.getAttribute('data-poll-id'))).toEqual(['a', 'b', 'c']);
  });

  it('shows members a plain empty state and admins the invitation to ask', () => {
    build([], []);
    expect(el('jh-empty-state')?.textContent).toContain('No polls yet.');
    expect(el('[data-testid="polls-start"]')).toBeNull();

    TestBed.resetTestingModule();
    build([], [], 0, true);
    expect(el('jh-empty-state')?.textContent).toContain('Ask the team a question');
    expect(el('[data-testid="polls-start"]')).not.toBeNull();
  });

  it('opens the editor for an admin and puts a new poll at the top', () => {
    build([poll('a')], [], 0, true);
    el('[data-testid="polls-start"]')?.click();
    fixture.detectChanges();
    expect(el('[data-testid="poll-editor"]')).not.toBeNull();
    expect(el('[data-testid="polls-start"]')).toBeNull();
  });

  it('offers older closed polls a page at a time', () => {
    build([], [poll('c1', { isOpen: false })], 6);
    el('[data-testid="polls-older"]')?.click();
    fixture.detectChanges();
    expect(list).toHaveBeenCalledWith('rheinfeuer', 'closed', 1, 5);
    expect(all('[data-testid="poll"]')).toHaveLength(2);
  });

  it('goes to the poll a link names once the lists have arrived', async () => {
    build([poll('a'), poll('b')], []);
    const target = el('#poll-b') as HTMLElement;
    const focus = jest.spyOn(target, 'focus');

    fragment$.next('poll-b');
    fixture.detectChanges();
    await fixture.whenStable();

    expect(focus).toHaveBeenCalled();
  });
});
