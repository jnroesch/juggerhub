import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { LoadingComponent } from '../../../shared/ui';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ChatService } from '../../../core/services/chat.service';

/**
 * Compose a NEW direct message (feature 022 — lazy DM creation). Reached at
 * `/chat/compose/:handle` from the profile Message action or the new-message picker when no
 * conversation with that player exists yet.
 *
 * Crucially this view persists **nothing**: no conversation, no inbox entry, no typing/read signals.
 * The direct conversation is created only when the first message is sent (`ChatService.sendDirect`),
 * after which we replace the URL with the real `/chat/:id`. Leaving without sending leaves no trace.
 */
@Component({
  selector: 'jh-chat-compose',
  imports: [FormsModule, RouterLink, LoadingComponent],
  templateUrl: './chat-compose.component.html',
  styleUrl: './chat-compose.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChatComposeComponent implements OnInit {
  /** The target player's public handle (from the route). */
  readonly handle = input.required<string>();

  private readonly chat = inject(ChatService);
  private readonly router = inject(Router);

  protected readonly displayName = signal('');
  protected readonly targetUserId = signal<string | null>(null);
  protected readonly resolving = signal(true);
  /** The player can't be messaged (blocked, or the account is gone). */
  protected readonly unavailable = signal(false);

  protected readonly draft = signal('');
  protected readonly sending = signal(false);
  protected readonly sendError = signal<string | null>(null);

  protected readonly canSend = computed(
    () => this.draft().trim().length > 0 && !this.sending() && this.targetUserId() !== null,
  );

  ngOnInit(): void {
    // The entry points pass the resolved person via navigation state to save a round trip.
    const state = history.state as { userId?: string; displayName?: string } | null;
    if (state?.userId) {
      this.targetUserId.set(state.userId);
      this.displayName.set(state.displayName ?? this.handle());
      this.resolving.set(false);
      return;
    }

    // Direct navigation / reload: resolve the target from the handle via chat search.
    const target = this.handle();
    this.chat.search(target).subscribe({
      next: (res) => {
        const hit = res.people.items.find((p) => (p.handle ?? '').toLowerCase() === target.toLowerCase());
        if (!hit) {
          this.unavailable.set(true);
          this.resolving.set(false);
          return;
        }
        // Already have a DM with them → open it instead of composing a new one.
        if (hit.existingConversationId) {
          void this.router.navigate(['/chat', hit.existingConversationId], { replaceUrl: true });
          return;
        }
        this.targetUserId.set(hit.userId);
        this.displayName.set(hit.displayName);
        this.resolving.set(false);
      },
      error: () => {
        this.unavailable.set(true);
        this.resolving.set(false);
      },
    });
  }

  protected onDraftChange(value: string): void {
    this.draft.set(value);
  }

  protected send(): void {
    const userId = this.targetUserId();
    if (!userId || !this.canSend()) {
      return;
    }

    const body = this.draft().trim();
    this.sending.set(true);
    this.sendError.set(null);

    this.chat.sendDirect(userId, body).subscribe({
      next: (r) => void this.router.navigate(['/chat', r.conversation.id], { replaceUrl: true }),
      error: (e: { error?: { detail?: string } }) => {
        this.sending.set(false);
        this.sendError.set(e.error?.detail ?? "That message couldn't be sent.");
      },
    });
  }
}
