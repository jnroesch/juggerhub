import { HttpErrorResponse } from '@angular/common/http';
import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { translocoLocaleTestingProviders, translocoTestingModule } from '../../../../../testing/transloco-testing';
import { TeamPoll } from '../../../../core/models/poll.models';
import { PollService } from '../../../../core/services/poll.service';
import { PollItemComponent, PollRemoval } from './poll-item.component';

function poll(overrides: Partial<TeamPoll> = {}): TeamPoll {
  return {
    id: 'p1',
    question: 'Thursday instead of Tuesday this week?',
    allowsMultiple: false,
    isAnonymous: false,
    resultsAfterAnswer: false,
    createdDate: new Date().toISOString(),
    closesAt: null,
    closedAt: null,
    isOpen: true,
    authorName: 'Ada',
    authorHandle: 'ada',
    memberCount: 5,
    answeredCount: 2,
    resultsVisible: true,
    hasAnswers: true,
    myOptionIds: [],
    options: [
      { id: 'o1', text: 'Thursday works', count: 1, voters: [{ name: 'Ben', handle: 'ben' }] },
      { id: 'o2', text: 'Stay on Tuesday', count: 1, voters: [{ name: 'Cleo', handle: 'cleo' }] },
    ],
    notAnswered: null,
    ...overrides,
  };
}

@Component({
  imports: [PollItemComponent],
  template: `<jh-poll-item [poll]="poll()" slug="rheinfeuer" [isAdmin]="isAdmin()"
    (changed)="changed.push($event); poll.set($event)" (removed)="removed.push($event)" (stale)="stale = stale + 1" />`,
})
class HostComponent {
  readonly poll = signal<TeamPoll>(poll());
  readonly isAdmin = signal(false);
  readonly changed: TeamPoll[] = [];
  readonly removed: PollRemoval[] = [];
  stale = 0;
}

/**
 * Feature 062 — one poll: answering it, what it shows, and the admin's menu. Everything shown comes
 * from the server's DTO; these pin that the component shows exactly that and nothing it was not sent.
 */
describe('PollItemComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;
  let api: jest.Mocked<Pick<PollService, 'answer' | 'withdraw' | 'close' | 'remove' | 'update' | 'create'>>;

  beforeEach(() => {
    api = {
      answer: jest.fn(),
      withdraw: jest.fn(),
      close: jest.fn(),
      remove: jest.fn(),
      update: jest.fn(),
      create: jest.fn(),
    };
    TestBed.configureTestingModule({
      imports: [HostComponent, translocoTestingModule()],
      providers: [provideRouter([]), { provide: PollService, useValue: api }, ...translocoLocaleTestingProviders()],
    });
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
  });

  const all = (selector: string): HTMLElement[] => Array.from(fixture.nativeElement.querySelectorAll(selector));
  const el = (selector: string): HTMLElement | null => fixture.nativeElement.querySelector(selector);
  function click(target: HTMLElement | null): void {
    if (!target) throw new Error('Nothing to click');
    target.click();
    fixture.detectChanges();
  }
  function set(p: TeamPoll): void {
    host.poll.set(p);
    fixture.detectChanges();
  }

  it('anchors the question for links to the poll and shows it as plain text', () => {
    set(poll({ question: '<b>bold?</b>' }));
    const heading = el('#poll-p1');
    expect(heading?.textContent?.trim()).toBe('<b>bold?</b>');
    expect(heading?.querySelector('b')).toBeNull();
    expect(heading?.getAttribute('tabindex')).toBe('-1');
  });

  it('records a one-answer poll on a single press', () => {
    api.answer.mockReturnValue(of(poll({ myOptionIds: ['o2'] })));

    click(all('[data-testid="poll-option"]')[1]);

    expect(api.answer).toHaveBeenCalledWith('rheinfeuer', 'p1', ['o2']);
    expect(host.changed).toHaveLength(1);
    const pressed = all('[data-testid="poll-option"]').map((b) => b.getAttribute('aria-pressed'));
    expect(pressed).toEqual(['false', 'true']);
  });

  it('does nothing when the answer already given is pressed again', () => {
    set(poll({ myOptionIds: ['o1'] }));
    click(all('[data-testid="poll-option"]')[0]);
    expect(api.answer).not.toHaveBeenCalled();
  });

  it('saves a several-answer poll with one press, and an empty save withdraws', () => {
    set(poll({ allowsMultiple: true }));
    api.answer.mockReturnValue(of(poll({ allowsMultiple: true, myOptionIds: ['o1', 'o2'] })));

    const save = el('[data-testid="poll-save-answer"]') as HTMLButtonElement;
    expect(save.disabled).toBe(true);
    click(all('[data-testid="poll-option"]')[0]);
    click(all('[data-testid="poll-option"]')[1]);
    expect(api.answer).not.toHaveBeenCalled();
    click(el('[data-testid="poll-save-answer"]'));
    expect(api.answer).toHaveBeenCalledWith('rheinfeuer', 'p1', ['o1', 'o2']);

    api.withdraw.mockReturnValue(of(poll({ allowsMultiple: true, myOptionIds: [] })));
    click(all('[data-testid="poll-option"]')[0]);
    click(all('[data-testid="poll-option"]')[1]);
    click(el('[data-testid="poll-save-answer"]'));
    expect(api.withdraw).toHaveBeenCalledWith('rheinfeuer', 'p1');
  });

  it('shows counts and names only where the server sent them', () => {
    expect(all('[data-testid="poll-count"]')).toHaveLength(2);
    expect(all('[data-testid="poll-voters"]').map((v) => v.textContent)).toEqual([
      expect.stringContaining('Ben'),
      expect.stringContaining('Cleo'),
    ]);

    // Hidden until you answer: no counts, no names, no bars — and the reason.
    set(poll({ resultsVisible: false, options: [
      { id: 'o1', text: 'Thursday works', count: null, voters: null },
      { id: 'o2', text: 'Stay on Tuesday', count: null, voters: null },
    ] }));
    expect(all('[data-testid="poll-count"]')).toHaveLength(0);
    expect(all('[data-testid="poll-voters"]')).toHaveLength(0);
    expect(el('[data-testid="poll-results-hidden"]')).not.toBeNull();
    expect(el('[data-testid="poll-answered"]')?.textContent).toContain('2 of 5 answered');
  });

  it('tells an anonymous poll apart before anyone answers', () => {
    set(poll({ isAnonymous: true, options: [
      { id: 'o1', text: 'Black', count: 1, voters: null },
      { id: 'o2', text: 'Orange', count: 1, voters: null },
    ] }));
    expect(el('[data-testid="poll-anonymous-notice"]')?.textContent).toContain('Nobody sees who chose what.');
    expect(el('[data-testid="poll-chip-names"]')?.textContent).toContain('Anonymous');
    expect(all('[data-testid="poll-voters"]')).toHaveLength(0);
  });

  it('shows the not-answered line only when it was sent', () => {
    expect(el('[data-testid="poll-not-answered"]')).toBeNull();
    set(poll({ notAnswered: [{ name: 'Dora', handle: 'dora' }] }));
    expect(el('[data-testid="poll-not-answered"]')?.textContent).toContain('Dora');
  });

  it('asks the card to refresh when the poll closed meanwhile, and says so', () => {
    api.answer.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 409, error: { code: 'closed', detail: 'English' } })));

    click(all('[data-testid="poll-option"]')[0]);

    expect(host.stale).toBe(1);
    expect(el('[data-testid="poll-error"]')?.textContent).toContain('This poll has closed.');
    expect(el('[data-testid="poll-error"]')?.textContent).not.toContain('English');
  });

  it('reports a poll that no longer exists as gone', () => {
    api.answer.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 })));
    click(all('[data-testid="poll-option"]')[0]);
    expect(host.removed).toEqual([{ gone: true }]);
  });

  it('renders a closed poll as results, not buttons', () => {
    set(poll({ isOpen: false, closedAt: new Date().toISOString() }));
    expect(all('[data-testid="poll-option"]')).toHaveLength(0);
    expect(all('[data-testid="poll-option-result"]')).toHaveLength(2);
    expect(el('[data-testid="poll-chip-closed"]')).not.toBeNull();
    expect(el('[data-testid="poll-withdraw"]')).toBeNull();
  });

  describe('for an admin', () => {
    beforeEach(() => {
      host.isAdmin.set(true);
      fixture.detectChanges();
    });

    it('offers no menu to plain members', () => {
      host.isAdmin.set(false);
      fixture.detectChanges();
      expect(el('[data-testid="poll-menu-trigger"]')).toBeNull();
    });

    it('closes after confirming, with the safe answer first', () => {
      api.close.mockReturnValue(of(poll({ isOpen: false, closedAt: new Date().toISOString() })));

      click(el('[data-testid="poll-menu-trigger"]'));
      click(el('[data-testid="poll-close"]'));
      expect(el('[data-testid="poll-confirm"]')).not.toBeNull();
      expect(el('[data-testid="poll-confirm-keep"]')?.textContent).toContain('Keep it open');
      click(el('[data-testid="poll-confirm-submit"]'));

      expect(api.close).toHaveBeenCalledWith('rheinfeuer', 'p1');
      expect(host.changed.at(-1)?.isOpen).toBe(false);
      expect(el('[data-testid="poll-confirm"]')).toBeNull();
    });

    it('deletes after confirming with a destructive button', () => {
      api.remove.mockReturnValue(of(undefined));

      click(el('[data-testid="poll-menu-trigger"]'));
      click(el('[data-testid="poll-delete"]'));
      const submit = el('[data-testid="poll-confirm-submit"]');
      expect(submit?.textContent).toContain('Delete poll');
      click(submit);

      expect(api.remove).toHaveBeenCalledWith('rheinfeuer', 'p1');
      expect(host.removed).toEqual([{ gone: false }]);
    });

    it('keeps the dialog open with a message when deleting fails, and never retries on its own', () => {
      api.remove.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 500 })));

      click(el('[data-testid="poll-menu-trigger"]'));
      click(el('[data-testid="poll-delete"]'));
      click(el('[data-testid="poll-confirm-submit"]'));

      expect(api.remove).toHaveBeenCalledTimes(1);
      expect(el('[data-testid="poll-confirm-error"]')).not.toBeNull();
    });

    it('offers only delete on a closed poll', () => {
      set(poll({ isOpen: false, closedAt: new Date().toISOString() }));
      click(el('[data-testid="poll-menu-trigger"]'));
      expect(el('[data-testid="poll-edit"]')).toBeNull();
      expect(el('[data-testid="poll-close"]')).toBeNull();
      expect(el('[data-testid="poll-delete"]')).not.toBeNull();
    });

    it('opens the editor in place', () => {
      click(el('[data-testid="poll-menu-trigger"]'));
      click(el('[data-testid="poll-edit"]'));
      expect(el('[data-testid="poll-editor"]')).not.toBeNull();
      expect(el('#poll-p1')).toBeNull();
    });
  });
});
