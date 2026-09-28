import { Component, ElementRef, Injector, afterNextRender, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { forkJoin, of } from 'rxjs';
import { AlertComponent, ButtonDirective, CardComponent, EmptyStateComponent, IconComponent, LoadingComponent } from '../../../../shared/ui';
import { TeamPoll } from '../../../../core/models/poll.models';
import { PollService } from '../../../../core/services/poll.service';
import { PollEditorComponent } from './poll-editor.component';
import { PollItemComponent, PollRemoval } from './poll-item.component';

/** Open polls are never more than ten (the cap), so one request brings them all. */
const OPEN_TAKE = 10;
/** Closed polls stay until deleted, so they come a few at a time. */
const CLOSED_PAGE = 5;

/**
 * The only fragments this card acts on: `poll-` and a poll id. Anything else in the address is left
 * alone — and nothing else can reach the selector the scroll builds from it.
 */
const POLL_ANCHOR = /^poll-[0-9a-f-]{1,64}$/i;

/**
 * The team's Polls card (feature 062): open polls first, newest first, then closed ones, most recently
 * closed first. Members only — the team page mounts it inside its member block, so a non-member's page
 * makes no request for it (spec SC-009); the server refuses them anyway.
 *
 * Admins start a poll here, inline. A notice, an email or a Home item links to a single poll as
 * `/t/{slug}#poll-{id}`; the card scrolls to it once its lists have arrived, which the router's own
 * anchor scrolling cannot do — it runs before the polls exist.
 */
@Component({
  selector: 'jh-team-polls',
  imports: [TranslocoPipe, AlertComponent, ButtonDirective, CardComponent, EmptyStateComponent, IconComponent, LoadingComponent, PollEditorComponent, PollItemComponent],
  templateUrl: './team-polls.component.html',
  styleUrl: './team-polls.component.css',
})
export class TeamPollsComponent {
  private readonly polls = inject(PollService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  readonly slug = input.required<string>();
  readonly isAdmin = input(false);

  protected readonly open = signal<TeamPoll[]>([]);
  protected readonly closed = signal<TeamPoll[]>([]);
  protected readonly closedTotal = signal(0);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly loadingOlder = signal(false);
  protected readonly creating = signal(false);
  /** A translation key: what happened to a poll the viewer acted on (e.g. deleted meanwhile). */
  protected readonly notice = signal<string | null>(null);

  protected readonly isEmpty = computed(() => !this.loading() && !this.loadError() && this.open().length === 0 && this.closed().length === 0);
  protected readonly hasOlder = computed(() => this.closed().length < this.closedTotal());

  /** The link's `#poll-{id}`, if any. A test route without a fragment stream simply has none. */
  private readonly fragment = toSignal(inject(ActivatedRoute).fragment ?? of(null), { initialValue: null });
  /** The fragment already scrolled to, so a reload of the lists does not yank the page back. */
  private scrolledTo: string | null = null;

  constructor() {
    effect(() => {
      const slug = this.slug();
      untracked(() => this.load(slug));
    });

    effect(() => {
      const fragment = this.fragment();
      const ready = !this.loading();
      if (ready && fragment && POLL_ANCHOR.test(fragment) && fragment !== this.scrolledTo) {
        untracked(() => this.scrollTo(fragment));
      }
    });
  }

  protected retry(): void {
    this.load(this.slug());
  }

  private load(slug: string, quiet = false): void {
    if (!quiet) this.loading.set(true);
    this.loadError.set(false);
    // Keep however many closed polls the viewer already opened, so a refresh does not fold them away.
    const closedTake = Math.max(CLOSED_PAGE, this.closed().length);
    forkJoin({
      open: this.polls.list(slug, 'open', 0, OPEN_TAKE),
      closed: this.polls.list(slug, 'closed', 0, closedTake),
    }).subscribe({
      next: ({ open, closed }) => {
        this.open.set(open.items);
        this.closed.set(closed.items);
        this.closedTotal.set(closed.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loadError.set(true);
        this.loading.set(false);
      },
    });
  }

  protected showOlder(): void {
    if (this.loadingOlder()) return;
    this.loadingOlder.set(true);
    this.polls.list(this.slug(), 'closed', this.closed().length, CLOSED_PAGE).subscribe({
      next: (page) => {
        const known = new Set(this.closed().map((p) => p.id));
        this.closed.update((current) => [...current, ...page.items.filter((p) => !known.has(p.id))]);
        this.closedTotal.set(page.totalCount);
        this.loadingOlder.set(false);
      },
      error: () => this.loadingOlder.set(false),
    });
  }

  // --- Starting a poll -------------------------------------------------------------------------------

  protected startCreating(): void {
    this.notice.set(null);
    this.creating.set(true);
  }

  protected onCreated(poll: TeamPoll): void {
    this.creating.set(false);
    this.open.update((current) => [poll, ...current.filter((p) => p.id !== poll.id)]);
    this.focusAfterRender(`#poll-${poll.id}`);
  }

  protected onCreateCancelled(): void {
    this.creating.set(false);
    this.focusAfterRender('[data-testid="polls-start"]');
  }

  // --- What a poll reports back -----------------------------------------------------------------------

  /** A poll changed: in place while open; moved to the top of the closed list once it closed. */
  protected onChanged(poll: TeamPoll): void {
    if (poll.isOpen) {
      this.open.update((list) => list.map((p) => (p.id === poll.id ? poll : p)));
      return;
    }
    const wasOpen = this.open().some((p) => p.id === poll.id);
    this.open.update((list) => list.filter((p) => p.id !== poll.id));
    if (wasOpen) {
      this.closed.update((list) => [poll, ...list.filter((p) => p.id !== poll.id)]);
      this.closedTotal.update((n) => n + 1);
      this.focusAfterRender(`#poll-${poll.id}`);
    } else {
      this.closed.update((list) => list.map((p) => (p.id === poll.id ? poll : p)));
    }
  }

  protected onRemoved(id: string, removal: PollRemoval): void {
    const wasClosed = this.closed().some((p) => p.id === id);
    this.open.update((list) => list.filter((p) => p.id !== id));
    this.closed.update((list) => list.filter((p) => p.id !== id));
    if (wasClosed) this.closedTotal.update((n) => Math.max(0, n - 1));
    this.notice.set(removal.gone ? 'teams.polls.gone' : null);
    // The poll's own controls went with it; focus lands on the card's heading, never on the page body.
    this.focusAfterRender('#team-polls-heading');
  }

  /** What a poll showed is out of date (closed or changed meanwhile): fetch what is true, quietly. */
  protected onStale(): void {
    this.load(this.slug(), true);
  }

  private scrollTo(fragment: string): void {
    this.scrolledTo = fragment;
    afterNextRender(
      () => {
        const target =
          this.host.nativeElement.querySelector<HTMLElement>(`#${fragment}`) ??
          this.host.nativeElement.querySelector<HTMLElement>('#team-polls-heading');
        target?.scrollIntoView?.({ block: 'start' });
        target?.focus({ preventScroll: true });
      },
      { injector: this.injector },
    );
  }

  private focusAfterRender(selector: string): void {
    afterNextRender(() => this.host.nativeElement.querySelector<HTMLElement>(selector)?.focus(), { injector: this.injector });
  }
}
