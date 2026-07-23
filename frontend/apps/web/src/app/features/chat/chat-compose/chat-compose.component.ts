import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { LoadingComponent } from '../../../shared/ui';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ChatService } from '../../../core/services/chat.service';

/** Which kind of thread this compose view is drafting (feature 022 DM, or feature 027 inquiry). */
type ComposeMode = 'direct' | 'team' | 'event';

/**
 * Compose a NEW direct message (feature 022 — lazy DM creation) OR a new "contact the admins" inquiry
 * (feature 027). Reached at `/chat/compose/:handle` (DM) or `/chat/contact/:kind/:targetId` (inquiry).
 *
 * Crucially this view persists **nothing**: no conversation, no inbox entry, no typing/read signals.
 * The conversation is created only when the first message is sent (`ChatService.sendDirect` /
 * `sendInquiryToTeam` / `sendInquiryToEvent`), after which we replace the URL with the real
 * `/chat/:id`. Leaving without sending leaves no trace.
 */
@Component({
  selector: 'jh-chat-compose',
  imports: [FormsModule, RouterLink, LoadingComponent],
  templateUrl: './chat-compose.component.html',
  styleUrl: './chat-compose.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChatComposeComponent implements OnInit {
  /** The target player's public handle (DM mode; from the route). Absent in inquiry mode. */
  readonly handle = input<string>('');
  /** Inquiry mode: 'team' | 'event' (from the `contact/:kind/...` route). Absent in DM mode. */
  readonly kind = input<string>('');
  /** Inquiry mode: the team/event id (from the route). */
  readonly targetId = input<string>('');

  private readonly chat = inject(ChatService);
  private readonly router = inject(Router);

  protected readonly displayName = signal('');
  protected readonly targetUserId = signal<string | null>(null);
  protected readonly resolving = signal(true);
  /** The player can't be messaged (blocked, or the account is gone), or the target is unavailable. */
  protected readonly unavailable = signal(false);

  /** DM by default; set from the route in inquiry mode. */
  protected readonly mode = signal<ComposeMode>('direct');

  protected readonly draft = signal('');
  protected readonly sending = signal(false);
  protected readonly sendError = signal<string | null>(null);

  protected readonly canSend = computed(
    () => this.draft().trim().length > 0 && !this.sending() && this.isReady(),
  );

  /** In DM mode we need a resolved user id; in inquiry mode a target id is enough. */
  private isReady(): boolean {
    return this.mode() === 'direct' ? this.targetUserId() !== null : this.targetId().length > 0;
  }

  ngOnInit(): void {
    // Inquiry mode (feature 027): the "Contact admins" button routes here with the team/event id and
    // passes the team/event name via navigation state for the header. If a thread already exists,
    // jump straight into it rather than opening a fresh compose (FR-004).
    const routeKind = this.kind();
    if (routeKind === 'team' || routeKind === 'event') {
      this.mode.set(routeKind);
      const state = history.state as { name?: string } | null;
      this.displayName.set(state?.name ?? (routeKind === 'team' ? 'the team admins' : 'the event admins'));

      const id = this.targetId();
      const find$ = routeKind === 'team' ? this.chat.findTeamInquiry(id) : this.chat.findEventInquiry(id);
      find$.subscribe({
        next: (ref) => {
          if (ref.conversationId) {
            void this.router.navigate(['/chat', ref.conversationId], { replaceUrl: true });
            return;
          }
          this.resolving.set(false);
        },
        error: () => this.resolving.set(false),
      });
      return;
    }

    // DM mode: the entry points pass the resolved person via navigation state to save a round trip.
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
    if (!this.canSend()) {
      return;
    }

    const body = this.draft().trim();
    this.sending.set(true);
    this.sendError.set(null);

    const mode = this.mode();
    const send$ =
      mode === 'team'
        ? this.chat.sendInquiryToTeam(this.targetId(), body)
        : mode === 'event'
          ? this.chat.sendInquiryToEvent(this.targetId(), body)
          : this.chat.sendDirect(this.targetUserId()!, body);

    send$.subscribe({
      next: (r) => void this.router.navigate(['/chat', r.conversation.id], { replaceUrl: true }),
      error: (e: { error?: { detail?: string } }) => {
        this.sending.set(false);
        this.sendError.set(e.error?.detail ?? "That message couldn't be sent.");
      },
    });
  }
}
