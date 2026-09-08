import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ButtonDirective } from '../../../shared/ui';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ChatService } from '../../../core/services/chat.service';
import { Conversation } from '../../../core/models/chat.models';
import { injectLocale } from '../../../core/i18n/locale-format';

/**
 * The chat inbox (feature 019, wireframe 9a): every conversation as a row, a search that narrows
 * those rows to the conversations whose members' or own names match (feature 046), and a warm empty
 * state.
 *
 * Rows render live — the shared {@link ChatService} keeps them current over SignalR — and the kinds
 * read at a glance: DMs are round avatars, groups a 2×2 cluster, and the auto-made team and party
 * chats wear a small tag. Search results are those same rows, from the same endpoint, so a match
 * looks and behaves exactly like its inbox row. Message text is never searched.
 */
@Component({
  selector: 'jh-chat-inbox',
  imports: [RouterLink, FormsModule, ButtonDirective, TranslocoPipe],
  templateUrl: './chat-inbox.component.html',
  styleUrl: './chat-inbox.component.css',
})
export class ChatInboxComponent implements OnInit {
  private readonly chat = inject(ChatService);
  private readonly t = inject(TranslocoService);
  private readonly lang = toSignal(this.t.langChanges$, { initialValue: this.t.getActiveLang() });
  private readonly locale = injectLocale();

  protected readonly conversations = this.chat.conversations;
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);

  protected readonly term = signal('');
  /** The conversations the current term matched; null until the first page for a term arrives. */
  protected readonly results = signal<Conversation[] | null>(null);
  protected readonly searching = signal(false);

  /** Search replaces the list while a term is entered; clearing it returns to the inbox. */
  protected readonly isSearching = computed(() => this.term().trim().length >= 2);

  /**
   * What the list renders: the inbox, or — while a term is entered — the conversations it matched.
   * Until the first page for a term arrives the inbox stays put beneath the status line, so a search
   * never blanks the screen (DESIGN.md: keep what's there and let the quiet line do the talking).
   */
  protected readonly displayed = computed(() =>
    this.isSearching() ? (this.results() ?? this.conversations()) : this.conversations(),
  );

  /** A term was searched and matched nothing — an empty state, never an error. */
  protected readonly noMatches = computed(
    () => this.isSearching() && !this.searching() && this.results()?.length === 0,
  );

  protected readonly hasNothing = computed(() => !this.loading() && this.conversations().length === 0);

  private searchTimer?: ReturnType<typeof setTimeout>;
  /** Bumped per search so a late response for an older term can never overwrite a newer one. */
  private searchSeq = 0;

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
      this.searchSeq++;
      this.results.set(null);
      this.searching.set(false);
      return;
    }

    // Debounced so a fast typist doesn't fire a query per keystroke.
    this.searchTimer = setTimeout(() => this.runSearch(value), 250);
  }

  private runSearch(value: string): void {
    const seq = ++this.searchSeq;
    this.searching.set(true);
    this.chat.searchInbox(value.trim()).subscribe({
      next: (page) => {
        if (seq !== this.searchSeq) {
          return;
        }
        this.results.set([...page.items]);
        this.searching.set(false);
      },
      error: () => {
        if (seq === this.searchSeq) {
          this.searching.set(false);
        }
      },
    });
  }

  protected clearSearch(): void {
    this.onTermChange('');
  }

  /** Names of people typing in a row's conversation ("Lena is typing…"). */
  protected typingIn(conversationId: string): string | null {
    const who = this.chat.typing().filter((t) => t.conversationId === conversationId);
    if (who.length === 0) {
      return null;
    }

    this.lang();
    return who.length === 1
      ? this.t.translate('chat.inbox.typingOne', { name: who[0].displayName })
      : this.t.translate('chat.inbox.typingSeveral');
  }

  /** The TEAM / PARTY / ADMINS eyebrow tag, or null for a DM/group. */
  protected tagFor(c: Conversation): string | null {
    this.lang();
    switch (c.kind) {
      case 'Team':
        return this.t.translate('chat.inbox.tagTeam');
      case 'Party':
        return this.t.translate('chat.inbox.tagParty');
      // Both inquiry kinds (feature 027) wear the same tag — a player's line to the admins.
      case 'TeamInquiry':
      case 'EventInquiry':
        return this.t.translate('chat.inbox.tagAdmins');
      default:
        return null;
    }
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
      return at.toLocaleTimeString(this.locale(), { hour: '2-digit', minute: '2-digit' });
    }

    const days = (now.getTime() - at.getTime()) / 86_400_000;
    return days < 7
      ? at.toLocaleDateString(this.locale(), { weekday: 'short' })
      : at.toLocaleDateString(this.locale(), { day: '2-digit', month: '2-digit' });
  }
}
