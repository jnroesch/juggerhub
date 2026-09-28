import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, Injector, OnInit, afterNextRender, computed, inject, input, output, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { ButtonDirective, IconComponent } from '../../../../shared/ui';
import { TeamPoll } from '../../../../core/models/poll.models';
import { PollService } from '../../../../core/services/poll.service';
import { toLocalInputValue, toUtcInstant } from '../../../../core/utils/poll-close-time';

/** The limits the server enforces (TeamPollRules). Mirrored only so the form can say so early. */
export const POLL_QUESTION_MAX = 200;
export const POLL_OPTION_MAX = 80;
export const POLL_MIN_OPTIONS = 2;
export const POLL_MAX_OPTIONS = 10;

interface OptionRow {
  /** Stable identity for the list, so removing a row never re-uses another row's input. */
  key: number;
  text: string;
}

let editorSeq = 0;

/**
 * Start a poll, or change one (feature 062). Inline in the Polls card, never a modal: on a phone the
 * keyboard covers a fixed sheet (feature 057's reason).
 *
 * In edit mode two things are fixed. Whether answers are anonymous is never offered — the request has
 * no field for it (spec FR-016). And once anyone has answered, only the closing time can change
 * (FR-023): the rest is shown, disabled, with the reason.
 *
 * Every refusal is shown from the server's `code`, never its English `detail` (GH #179). The form's own
 * checks only mirror the limits, for a quicker answer; the server decides.
 */
@Component({
  selector: 'jh-poll-editor',
  imports: [TranslocoPipe, ButtonDirective, IconComponent],
  templateUrl: './poll-editor.component.html',
  styleUrl: './poll-editor.component.css',
})
export class PollEditorComponent implements OnInit {
  private readonly polls = inject(PollService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  readonly mode = input<'create' | 'edit'>('create');
  readonly slug = input.required<string>();
  /** The poll being changed (edit mode). */
  readonly poll = input<TeamPoll | null>(null);

  readonly saved = output<TeamPoll>();
  readonly cancelled = output<void>();
  /** The poll changed underneath the form (answered or closed meanwhile): the card reloads. */
  readonly stale = output<void>();
  /** The poll no longer exists. */
  readonly gone = output<void>();

  protected readonly questionMax = POLL_QUESTION_MAX;
  protected readonly optionMax = POLL_OPTION_MAX;
  protected readonly ids = `poll-editor-${++editorSeq}`;

  private nextKey = 0;

  // Zoneless: everything the template reads to enable or disable something is a signal (045's lesson).
  protected readonly question = signal('');
  protected readonly options = signal<OptionRow[]>([this.row(), this.row()]);
  protected readonly allowsMultiple = signal(false);
  protected readonly isAnonymous = signal(false);
  protected readonly resultsAfterAnswer = signal(false);
  protected readonly closesAtLocal = signal('');
  /** Somebody has answered: only the closing time can change. */
  protected readonly locked = signal(false);
  protected readonly saving = signal(false);

  protected readonly formError = signal<string | null>(null);
  protected readonly questionError = signal<string | null>(null);
  protected readonly optionError = signal<{ index: number | null; key: string } | null>(null);
  protected readonly closesError = signal<string | null>(null);

  protected readonly canAddOption = computed(() => !this.locked() && this.options().length < POLL_MAX_OPTIONS);
  protected readonly canRemoveOption = computed(() => !this.locked() && this.options().length > POLL_MIN_OPTIONS);

  /** Nothing obviously missing: a question and no empty option. The server checks the rest. */
  protected readonly canSubmit = computed(
    () => !this.saving() && this.question().trim().length > 0 && this.options().every((o) => o.text.trim().length > 0),
  );

  ngOnInit(): void {
    const poll = this.poll();
    if (poll) {
      this.question.set(poll.question);
      this.options.set(poll.options.map((o) => this.row(o.text)));
      this.allowsMultiple.set(poll.allowsMultiple);
      this.isAnonymous.set(poll.isAnonymous);
      this.resultsAfterAnswer.set(poll.resultsAfterAnswer);
      this.closesAtLocal.set(toLocalInputValue(poll.closesAt));
      this.locked.set(poll.hasAnswers);
    }
    // Where typing starts: the question, or — when only the closing time can change — that field.
    afterNextRender(() => this.focus(this.locked() ? `#${this.ids}-closes` : `#${this.ids}-question`), { injector: this.injector });
  }

  protected setOption(index: number, text: string): void {
    this.options.update((rows) => rows.map((r, i) => (i === index ? { ...r, text } : r)));
    if (this.optionError()?.index === index) this.optionError.set(null);
  }

  protected addOption(): void {
    if (!this.canAddOption()) return;
    this.options.update((rows) => [...rows, this.row()]);
    const last = this.options().length - 1;
    afterNextRender(() => this.focus(`#${this.ids}-option-${last}`), { injector: this.injector });
  }

  protected removeOption(index: number): void {
    if (!this.canRemoveOption()) return;
    this.options.update((rows) => rows.filter((_, i) => i !== index));
    this.optionError.set(null);
  }

  protected clearCloses(): void {
    this.closesAtLocal.set('');
    this.closesError.set(null);
  }

  protected cancel(): void {
    this.cancelled.emit();
  }

  /** One press; never retried automatically (spec FR-038). */
  protected submit(): void {
    if (!this.canSubmit()) return;
    this.saving.set(true);
    this.clearErrors();

    const body = {
      question: this.question().trim(),
      options: this.options().map((o) => o.text.trim()),
      allowsMultiple: this.allowsMultiple(),
      resultsAfterAnswer: this.resultsAfterAnswer(),
      closesAt: toUtcInstant(this.closesAtLocal()),
    };
    const poll = this.poll();
    const call =
      this.mode() === 'edit' && poll
        ? this.polls.update(this.slug(), poll.id, body)
        : this.polls.create(this.slug(), { ...body, isAnonymous: this.isAnonymous() });

    call.subscribe({
      next: (result) => {
        this.saving.set(false);
        this.saved.emit(result);
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.showError(err);
      },
    });
  }

  private showError(err: unknown): void {
    if (err instanceof HttpErrorResponse && err.status === 404 && this.mode() === 'edit') {
      this.gone.emit();
      return;
    }
    const body = err instanceof HttpErrorResponse && err.error && typeof err.error === 'object' ? (err.error as { code?: unknown; option?: unknown }) : null;
    const code = typeof body?.code === 'string' ? body.code : null;
    const index = typeof body?.option === 'number' ? body.option : null;
    switch (code) {
      case 'question':
        this.questionError.set('teams.polls.error.question');
        break;
      case 'optionCount':
        this.optionError.set({ index: null, key: 'teams.polls.error.optionCount' });
        break;
      case 'optionLength':
      case 'optionDuplicate':
        this.optionError.set({ index, key: `teams.polls.error.${code}` });
        break;
      case 'closesAtPast':
      case 'closesAtTooFar':
        this.closesError.set(`teams.polls.error.${code}`);
        break;
      case 'tooManyOpen':
        this.formError.set('teams.polls.error.tooManyOpen');
        break;
      case 'answered':
        // Someone answered while the form was open: the content is locked now. Say so, keep what was
        // typed for the closing time, and let the card fetch what is true.
        this.locked.set(true);
        this.formError.set('teams.polls.error.answered');
        this.stale.emit();
        break;
      case 'closed':
        this.formError.set('teams.polls.error.closed');
        this.stale.emit();
        break;
      default:
        this.formError.set('teams.polls.error.generic');
    }
  }

  private clearErrors(): void {
    this.formError.set(null);
    this.questionError.set(null);
    this.optionError.set(null);
    this.closesError.set(null);
  }

  private row(text = ''): OptionRow {
    return { key: this.nextKey++, text };
  }

  private focus(selector: string): void {
    this.host.nativeElement.querySelector<HTMLElement>(selector)?.focus();
  }
}
