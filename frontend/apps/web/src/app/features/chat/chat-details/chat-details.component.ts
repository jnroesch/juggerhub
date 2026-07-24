import { Component, OnChanges, computed, inject, input, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { ButtonDirective } from '../../../shared/ui';
import { Router, RouterLink } from '@angular/router';
import { ChatService } from '../../../core/services/chat.service';
import { ChatMember, ConversationDetail } from '../../../core/models/chat.models';

/**
 * A conversation's details (feature 019, wireframe 9e): who is in it, what has been shared, and the
 * controls that apply to its kind.
 *
 * The controls are **server-decided**: `canLeave`/`canAddMembers` come from the API, so the UI never
 * offers what the server would refuse. A team or party chat gets mute and hide instead of leave —
 * membership follows the roster, so there is nothing to leave (FR-026).
 */
@Component({
  selector: 'jh-chat-details',
  imports: [RouterLink, NgTemplateOutlet, ButtonDirective],
  templateUrl: './chat-details.component.html',
  styleUrl: './chat-details.component.css',
})
export class ChatDetailsComponent implements OnChanges {
  readonly conversationId = input.required<string>();

  private readonly chat = inject(ChatService);
  private readonly router = inject(Router);

  protected readonly detail = signal<ConversationDetail | null>(null);
  protected readonly members = signal<ChatMember[]>([]);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly busy = signal(false);
  protected readonly confirmingLeave = signal(false);
  protected readonly confirmingBlock = signal(false);

  /** Blocking is a DM concept: there is no "block" in a team chat you both legitimately belong to. */
  protected readonly canBlock = computed(() => this.detail()?.kind === 'Direct');

  /** The other person in a DM — the one a block would apply to. */
  protected readonly otherMember = computed(() => this.members().find((m) => !m.isYou) ?? null);

  ngOnChanges(): void {
    this.load();
  }

  private load(): void {
    const id = this.conversationId();
    this.loading.set(true);
    this.failed.set(false);

    this.chat.getDetail(id).subscribe({
      next: (d) => {
        this.detail.set(d);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.failed.set(true);
      },
    });

    this.chat.getMembers(id).subscribe({
      next: (page) => this.members.set([...page.items]),
      error: () => undefined,
    });
  }

  protected toggleMute(): void {
    const d = this.detail();
    if (!d) {
      return;
    }

    const next = !d.isMuted;
    this.busy.set(true);
    this.chat.setState(d.id, { isMuted: next }).subscribe({
      next: () => {
        this.detail.update((x) => (x ? { ...x, isMuted: next } : x));
        this.busy.set(false);
      },
      error: () => this.busy.set(false),
    });
  }

  protected hide(): void {
    const d = this.detail();
    if (!d) {
      return;
    }

    this.busy.set(true);
    this.chat.setState(d.id, { isHidden: true }).subscribe({
      next: () => void this.router.navigate(['/chat']),
      error: () => this.busy.set(false),
    });
  }

  protected leave(): void {
    const d = this.detail();
    if (!d) {
      return;
    }

    this.busy.set(true);
    this.chat.leave(d.id).subscribe({
      next: () => void this.router.navigate(['/chat']),
      error: () => this.busy.set(false),
    });
  }

  protected block(): void {
    const other = this.otherMember();
    if (!other) {
      return;
    }

    this.busy.set(true);
    this.chat.block(other.userId).subscribe({
      next: () => {
        this.chat.loadInbox().subscribe({ error: () => undefined });
        void this.router.navigate(['/chat']);
      },
      error: () => this.busy.set(false),
    });
  }
}
