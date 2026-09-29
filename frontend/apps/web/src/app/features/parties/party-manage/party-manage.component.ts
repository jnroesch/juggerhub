import { Component, ElementRef, Injector, OnInit, afterNextRender, computed, inject, signal } from '@angular/core';
import { AlertComponent, ButtonDirective, CardComponent, ChipDirective, ConfirmDialogComponent, EmptyStateComponent, IconComponent, LoadingComponent } from '../../../shared/ui';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoDatePipe } from '@jsverse/transloco-locale';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { PluralKeyPipe } from '../../../core/i18n/plural-key.pipe';
import { Party, PartyMember, PartyNews, PartyRosterGroup } from '../../../core/models/party.models';
import { ChatService } from '../../../core/services/chat.service';
import { PartyService } from '../../../core/services/party.service';
import { Pompfe, pompfeLabelKey } from '../../../shared/pompfen.catalog';
import { NewsPostComponent } from '../../../shared/news-post/news-post.component';
import { NewsPostEditing } from '../../../shared/news-post/news-post-editing';
import { SignupStatus } from '../../../core/models/event.models';

const APPLIED_GROUP_KEYS: Record<SignupStatus, string> = {
  Joined: 'parties.manage.appliedJoined',
  AwaitingApproval: 'parties.manage.appliedAwaiting',
  Waitlisted: 'parties.manage.appliedWaitlisted',
};

/**
 * The party manage hub (feature 016 · wireframes 6d–6h). One page for the whole party: roster in
 * three groups (In / Declined / No reply), readiness, the primary "Apply to event" action, party
 * tools (news, co-admins, disband + LATER placeholders), and — for a non-admin crew member — the
 * join/leave affordances. Admin controls are gated on the viewer's party role (server-enforced too).
 * The crew also gets a way into the party's chat, in whichever of those two cards is theirs (feature 063).
 */
@Component({
  selector: 'jh-party-manage',
  imports: [RouterLink, TranslocoDatePipe, FormsModule, ButtonDirective, ChipDirective, LoadingComponent, AlertComponent, EmptyStateComponent, CardComponent, TranslocoPipe, PluralKeyPipe, IconComponent, NewsPostComponent, ConfirmDialogComponent],
  // Feature 059 — an open editor and its typed text belong to the page, not to one post's controls.
  providers: [NewsPostEditing],
  templateUrl: './party-manage.component.html',
  styleUrl: './party-manage.component.css',
})
export class PartyManageComponent implements OnInit {
  private readonly parties = inject(PartyService);
  private readonly chat = inject(ChatService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly party = signal<Party | null>(null);
  protected readonly members = signal<PartyMember[]>([]);
  protected readonly activeTab = signal<PartyRosterGroup>('In');
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly acting = signal(false);
  protected readonly error = signal<string | null>(null);

  // Party news, shown inline on the manage view (crew-only).
  protected readonly news = signal<PartyNews[]>([]);
  protected readonly posting = signal(false);
  protected readonly newsBody = signal('');
  /** Feature 059 — what happened to a post an admin tried to act on (a translation key). */
  protected readonly newsNotice = signal<string | null>(null);

  protected readonly saveNews = (postId: string, body: string): Observable<PartyNews> =>
    this.parties.editNews(this.id, postId, body);

  protected readonly deleteNews = (postId: string): Observable<void> => this.parties.deleteNews(this.id, postId);

  protected readonly isAdmin = computed(() => this.party()?.myRole === 'Admin');
  protected readonly isApplied = computed(() => this.party()?.status === 'Applied');
  /** Where the party's entry landed on the event, as a translation key (GH #388 — it showed the raw value). */
  protected readonly appliedGroupKey = computed(() => {
    const group = this.party()?.appliedGroup;
    return group ? APPLIED_GROUP_KEYS[group] : null;
  });
  /** Crew members (In or Admin) can see the private news feed. */
  protected readonly isCrew = computed(() => {
    const s = this.party()?.myState;
    return s === 'In' || s === 'Admin';
  });

  protected id = '';

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    this.reload();
  }

  private reload(): void {
    this.parties.getParty(this.id).subscribe({
      next: (p) => {
        this.party.set(p);
        this.loading.set(false);
        this.loadTab(this.activeTab());
        if (this.isCrew()) {
          this.loadNews();
        }
      },
      error: () => {
        this.notFound.set(true);
        this.loading.set(false);
      },
    });
  }

  private loadNews(): void {
    this.parties.listNews(this.id).subscribe({ next: (page) => this.news.set(page.items), error: () => this.news.set([]) });
  }

  protected postNews(): void {
    const text = this.newsBody().trim();
    if (this.posting() || text.length === 0) {
      return;
    }
    this.posting.set(true);
    this.parties.postNews(this.id, { body: text }).subscribe({
      next: () => {
        this.newsBody.set('');
        this.posting.set(false);
        this.loadNews();
      },
      error: () => this.posting.set(false),
    });
  }

  protected replaceNews(updated: PartyNews): void {
    this.newsNotice.set(null);
    this.news.update((list) => list.map((n) => (n.id === updated.id ? updated : n)));
  }

  /** The post is gone — deleted here, or by another admin meanwhile (FR-020), which the notice says. */
  protected dropNews(id: string, gone: boolean): void {
    this.news.update((list) => list.filter((n) => n.id !== id));
    this.newsNotice.set(gone ? 'news.gone' : null);
    // The button that opened the menu went with the post; land on the section's heading instead.
    afterNextRender(() => this.host.nativeElement.querySelector<HTMLElement>('#party-news-heading')?.focus(), {
      injector: this.injector,
    });
  }

  protected positions(pompfen: Pompfe[]): string {
    return pompfen.map((p) => this.transloco.translate(pompfeLabelKey(p))).join(' · ');
  }

  protected loadTab(group: PartyRosterGroup): void {
    this.activeTab.set(group);
    this.parties.listMembers(this.id, group).subscribe({
      next: (page) => this.members.set(page.items),
      error: () => this.members.set([]),
    });
  }

  // --- The party chat (feature 063) -----------------------------------------

  protected readonly partyChatBusy = signal(false);
  /** The chat could not be opened — shown in the card beside the button. A translation key. */
  protected readonly partyChatError = signal<string | null>(null);
  /**
   * The player is no longer in the crew. Page level, not in the card: the reload that follows swaps the
   * crew card for the request card (or the whole page for "not found"). A translation key.
   */
  protected readonly partyChatNotice = signal<string | null>(null);

  /**
   * Open the party's own chat. Looked up on the press, never on load (as the team page does, feature
   * 060); the server creates the chat if nobody in the crew has opened it yet.
   */
  protected openPartyChat(): void {
    if (this.partyChatBusy()) {
      return;
    }
    this.partyChatBusy.set(true);
    this.partyChatError.set(null);
    this.partyChatNotice.set(null);
    this.chat.openPartyChat(this.id).subscribe({
      next: (ref) => {
        this.partyChatBusy.set(false);
        void this.router.navigate(['/chat', ref.conversationId]);
      },
      error: (err) => {
        this.partyChatBusy.set(false);
        // Branch on the status, never the server's English `detail` (GH #179) — which is why this does
        // not go through fail(). A 404 means the player left or was removed since the page loaded, or
        // the party was disbanded: say so, and show the page as it now is.
        if (err instanceof HttpErrorResponse && err.status === 404) {
          this.partyChatNotice.set('parties.manage.partyChatNotCrew');
          this.reload();
          return;
        }
        this.partyChatError.set('parties.manage.partyChatFailed');
      },
    });
  }

  // --- Self actions (crew member) ------------------------------------------

  protected join(): void {
    this.run(() => this.parties.join(this.id));
  }

  protected declineRequest(): void {
    this.run(() => this.parties.decline(this.id));
  }

  protected leave(): void {
    this.run(() => this.parties.leave(this.id));
  }

  // --- Admin actions --------------------------------------------------------

  protected nudge(userId: string): void {
    this.parties.nudge(this.id, userId).subscribe({ error: () => undefined });
  }

  // --- Asking first (feature 064): Remove and Disband go through one in-page dialog ---------------

  /**
   * What the dialog is asking about (null = closed). A removal remembers the tab it was asked from:
   * on *In* it takes a player out of the crew, on *Declined* it clears their answer — two different
   * questions for one button.
   */
  protected readonly pendingConfirm = signal<
    { kind: 'remove'; member: PartyMember; tab: PartyRosterGroup } | { kind: 'disband' } | null
  >(null);
  protected readonly confirmBusy = signal(false);
  /** Why the last attempt failed, shown in the dialog. A translation key. */
  protected readonly confirmError = signal<string | null>(null);
  /**
   * The player was already gone when the removal was confirmed. Page level: the reload that follows
   * redraws the roster the note would otherwise sit in. A translation key and the name it needs.
   */
  protected readonly removeNotice = signal<{ key: string; name: string } | null>(null);

  /** The dialog's wording, by what it is asking about. Translation keys. */
  protected readonly confirmCopy = computed(() => {
    const pending = this.pendingConfirm();
    if (!pending) {
      return null;
    }
    if (pending.kind === 'disband') {
      return {
        title: 'parties.manage.disbandTitle',
        body: 'parties.manage.disbandBody',
        keep: 'parties.manage.disbandKeep',
        confirm: 'parties.manage.disbandConfirm',
        busy: 'parties.manage.disbanding',
        name: '',
      };
    }
    const declined = pending.tab === 'Declined';
    return {
      title: declined ? 'parties.manage.removeDeclinedTitle' : 'parties.manage.removeInTitle',
      body: declined ? 'parties.manage.removeDeclinedBody' : 'parties.manage.removeInBody',
      keep: declined ? 'parties.manage.keepAnswer' : 'parties.manage.keepMember',
      confirm: declined ? 'parties.manage.removeDeclinedConfirm' : 'parties.manage.removeInConfirm',
      busy: declined ? 'parties.manage.clearing' : 'parties.manage.removing',
      name: pending.member.displayName,
    };
  });

  protected askRemove(member: PartyMember): void {
    this.confirmError.set(null);
    this.removeNotice.set(null);
    this.pendingConfirm.set({ kind: 'remove', member, tab: this.activeTab() });
  }

  protected askDisband(): void {
    this.confirmError.set(null);
    this.pendingConfirm.set({ kind: 'disband' });
  }

  protected dismissConfirm(): void {
    const pending = this.pendingConfirm();
    if (!pending || this.confirmBusy()) {
      return;
    }
    this.pendingConfirm.set(null);
    this.confirmError.set(null);
    // Back to the button that asked, or a keyboard user is left on the page body.
    this.focusAfterRender(pending.kind === 'disband' ? '[data-testid="party-disband"]' : `[data-party-remove="${pending.member.userId}"]`);
  }

  protected confirmPending(): void {
    const pending = this.pendingConfirm();
    if (!pending || this.confirmBusy()) {
      return;
    }
    this.confirmBusy.set(true);
    this.confirmError.set(null);
    if (pending.kind === 'disband') {
      this.parties.disband(this.id).subscribe({
        next: () => {
          this.confirmBusy.set(false);
          this.pendingConfirm.set(null);
          const slug = this.party()?.teamSlug;
          this.router.navigate(slug ? ['/t', slug] : ['/']);
        },
        error: () => {
          // Our own sentence, never the server's English `detail` (GH #179); confirming again is the retry.
          this.confirmBusy.set(false);
          this.confirmError.set('parties.manage.disbandFailed');
        },
      });
      return;
    }

    this.parties.removeMember(this.id, pending.member.userId).subscribe({
      next: () => {
        this.confirmBusy.set(false);
        this.pendingConfirm.set(null);
        this.reload();
      },
      error: (err) => {
        this.confirmBusy.set(false);
        // Branch on the status, never the server's `detail` (GH #179). A 404: the player already left,
        // or the party is gone — say so, and show the page as it now is.
        if (err instanceof HttpErrorResponse && err.status === 404) {
          this.pendingConfirm.set(null);
          this.removeNotice.set({ key: 'parties.manage.removeGone', name: pending.member.displayName });
          this.reload();
          return;
        }
        this.confirmError.set('parties.manage.removeFailed');
      },
    });
  }

  private focusAfterRender(selector: string): void {
    afterNextRender(() => this.host.nativeElement.querySelector<HTMLElement>(selector)?.focus(), { injector: this.injector });
  }

  protected apply(): void {
    this.run(() => this.parties.apply(this.id));
  }

  protected withdraw(): void {
    this.run(() => this.parties.withdraw(this.id));
  }

  private run(op: () => Observable<unknown>): void {
    if (this.acting()) {
      return;
    }
    this.acting.set(true);
    this.error.set(null);
    // A note about the party chat describes the page before this action (e.g. "no longer in this
    // crew" and then "I'm in" again), so it goes with it.
    this.partyChatNotice.set(null);
    this.partyChatError.set(null);
    op().subscribe({
      next: () => {
        this.acting.set(false);
        this.reload();
      },
      error: (err) => this.fail(err),
    });
  }

  private fail(err: unknown): void {
    this.acting.set(false);
    this.error.set(err instanceof HttpErrorResponse ? err.error?.detail ?? this.transloco.translate('parties.manage.error') : this.transloco.translate('parties.manage.error'));
  }
}
