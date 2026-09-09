import { AfterViewChecked, Component, ElementRef, OnChanges, SimpleChanges, ViewChild, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LoadingComponent } from '../../../shared/ui';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ChatService } from '../../../core/services/chat.service';
import { ChatMessage, ConversationDetail } from '../../../core/models/chat.models';
import { injectLocale } from '../../../core/i18n/locale-format';

/**
 * One open conversation (feature 019, wireframe 9b/9c): the thread, the composer, live delivery,
 * typing, read receipts, and the "new messages" divider with its jump-to-latest pill.
 *
 * Your bubbles sit right in **coral** and theirs left in sand. The wireframe drew blue; DESIGN.md is
 * the source of truth and reserves blue for the `info` token, so coral wins — reported rather than
 * silently resolved (research §12).
 */
@Component({
  selector: 'jh-chat-conversation',
  imports: [FormsModule, RouterLink, LoadingComponent, TranslocoPipe],
  templateUrl: './chat-conversation.component.html',
  styleUrl: './chat-conversation.component.css',
})
export class ChatConversationComponent implements OnChanges, AfterViewChecked {
  /** The conversation to show. Set by the route (mobile) or the rail (desktop). */
  readonly conversationId = input.required<string>();

  private readonly chat = inject(ChatService);
  private readonly t = inject(TranslocoService);
  private readonly locale = injectLocale();

  @ViewChild('scroller') private scroller?: ElementRef<HTMLElement>;

  protected readonly messages = this.chat.messages;
  protected readonly hasMoreHistory = this.chat.hasMoreHistory;
  protected readonly loadingOlder = this.chat.loadingOlder;
  protected readonly typingHere = this.chat.typingHere;

  protected readonly detail = signal<ConversationDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly draft = signal('');
  protected readonly sending = signal(false);
  protected readonly sendError = signal<string | null>(null);

  /** How many messages have landed while the reader is scrolled away from the latest. */
  protected readonly newWhileAway = signal(0);
  /** The id the "new messages" divider sits above, frozen when the reader was last at the bottom. */
  protected readonly dividerBeforeId = signal<string | null>(null);

  protected readonly canSend = computed(
    () => this.draft().trim().length > 0 && !this.sending() && this.detail()?.state !== 'Archived',
  );

  protected readonly isArchived = computed(() => this.detail()?.state === 'Archived');

  /** Sender labels only make sense where there is more than one other person. */
  protected readonly showsSenderNames = computed(() => this.detail()?.kind !== 'Direct');

  private pinnedToBottom = true;
  private pendingScrollToBottom = false;
  /** The scroller height captured before a history page was requested; applied once it has rendered. */
  private pendingScrollAnchor: number | null = null;
  /** The newest message the thread held on the last pass — the anchor for "what has arrived since". */
  private lastLatestId: string | null = null;

  constructor() {
    // A message arriving while the reader is scrolled up must NOT yank them to the bottom (FR-021):
    // it drops in behind a divider and raises the jump pill instead.
    //
    // Tracked by the id of the newest message rather than by the length, because the thread also
    // grows at the *front* when the reader pages back through history — and older messages are not
    // new ones: counting them would raise "30 new messages" and plant a divider above the page the
    // reader just asked for.
    effect(() => {
      const all = this.messages();
      const latestId = all.length > 0 ? all[all.length - 1].id : null;
      const previousLatestId = this.lastLatestId;
      this.lastLatestId = latestId;

      // Nothing yet, a thread just opened, or only older history came in.
      if (previousLatestId === null || latestId === previousLatestId) {
        return;
      }

      const previousIndex = all.findIndex((m) => m.id === previousLatestId);
      const arrived = previousIndex >= 0 ? all.length - 1 - previousIndex : all.length;
      if (arrived <= 0) {
        return;
      }

      if (this.pinnedToBottom) {
        this.pendingScrollToBottom = true;
      } else {
        this.newWhileAway.update((n) => n + arrived);
        this.dividerBeforeId.update((id) => id ?? all[previousIndex + 1]?.id ?? null);
      }
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['conversationId']) {
      this.open();
    }
  }

  ngAfterViewChecked(): void {
    if (this.pendingScrollToBottom) {
      this.pendingScrollToBottom = false;
      this.scrollToBottom();
    }

    if (this.pendingScrollAnchor !== null) {
      const previousHeight = this.pendingScrollAnchor;
      this.pendingScrollAnchor = null;
      const el = this.scroller?.nativeElement;
      if (el) {
        el.scrollTop = el.scrollHeight - previousHeight;
      }
    }
  }

  private open(): void {
    const id = this.conversationId();
    this.loading.set(true);
    this.failed.set(false);
    this.resetDivider();
    this.lastLatestId = null;
    this.pendingScrollAnchor = null;
    this.pinnedToBottom = true;

    this.chat.getDetail(id).subscribe({
      next: (d) => this.detail.set(d),
      error: () => this.failed.set(true),
    });

    this.chat.openConversation(id).subscribe({
      next: () => {
        this.loading.set(false);
        this.pendingScrollToBottom = true;
      },
      error: () => {
        this.loading.set(false);
        this.failed.set(true);
      },
    });
  }

  protected onScroll(): void {
    const el = this.scroller?.nativeElement;
    if (!el) {
      return;
    }

    // "At the bottom" with a little slack, so a pixel of overscroll doesn't unpin the thread.
    const atBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 40;
    this.pinnedToBottom = atBottom;

    if (atBottom) {
      this.resetDivider();
      this.chat.markReadToLatest(this.conversationId());
    }

    // Reaching the top pages further back. The service refuses a second page while one is in flight
    // (GH #220); asking here as well keeps the pointless subscriptions from being created at all.
    if (el.scrollTop < 80 && this.hasMoreHistory() && !this.loadingOlder()) {
      const previousHeight = el.scrollHeight;
      this.chat.loadOlder(this.conversationId()).subscribe({
        // Keep the reader's eye where it was: prepending content would otherwise jump the view. The
        // measurement has to wait for the prepended rows to be in the DOM, and the app is zoneless —
        // rendering is scheduled on a rAF/timeout race, so a microtask still sees the OLD height and
        // would park the reader at scrollTop 0, straight back on the trigger.
        next: () => (this.pendingScrollAnchor = previousHeight),
        error: () => undefined,
      });
    }
  }

  protected jumpToLatest(): void {
    this.resetDivider();
    this.pinnedToBottom = true;
    this.scrollToBottom();
    this.chat.markReadToLatest(this.conversationId());
  }

  private resetDivider(): void {
    this.newWhileAway.set(0);
    this.dividerBeforeId.set(null);
  }

  private scrollToBottom(): void {
    const el = this.scroller?.nativeElement;
    if (el) {
      el.scrollTop = el.scrollHeight;
    }
  }

  protected onDraftChange(value: string): void {
    this.draft.set(value);
    if (value.trim().length > 0 && !this.isArchived()) {
      this.chat.signalTyping(this.conversationId());
    }
  }

  protected send(): void {
    if (!this.canSend()) {
      return;
    }

    const body = this.draft().trim();
    this.sending.set(true);
    this.sendError.set(null);

    this.chat.send(this.conversationId(), body).subscribe({
      next: () => {
        this.draft.set('');
        this.sending.set(false);
        this.pendingScrollToBottom = true;
      },
      error: (e: { error?: { detail?: string } }) => {
        this.sending.set(false);
        this.sendError.set(e.error?.detail ?? "That message couldn't be sent.");
      },
    });
  }

  protected deleteMessage(messageId: string): void {
    this.chat.deleteMessage(messageId).subscribe({ error: () => undefined });
  }

  protected typingLabel(): string {
    const who = this.typingHere();
    if (who.length === 0) {
      return '';
    }

    return who.length === 1
      ? this.t.translate('chat.inbox.typingOne', { name: who[0].displayName })
      : this.t.translate('chat.inbox.typingSeveral');
  }

  protected messageTime(iso: string): string {
    return new Date(iso).toLocaleTimeString(this.locale(), { hour: '2-digit', minute: '2-digit' });
  }

  /** The wording for a system line. Rendered here so it stays consistent and translatable. */
  protected systemText(m: ChatMessage): string {
    const name = m.systemSubjectName ?? this.t.translate('chat.conversation.someone');
    switch (m.systemEvent) {
      case 'Joined':
        return this.t.translate('chat.conversation.systemJoined', { name });
      case 'Left':
        return this.t.translate('chat.conversation.systemLeft', { name });
      case 'Removed':
        return this.t.translate('chat.conversation.systemRemoved', { name });
      case 'GroupCreated':
        return this.t.translate('chat.conversation.systemGroupCreated', { name });
      default:
        return '';
    }
  }
}
