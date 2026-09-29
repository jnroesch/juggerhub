import { HttpErrorResponse } from '@angular/common/http';
import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { translocoTestingModule } from '../../../../../testing/transloco-testing';
import { TeamPoll } from '../../../../core/models/poll.models';
import { PollService } from '../../../../core/services/poll.service';
import { PollEditorComponent } from './poll-editor.component';

const existing: TeamPoll = {
  id: 'p1',
  question: 'Jersey colour?',
  allowsMultiple: false,
  isAnonymous: true,
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
    { id: 'o1', text: 'Black', count: 0, voters: null },
    { id: 'o2', text: 'Orange', count: 0, voters: null },
  ],
  notAnswered: null,
};

@Component({
  imports: [PollEditorComponent],
  template: `<jh-poll-editor [mode]="mode()" slug="rheinfeuer" [poll]="poll()"
    (saved)="saved.push($event)" (cancelled)="cancelled = cancelled + 1" (stale)="stale = stale + 1" />`,
})
class HostComponent {
  readonly mode = signal<'create' | 'edit'>('create');
  readonly poll = signal<TeamPoll | null>(null);
  readonly saved: TeamPoll[] = [];
  cancelled = 0;
  stale = 0;
}

/** Feature 062 — starting and changing a poll. The server decides; the form mirrors the limits and shows codes. */
describe('PollEditorComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;
  let api: { create: jest.Mock; update: jest.Mock };

  function build(mode: 'create' | 'edit', poll: TeamPoll | null = null): void {
    api = { create: jest.fn(), update: jest.fn() };
    TestBed.configureTestingModule({
      imports: [HostComponent, translocoTestingModule()],
      providers: [{ provide: PollService, useValue: api }],
    });
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    host.mode.set(mode);
    host.poll.set(poll);
    fixture.detectChanges();
  }

  const el = <T extends HTMLElement = HTMLElement>(s: string): T | null => fixture.nativeElement.querySelector(s);
  const all = (s: string): HTMLElement[] => Array.from(fixture.nativeElement.querySelectorAll(s));
  function type(input: HTMLInputElement | null, text: string): void {
    if (!input) throw new Error('No input');
    input.value = text;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }
  function click(target: HTMLElement | null): void {
    if (!target) throw new Error('Nothing to click');
    target.click();
    fixture.detectChanges();
  }
  function submit(): void {
    el('form')?.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  }

  it('starts with two option rows, adds up to ten and never goes below two', () => {
    build('create');
    expect(all('[data-testid="poll-editor-option"]')).toHaveLength(2);
    expect(el('[data-testid="poll-editor-option-remove"]')).toBeNull();

    for (let i = 0; i < 8; i++) click(el('[data-testid="poll-editor-add-option"]'));
    expect(all('[data-testid="poll-editor-option"]')).toHaveLength(10);
    expect(el<HTMLButtonElement>('[data-testid="poll-editor-add-option"]')?.disabled).toBe(true);

    click(all('[data-testid="poll-editor-option-remove"]')[0]);
    expect(all('[data-testid="poll-editor-option"]')).toHaveLength(9);
  });

  it('sends the poll the admin described, with the close time as an instant', () => {
    build('create');
    api.create.mockReturnValue(of(existing));
    type(el('[data-testid="poll-editor-question"]'), '  Jersey colour?  ');
    const [a, b] = all('[data-testid="poll-editor-option-input"]') as HTMLInputElement[];
    type(a, 'Black');
    type(b, 'Orange');
    click(el('[data-testid="poll-editor-anonymous"]'));
    click(el('[data-testid="poll-editor-results-after"]'));
    type(el('[data-testid="poll-editor-closes"]'), '2027-01-15T20:00');

    submit();

    expect(api.create).toHaveBeenCalledWith('rheinfeuer', {
      question: 'Jersey colour?',
      options: ['Black', 'Orange'],
      allowsMultiple: false,
      isAnonymous: true,
      resultsAfterAnswer: true,
      closesAt: new Date(2027, 0, 15, 20, 0).toISOString(),
    });
    expect(host.saved).toEqual([existing]);
  });

  it('cannot submit without a question or with an empty option', () => {
    build('create');
    const submitButton = el<HTMLButtonElement>('[data-testid="poll-editor-submit"]');
    expect(submitButton?.disabled).toBe(true);
    type(el('[data-testid="poll-editor-question"]'), 'Q?');
    type(all('[data-testid="poll-editor-option-input"]')[0] as HTMLInputElement, 'A');
    expect(submitButton?.disabled).toBe(true);
    type(all('[data-testid="poll-editor-option-input"]')[1] as HTMLInputElement, 'B');
    expect(submitButton?.disabled).toBe(false);
  });

  it('keeps the submit button secondary: the page has one coral call to action', () => {
    build('create');
    expect(el('[data-testid="poll-editor-submit"]')?.className).not.toContain('bg-brand-strong');
  });

  it('shows a refusal at the option it is about, in its own words', () => {
    build('create');
    api.create.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 400, error: { code: 'optionDuplicate', option: 1, detail: 'Two options are the same.' } })));
    type(el('[data-testid="poll-editor-question"]'), 'Colour?');
    type(all('[data-testid="poll-editor-option-input"]')[0] as HTMLInputElement, 'Rot');
    type(all('[data-testid="poll-editor-option-input"]')[1] as HTMLInputElement, 'rot');

    submit();

    const rows = all('[data-testid="poll-editor-option"]');
    expect(rows[0].querySelector('[data-testid="poll-editor-option-error"]')).toBeNull();
    expect(rows[1].querySelector('[data-testid="poll-editor-option-error"]')?.textContent).toContain('This option is the same as another one.');
  });

  it('never offers anonymity when changing a poll, and says why', () => {
    build('edit', existing);
    expect(el('[data-testid="poll-editor-anonymous"]')).toBeNull();
    expect(el('[data-testid="poll-editor-names-fixed"]')?.textContent).toContain('This poll is anonymous.');

    api.update.mockReturnValue(of(existing));
    submit();
    const body = api.update.mock.calls[0][2];
    expect(body).not.toHaveProperty('isAnonymous');
  });

  it('locks everything but the close time once someone has answered', () => {
    build('edit', { ...existing, hasAnswers: true });
    expect(el('[data-testid="poll-editor-locked"]')).not.toBeNull();
    expect(el<HTMLInputElement>('[data-testid="poll-editor-question"]')?.disabled).toBe(true);
    expect(el<HTMLButtonElement>('[data-testid="poll-editor-multiple"]')?.disabled).toBe(true);
    expect(el('[data-testid="poll-editor-add-option"]')).toBeNull();
    expect(el<HTMLInputElement>('[data-testid="poll-editor-closes"]')?.disabled).toBe(false);
  });

  it('locks itself when an answer arrived while it was open, and asks for a refresh', () => {
    build('edit', existing);
    api.update.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 409, error: { code: 'answered' } })));
    type(el('[data-testid="poll-editor-question"]'), 'Another question?');

    submit();

    expect(host.stale).toBe(1);
    expect(el('[data-testid="poll-editor-locked"]')).not.toBeNull();
    expect(el('[data-testid="poll-editor-error"]')?.textContent).toContain("Someone has answered");
  });
});
