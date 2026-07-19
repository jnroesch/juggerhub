import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ChatService } from '../../../core/services/chat.service';
import { ChatSearchResult, Conversation } from '../../../core/models/chat.models';

/**
 * The chat inbox (feature 019, wireframe 9a): every conversation as a row, a search that finds both
 * messages and people, and a warm empty state.
 *
 * Rows render live — the shared {@link ChatService} keeps them current over SignalR — and the four
 * kinds read at a glance: DMs are round avatars, groups a 2×2 cluster, and the auto-made team and
 * party chats wear a small tag.
 */
@Component({
  selector: 'jh-chat-inbox',
  imports: [RouterLink, FormsModule],
  templateUrl: './chat-inbox.component.html',
  styleUrl: './chat-inbox.component.css',
})
export class ChatInboxComponent implements OnInit {
  private readonly chat = inject(ChatService);
  private readonly router = inject(Router);

  protected readonly conversations = this.chat.conversations;
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);

  protected readonly term = signal('');
  protected readonly results = signal<ChatSearchResult | null>(null);
  protected readonly searching = signal(false);

  /** Search replaces the list while a term is entered; clearing it returns to the inbox. */
  protected readonly isSearching = computed(() => this.term().trim().length >= 2);

  protected readonly hasNothing = computed(() => !this.loading() && this.conversations().length === 0);

  private searchTimer?: ReturnType<typeof setTimeout>;

  ngOnInit(): void {
    this.chat.loadInbox().subscribe({
      next: () => this.loading.set(false),
      error: () => {
        this.loading.set(false);
        this.failed.set(true);
      },
    });
  }

  protected onTermChange(value: string): void {
    this.term.set(value);

    clearTimeout(this.searchTimer);
    if (value.trim().length < 2) {
      this.results.set(null);
      return;
    }

    // Debounced so a fast typist doesn't fire a query per keystroke.
    this.searchTimer = setTimeout(() => this.runSearch(value), 250);
  }

  private runSearch(value: string): void {
    this.searching.set(true);
    this.chat.search(value).subscribe({
      next: (r) => {
        this.results.set(r);
        this.searching.set(false);
      },
      error: () => this.searching.set(false),
    });
  }

  protected clearSearch(): void {
    this.term.set('');
    this.results.set(null);
  }

  /** A person result opens the existing DM, or starts one. */
  protected chatWith(userId: string, existingConversationId: string | null): void {
    if (existingConversationId) {
      void this.router.navigate(['/chat', existingConversationId]);
      return;
    }

    this.chat.start([userId], null).subscribe({
      next: (c) => void this.router.navigate(['/chat', c.id]),
      error: () => undefined,
    });
  }

  /** Names of people typing in a row's conversation ("Lena is typing…"). */
  protected typingIn(conversationId: string): string | null {
    const who = this.chat.typing().filter((t) => t.conversationId === conversationId);
    if (who.length === 0) {
      return null;
    }

    return who.length === 1 ? `${who[0].displayName} is typing…` : 'Several people are typing…';
  }

  /** The TEAM / PARTY eyebrow tag, or null for a DM/group. */
  protected tagFor(c: Conversation): string | null {
    return c.kind === 'Team' ? 'Team' : c.kind === 'Party' ? 'Party' : null;
  }

  protected badge(count: number): string {
    return count > 9 ? '9+' : String(count);
  }

  /** Compact, human time for a row: today → time, this week → weekday, older → date. */
  protected rowTime(iso: string): string {
    const at = new Date(iso);
    const now = new Date();
    const sameDay = at.toDateString() === now.toDateString();

    if (sameDay) {
      return at.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
    }

    const days = (now.getTime() - at.getTime()) / 86_400_000;
    return days < 7
      ? at.toLocaleDateString(undefined, { weekday: 'short' })
      : at.toLocaleDateString(undefined, { day: '2-digit', month: '2-digit' });
  }
}
