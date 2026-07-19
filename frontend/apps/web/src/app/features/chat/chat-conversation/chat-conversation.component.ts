import {
  AfterViewChecked,
  Component,
  ElementRef,
  OnChanges,
  SimpleChanges,
  ViewChild,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ChatService } from '../../../core/services/chat.service';
import { ChatMessage, ConversationDetail } from '../../../core/models/chat.models';

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
  imports: [FormsModule, RouterLink],
  templateUrl: './chat-conversation.component.html',
  styleUrl: './chat-conversation.component.css',
})
export class ChatConversationComponent implements OnChanges, AfterViewChecked {
  /** The conversation to show. Set by the route (mobile) or the rail (desktop). */
  readonly conversationId = input.required<string>();

  private readonly chat = inject(ChatService);

  @ViewChild('scroller') private scroller?: ElementRef<HTMLElement>;

  protected readonly messages = this.chat.messages;
  protected readonly hasMoreHistory = this.chat.hasMoreHistory;
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
  private lastCount = 0;

  constructor() {
    // A message arriving while the reader is scrolled up must NOT yank them to the bottom (FR-021):
    // it drops in behind a divider and raises the jump pill instead.
    effect(() => {
      const count = this.messages().length;
      if (count > this.lastCount && this.lastCount > 0) {
        if (this.pinnedToBottom) {
          this.pendingScrollToBottom = true;
        } else {
          this.newWhileAway.update((n) => n + (count - this.lastCount));
          this.dividerBeforeId.update((id) => id ?? this.messages()[this.lastCount]?.id ?? null);
        }
      }
      this.lastCount = count;
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
  }

  private open(): void {
    const id = this.conversationId();
    this.loading.set(true);
    this.failed.set(false);
    this.resetDivider();
    this.lastCount = 0;
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

    // Reaching the top pages further back.
    if (el.scrollTop < 80 && this.hasMoreHistory()) {
      const previousHeight = el.scrollHeight;
      this.chat.loadOlder(this.conversationId()).subscribe({
        next: () => {
          // Keep the reader's eye where it was: prepending content would otherwise jump the view.
          queueMicrotask(() => (el.scrollTop = el.scrollHeight - previousHeight));
        },
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

    return who.length === 1 ? `${who[0].displayName} is typing…` : 'Several people are typing…';
  }

  protected messageTime(iso: string): string {
    return new Date(iso).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
  }

  /** The wording for a system line. Rendered here so it stays consistent and translatable. */
  protected systemText(m: ChatMessage): string {
    const who = m.systemSubjectName ?? 'Someone';
    switch (m.systemEvent) {
      case 'Joined':
        return `${who} joined the chat`;
      case 'Left':
        return `${who} left the chat`;
      case 'Removed':
        return `${who} was removed`;
      case 'GroupCreated':
        return `${who} started this group`;
      default:
        return '';
    }
  }
}
