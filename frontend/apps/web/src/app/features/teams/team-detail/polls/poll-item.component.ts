import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  DestroyRef,
  ElementRef,
  HostListener,
  Injector,
  afterNextRender,
  computed,
  inject,
  input,
  linkedSignal,
  output,
  signal,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { TranslocoDatePipe } from '@jsverse/transloco-locale';
import { Observable } from 'rxjs';
import { ButtonDirective, ChipDirective, IconComponent } from '../../../../shared/ui';
import { TeamPoll, TeamPollOption, TeamPollPerson } from '../../../../core/models/poll.models';
import { PollService } from '../../../../core/services/poll.service';
import { injectRelativeTime } from '../../../../core/i18n/locale-format';
import { PollEditorComponent } from './poll-editor.component';

/** What became of a poll the controls removed from view. */
export interface PollRemoval {
  /** True when it was already gone (another admin deleted it meanwhile): the card says so. */
  gone: boolean;
}

/**
 * One team poll (feature 062): the question, how to answer it, and — where the server allows — the
 * result. Admins also get a menu to change it (in place, through the editor), close it or delete it.
 *
 * Everything shown is what the server sent for this viewer. Whether answers are anonymous, whether the
 * result is visible yet, and who has not answered are decided there; this component never works any of
 * it out, so it cannot show more than it was given (spec FR-018).
 */
@Component({
  selector: 'jh-poll-item',
  imports: [NgTemplateOutlet, RouterLink, TranslocoPipe, TranslocoDatePipe, ButtonDirective, ChipDirective, IconComponent, PollEditorComponent],
  templateUrl: './poll-item.component.html',
  styleUrl: './poll-item.component.css',
})
export class PollItemComponent {
  private readonly polls = inject(PollService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  readonly poll = input.required<TeamPoll>();
  readonly slug = input.required<string>();
  readonly isAdmin = input(false);

  /** The poll as the server now has it, after this viewer changed it. */
  readonly changed = output<TeamPoll>();
  /** The poll is gone; the card takes it off its list and moves focus, since this component goes with it. */
  readonly removed = output<PollRemoval>();
  /** What this component shows is out of date (closed or changed meanwhile): the card reloads its lists. */
  readonly stale = output<void>();

  protected readonly rel = injectRelativeTime();

  protected readonly busy = signal(false);
  /** Translation keys, not text, so a language switch re-renders them. */
  protected readonly error = signal<string | null>(null);

  /**
   * A several-answer poll's choices before they are saved. Reset from the server's answer whenever the
   * poll changes, so a saved answer and the boxes on screen never disagree.
   */
  protected readonly selection = linkedSignal<TeamPoll, ReadonlySet<string>>({
    source: this.poll,
    computation: (poll) => new Set(poll.myOptionIds),
  });

  protected readonly answered = computed(() => this.poll().myOptionIds.length > 0);

  /** The selection differs from the saved answer, so Save has something to do. */
  protected readonly selectionChanged = computed(() => {
    const saved = this.poll().myOptionIds;
    const chosen = this.selection();
    return chosen.size !== saved.length || saved.some((id) => !chosen.has(id));
  });

  protected readonly menuOpen = signal(false);
  protected readonly editing = signal(false);
  protected readonly confirming = signal<'close' | 'delete' | null>(null);
  protected readonly confirmBusy = signal(false);
  protected readonly confirmError = signal<string | null>(null);

  /** Set once the poll has left the screen; an answer that arrives later must not reach for its DOM. */
  private destroyed = false;

  constructor() {
    inject(DestroyRef).onDestroy(() => (this.destroyed = true));
  }

  protected isChosen(option: TeamPollOption): boolean {
    return this.poll().allowsMultiple ? this.selection().has(option.id) : this.poll().myOptionIds.includes(option.id);
  }

  /** How much of the bar an option fills: its share of the members who answered. */
  protected share(option: TeamPollOption): number {
    const answered = this.poll().answeredCount;
    return option.count === null || answered === 0 ? 0 : Math.round((option.count / answered) * 100);
  }

  protected names(people: TeamPollPerson[]): TeamPollPerson[] {
    return people.filter((p) => !!p.handle);
  }

  // --- Answering -------------------------------------------------------------------------------------

  /**
   * One press on an option. A one-answer poll records it straight away (spec FR-036); pressing the
   * answer you already gave does nothing. A several-answer poll only toggles the box — Save sends it.
   */
  protected choose(option: TeamPollOption): void {
    const poll = this.poll();
    if (this.busy() || !poll.isOpen) return;
    if (poll.allowsMultiple) {
      this.selection.update((current) => {
        const next = new Set(current);
        if (next.has(option.id)) next.delete(option.id);
        else next.add(option.id);
        return next;
      });
      return;
    }
    if (poll.myOptionIds.includes(option.id)) return;
    this.send(this.polls.answer(this.slug(), poll.id, [option.id]));
  }

  /** Save a several-answer poll's choices. Clearing every box and saving is withdrawing (spec edge case). */
  protected saveSelection(): void {
    const chosen = [...this.selection()];
    this.send(chosen.length === 0 ? this.polls.withdraw(this.slug(), this.poll().id) : this.polls.answer(this.slug(), this.poll().id, chosen));
  }

  protected withdraw(): void {
    this.send(this.polls.withdraw(this.slug(), this.poll().id));
  }

  /** Never retried automatically: a failed press is retried by pressing again (spec FR-038). */
  private send(call: Observable<TeamPoll>): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    call.subscribe({
      next: (updated) => {
        this.busy.set(false);
        this.changed.emit(updated);
      },
      error: (err: unknown) => {
        this.busy.set(false);
        if (status(err) === 404) {
          this.removed.emit({ gone: true });
          return;
        }
        const code = problemCode(err);
        if (code === 'closed' || code === 'choiceUnknown') {
          // Closed meanwhile, or its options were changed under the viewer: show what is true now.
          this.stale.emit();
        }
        this.error.set(answerErrorKey(code));
      },
    });
  }

  // --- The admin menu ----------------------------------------------------------------------------------

  protected toggleMenu(): void {
    this.menuOpen.update((open) => !open);
  }

  protected startEdit(): void {
    this.menuOpen.set(false);
    this.error.set(null);
    this.editing.set(true);
  }

  protected onSaved(updated: TeamPoll): void {
    this.editing.set(false);
    this.changed.emit(updated);
    this.focusAfterRender(`#poll-${updated.id}`);
  }

  protected onCancelled(): void {
    this.editing.set(false);
    this.focusAfterRender('[data-poll-menu-trigger]');
  }

  protected onEditorStale(): void {
    this.stale.emit();
  }

  protected onEditorGone(): void {
    this.removed.emit({ gone: true });
  }

  protected ask(action: 'close' | 'delete'): void {
    this.menuOpen.set(false);
    this.confirmError.set(null);
    this.confirming.set(action);
    // The safe answer takes focus, so Enter on arrival changes nothing.
    this.focusAfterRender('[data-testid="poll-confirm-keep"]');
  }

  protected dismiss(): void {
    if (!this.confirming() || this.confirmBusy()) return;
    this.confirming.set(null);
    this.confirmError.set(null);
    this.focusAfterRender('[data-poll-menu-trigger]');
  }

  protected confirm(): void {
    const action = this.confirming();
    if (!action || this.confirmBusy()) return;
    this.confirmBusy.set(true);
    this.confirmError.set(null);
    const poll = this.poll();

    if (action === 'close') {
      this.polls.close(this.slug(), poll.id).subscribe({
        next: (updated) => {
          this.confirmBusy.set(false);
          this.confirming.set(null);
          this.changed.emit(updated);
        },
        error: (err: unknown) => {
          this.confirmBusy.set(false);
          if (status(err) === 404) {
            this.confirming.set(null);
            this.removed.emit({ gone: true });
            return;
          }
          if (problemCode(err) === 'closed') {
            // Another admin closed it, or its time ran out: nothing left to do but show it closed.
            this.confirming.set(null);
            this.error.set('teams.polls.error.alreadyClosed');
            this.stale.emit();
            return;
          }
          this.confirmError.set('teams.polls.error.generic');
        },
      });
      return;
    }

    this.polls.remove(this.slug(), poll.id).subscribe({
      next: () => {
        this.confirmBusy.set(false);
        this.confirming.set(null);
        // Last: the card drops the poll, and this component with it.
        this.removed.emit({ gone: false });
      },
      error: (err: unknown) => {
        this.confirmBusy.set(false);
        if (status(err) === 404) {
          this.confirming.set(null);
          this.removed.emit({ gone: true });
          return;
        }
        // The dialog stays open; confirming again is the retry (never automatic).
        this.confirmError.set('teams.polls.error.generic');
      },
    });
  }

  /** Keep Tab inside the open dialog: `aria-modal` promises that the page behind it is inert. */
  protected trapTab(event: Event): void {
    const key = event as KeyboardEvent;
    const buttons = Array.from((key.currentTarget as HTMLElement).querySelectorAll<HTMLElement>('button:not([disabled])'));
    if (buttons.length === 0) return;
    const first = buttons[0];
    const last = buttons[buttons.length - 1];
    if (key.shiftKey && document.activeElement === first) {
      last.focus();
      key.preventDefault();
    } else if (!key.shiftKey && document.activeElement === last) {
      first.focus();
      key.preventDefault();
    }
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    if (this.confirming()) {
      this.dismiss();
    }
    if (this.menuOpen()) {
      this.menuOpen.set(false);
      this.focus('[data-poll-menu-trigger]');
    }
  }

  /** A click outside THIS poll's menu closes it — including a click on another poll's menu button. */
  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: MouseEvent): void {
    if (!this.menuOpen()) return;
    const menu = this.host.nativeElement.querySelector('[data-poll-menu]');
    if (!menu?.contains(event.target as Node | null)) {
      this.menuOpen.set(false);
    }
  }

  /** Zoneless: what to focus exists only after the next render (GH #344's lesson). */
  private focusAfterRender(selector: string): void {
    if (!this.destroyed) {
      afterNextRender(() => this.focus(selector), { injector: this.injector });
    }
  }

  private focus(selector: string): void {
    this.host.nativeElement.querySelector<HTMLElement>(selector)?.focus();
  }
}

function status(err: unknown): number | null {
  return err instanceof HttpErrorResponse ? err.status : null;
}

/** The server's machine-readable reason (`extensions.code`) — never its English `detail` (GH #179). */
export function problemCode(err: unknown): string | null {
  if (err instanceof HttpErrorResponse && err.error && typeof err.error === 'object') {
    const code = (err.error as { code?: unknown }).code;
    return typeof code === 'string' ? code : null;
  }
  return null;
}

function answerErrorKey(code: string | null): string {
  switch (code) {
    case 'closed':
      return 'teams.polls.error.closed';
    case 'choiceUnknown':
      return 'teams.polls.error.choiceUnknown';
    case 'choiceCount':
      return 'teams.polls.error.choiceCount';
    default:
      return 'teams.polls.error.generic';
  }
}
