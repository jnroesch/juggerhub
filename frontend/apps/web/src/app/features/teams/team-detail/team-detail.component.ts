import { TranslocoDatePipe } from '@jsverse/transloco-locale';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, Injector, afterNextRender, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { Observable } from 'rxjs';
import { PluralKeyPipe } from '../../../core/i18n/plural-key.pipe';
import { AlertComponent, ButtonDirective, CardComponent, ChipDirective, ConfirmDialogComponent, EmptyStateComponent, IconComponent, LoadingComponent } from '../../../shared/ui';
import { Pompfe, pompfeLabelKey } from '../../../shared/pompfen.catalog';
import {
  JoinRequest,
  PublicMember,
  TeamHappening,
  TeamMember,
  TeamNews,
  TeamPublicDetail,
} from '../../../core/models/team.models';
import { AuthService } from '../../../core/services/auth.service';
import { ChatService } from '../../../core/services/chat.service';
import { TeamService } from '../../../core/services/team.service';
import { PartyService } from '../../../core/services/party.service';
import { PartyRequestCard } from '../../../core/models/party.models';
import { problemDetail } from '../../../core/utils/problem';
import { linkHost } from '../../../core/utils/link-host';
import { RecognitionDisplayComponent } from '../../profile/components/recognition-display/recognition-display.component';
import { TeamHappeningsComponent } from './happenings/team-happenings.component';
import { TeamPlacementsComponent } from './placements/team-placements.component';
import { TeamPollsComponent } from './polls/team-polls.component';
import { NewsPostComponent } from '../../../shared/news-post/news-post.component';
import { NewsPostEditing } from '../../../shared/news-post/news-post-editing';

/**
 * The team page (feature 009). Public to everyone: overview, roster (names + positions),
 * recent activity, and upcoming trainings, plus a state-aware request-to-join action. Members
 * additionally see the news feed; admins additionally see the join-request queue and the roster
 * admin controls + team tools. The viewer's relation is decided server-side.
 */
@Component({
  selector: 'jh-team-detail',
  imports: [LoadingComponent, RouterLink, TranslocoDatePipe, RecognitionDisplayComponent, TeamHappeningsComponent, TeamPlacementsComponent, TeamPollsComponent, ButtonDirective, ChipDirective, EmptyStateComponent, CardComponent, TranslocoPipe, PluralKeyPipe, IconComponent, AlertComponent, NewsPostComponent, ConfirmDialogComponent],
  providers: [NewsPostEditing],
  templateUrl: './team-detail.component.html',
  styleUrl: './team-detail.component.css',
})
export class TeamDetailComponent {
  private readonly teams = inject(TeamService);
  private readonly parties = inject(PartyService);
  private readonly chat = inject(ChatService);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly slug = signal('');
  /**
   * Counts the teams this page has shown. The router reuses the component from one team to the next,
   * so an answer can arrive for a team the page has left — or left and come back to, which a
   * comparison of slugs cannot tell from never having left. A call notes the count when it starts,
   * and its answer is dropped if the count has moved on.
   */
  private visit = 0;
  protected readonly pub = signal<TeamPublicDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly error = signal<string | null>(null);

  // Members/admins load the full roster (with ids + admin menu) + news; admins load the queue.
  protected readonly members = signal<TeamMember[]>([]);
  protected readonly news = signal<TeamNews[]>([]);
  /** Feature 044 — the team-internal "What's happening" feed (members only). */
  protected readonly happenings = signal<TeamHappening[]>([]);
  protected readonly joinRequests = signal<JoinRequest[]>([]);
  // Feature 016: pinned party-request cards a member can answer.
  protected readonly partyRequests = signal<PartyRequestCard[]>([]);
  protected readonly partyBusy = signal(false);
  protected readonly openMenu = signal<string | null>(null);

  protected readonly requestBusy = signal(false);
  /** Which join action a confirmation modal is currently gating (null = closed). */
  protected readonly confirmIntent = signal<'join' | 'cancel' | null>(null);

  protected readonly relation = computed(() => this.pub()?.viewerRelation ?? 'Anonymous');
  protected readonly isMember = computed(() => this.relation() === 'Member' || this.relation() === 'Admin');
  protected readonly isAdmin = computed(() => this.relation() === 'Admin');
  protected readonly isAnon = computed(() => this.relation() === 'Anonymous');
  protected readonly canRequest = computed(() => this.relation() === 'NonMember');
  protected readonly requested = computed(() => this.relation() === 'Requested');
  /** Feature 027: any signed-in non-admin may contact the team's admins (FR-001/FR-002). */
  protected readonly canContactAdmins = computed(() => !this.isAnon() && !this.isAdmin());
  /**
   * The viewer's own id, so the roster's admin menu skips their own row (GH #361): what it
   * offers there — step down, leave — lives on "Manage team", not in a per-member menu.
   */
  protected readonly myUserId = computed(() => this.auth.currentUser()?.id ?? null);

  /** Open a "contact the admins" thread (feature 027). Nothing persists until the first message is sent. */
  protected contactAdmins(): void {
    const team = this.pub();
    if (!team) {
      return;
    }
    void this.router.navigate(['/chat', 'contact', 'team', team.id], { state: { name: team.name } });
  }

  // --- The team chat (feature 060) ---------------------------------------------------------------

  protected readonly teamChatBusy = signal(false);
  /** The chat could not be opened — shown in the card beside the button. A translation key. */
  protected readonly teamChatError = signal<string | null>(null);
  /**
   * The player is no longer on the team. Page level, not in the card: the reload that follows turns
   * the page into its non-member view, and the card goes with it. A translation key.
   */
  protected readonly teamChatNotice = signal<string | null>(null);

  /**
   * Open the team's own chat. Looked up on the press, never on load (the owner declined an unread
   * count, which is the only thing a lookup on load would have bought); the server creates the chat
   * if nobody has opened it yet.
   */
  protected openTeamChat(): void {
    const team = this.pub();
    if (!team || this.teamChatBusy()) {
      return;
    }
    this.teamChatBusy.set(true);
    this.teamChatError.set(null);
    this.teamChatNotice.set(null);
    this.chat.openTeamChat(team.id).subscribe({
      next: (ref) => {
        this.teamChatBusy.set(false);
        void this.router.navigate(['/chat', ref.conversationId]);
      },
      error: (err) => {
        this.teamChatBusy.set(false);
        // Branch on the status, never the server's English `detail` (GH #179). A 404 means the
        // player left or was removed since the page loaded: say so, and show the page as it now is.
        if (err instanceof HttpErrorResponse && err.status === 404) {
          this.teamChatNotice.set('teams.detail.teamChatNotMember');
          this.load();
          return;
        }
        this.teamChatError.set('teams.detail.teamChatFailed');
      },
    });
  }

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((pm) => {
      this.slug.set(pm.get('slug') ?? '');
      this.visit++;
      // The router reuses this component from one team to the next; an editor left open on the
      // previous team has no place here (feature 057). Only on a switch, not in load(): approving a
      // join request reloads too, mid-edit, and the editor and its text must survive that.
      this.newsEditing.reset();
      this.newsNotice.set(null);
      // Feature 058 — a note about another team's join requests has no place here.
      this.joinNotice.set(null);
      this.answerError.set(null);
      this.requestError.set(null);
      // A join confirmation left open was asked about the PREVIOUS team: here it would ask, unprompted,
      // about this one. A request still on its way belongs to that team too: its answer is dropped
      // when it arrives (see `visit`), so the busy flag it would have cleared is cleared here.
      this.confirmIntent.set(null);
      this.requestBusy.set(false);
      // Feature 060 — likewise for the team chat's notes.
      this.teamChatNotice.set(null);
      this.teamChatError.set(null);
      // Feature 064 — a pending removal was asked about a member of the PREVIOUS team: confirming it
      // here would send that member's id with this team's slug. It goes, with its notes.
      this.removing.set(null);
      this.removeBusy.set(false);
      this.removeError.set(null);
      this.removeNotice.set(null);
      this.load();
    });
  }

  /**
   * Reload the page. `then` runs after the reloaded page has rendered — not after the next render,
   * which is the loading line: the page's own elements do not exist until the detail arrives.
   */
  private load(then?: () => void): void {
    this.loading.set(true);
    this.notFound.set(false);
    this.members.set([]);
    this.news.set([]);
    this.happenings.set([]);
    this.joinRequests.set([]);

    this.teams.getPublicDetail(this.slug()).subscribe({
      next: (d) => {
        this.pub.set(d);
        this.loading.set(false);
        if (then) {
          afterNextRender(then, { injector: this.injector });
        }
        if (d.viewerRelation === 'Member' || d.viewerRelation === 'Admin') {
          this.loadMembers();
          this.loadNews();
          this.loadHappenings();
          this.loadPartyRequests();
        }
        if (d.viewerRelation === 'Admin') {
          this.loadJoinRequests();
        }
      },
      error: () => {
        this.loading.set(false);
        this.notFound.set(true);
      },
    });
  }

  private loadMembers(): void {
    this.teams.getMembers(this.slug()).subscribe({ next: (p) => this.members.set(p.items) });
  }

  private loadNews(): void {
    this.teams.getNews(this.slug()).subscribe({ next: (p) => this.news.set(p.items) });
  }

  /** Feature 044 — members only; already capped and windowed server-side, so no paging here. */
  private loadHappenings(): void {
    this.teams.getHappenings(this.slug()).subscribe({ next: (items) => this.happenings.set(items) });
  }

  protected readonly postingNews = signal(false);

  /** Admin-only (feature 010): post a news update; it fans out notifications to the roster. */
  protected postNews(input: HTMLTextAreaElement): void {
    const body = input.value.trim();
    if (body.length === 0 || this.postingNews()) {
      return;
    }
    this.postingNews.set(true);
    this.error.set(null);
    this.teams.postNews(this.slug(), body).subscribe({
      next: (post) => {
        this.news.update((current) => [post, ...current]);
        input.value = '';
        this.postingNews.set(false);
      },
      error: (err) => {
        this.error.set(problemDetail(err));
        this.postingNews.set(false);
      },
    });
  }

  // --- Editing and deleting news (feature 057): any admin, any post -----------------------------
  // The controls themselves are the shared jh-news-post (feature 059); the page keeps its list.

  /** Which post is being edited, and the typed text: page state, so a reload mid-edit keeps it. */
  private readonly newsEditing = inject(NewsPostEditing);
  /** What happened to a post an admin tried to act on — a translation key, re-rendered on a language switch. */
  protected readonly newsNotice = signal<string | null>(null);

  protected readonly saveNews = (postId: string, body: string): Observable<TeamNews> =>
    this.teams.editNews(this.slug(), postId, body);

  protected readonly deleteNews = (postId: string): Observable<void> => this.teams.deleteNews(this.slug(), postId);

  protected replaceNews(updated: TeamNews): void {
    this.newsNotice.set(null);
    this.news.update((list) => list.map((n) => (n.id === updated.id ? updated : n)));
  }

  /** The post is gone — deleted here, or by another admin meanwhile (FR-019), which the notice says. */
  protected dropNews(id: string, gone: boolean): void {
    this.news.update((list) => list.filter((n) => n.id !== id));
    this.newsNotice.set(gone ? 'news.gone' : null);
    // The button that opened the menu went with the post; land on the list's heading instead.
    afterNextRender(() => this.focus('#team-news-heading'), { injector: this.injector });
  }

  private focus(selector: string): void {
    this.host.nativeElement.querySelector<HTMLElement>(selector)?.focus();
  }

  private loadJoinRequests(): void {
    this.teams.getJoinRequests(this.slug()).subscribe({ next: (p) => this.joinRequests.set(p.items) });
  }

  private loadPartyRequests(): void {
    this.parties.getTeamPartyRequests(this.slug()).subscribe({ next: (p) => this.partyRequests.set(p.items) });
  }

  /** Answer a pinned party request from the team space (feature 016). */
  protected answerParty(card: PartyRequestCard, join: boolean): void {
    if (this.partyBusy()) {
      return;
    }
    this.partyBusy.set(true);
    const op = join ? this.parties.join(card.partyId) : this.parties.decline(card.partyId);
    op.subscribe({
      next: () => {
        this.partyBusy.set(false);
        this.loadPartyRequests();
      },
      error: (err) => {
        this.partyBusy.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }

  /** The confirmation's wording, by what it is asking about. Translation keys. */
  protected readonly confirmCopy = computed(() => {
    switch (this.confirmIntent()) {
      case 'join':
        return {
          title: 'teams.detail.confirmJoinTitle',
          body: 'teams.detail.confirmJoinBody',
          keep: 'teams.detail.dismiss',
          confirm: 'teams.detail.confirmJoinSubmit',
        };
      case 'cancel':
        return {
          title: 'teams.detail.confirmCancelTitle',
          body: 'teams.detail.confirmCancelBody',
          keep: 'teams.detail.keepRequest',
          confirm: 'teams.detail.confirmCancelSubmit',
        };
      default:
        return null;
    }
  });

  /** Open the confirmation modal for a join action (feature 009 — guards accidental clicks). */
  protected askConfirm(intent: 'join' | 'cancel'): void {
    if (this.requestBusy()) {
      return;
    }
    this.confirmIntent.set(intent);
  }

  /** The dialog's safe answer, or Escape. It asks neither while the request is under way. */
  protected dismissConfirm(): void {
    const intent = this.confirmIntent();
    if (!intent || this.requestBusy()) {
      return;
    }
    this.confirmIntent.set(null);
    // Back to the button that asked, or a keyboard user is left on the page body.
    this.focusAfterRender(intent === 'join' ? REQUEST_BUTTON : CANCEL_BUTTON);
  }

  /** Run the action the confirmation modal is gating. */
  protected confirmAction(): void {
    if (this.confirmIntent() === 'join') {
      this.requestToJoin();
    } else if (this.confirmIntent() === 'cancel') {
      this.cancelRequest();
    }
  }

  /**
   * A call from a visit that is over has succeeded. Its answer is not acted on, but the team did
   * change. If the page is on that team again, what it shows may be older than the change (it loaded
   * before the call landed), so it is shown afresh. Not while a newer call is under way: a reload
   * would take its question off the screen, and its own answer reloads the page (asking and
   * withdrawing twice are both fine by the server).
   */
  private showAfreshAfterLateSuccess(slug: string): void {
    if (this.slug() === slug && !this.requestBusy() && !this.removeBusy()) {
      this.load();
    }
  }

  /** Zoneless: what to focus exists only after the next render (GH #344's lesson — not an effect). */
  private focusAfterRender(selector: string): void {
    afterNextRender(() => this.focus(selector), { injector: this.injector });
  }

  /** Feature 058 — the player's own request failed. A translation key, so a language switch re-renders it. */
  protected readonly requestError = signal<string | null>(null);

  private requestToJoin(): void {
    if (this.requestBusy()) {
      return;
    }
    this.requestBusy.set(true);
    this.requestError.set(null);
    const visit = this.visit;
    const slug = this.slug();
    this.teams.requestToJoin(slug).subscribe({
      next: () => {
        if (this.visit !== visit) {
          // The page moved on meanwhile (even if it came back); this answer is not acted on.
          this.showAfreshAfterLateSuccess(slug);
          return;
        }
        this.requestBusy.set(false);
        this.confirmIntent.set(null);
        // relation → Requested. The dialog and the button that asked are gone; land on the line that
        // says the request is in, once the reloaded page is on screen.
        this.load(() => this.focus('[data-testid="requested"]'));
      },
      error: (err) => {
        if (this.visit !== visit) {
          return;
        }
        this.requestBusy.set(false);
        this.confirmIntent.set(null);
        const status = err instanceof HttpErrorResponse ? err.status : 0;
        // Branch on the status, never the server's English `detail` (GH #179). A 429 is our own
        // limit on asking (feature 058, FR-023): say when to try again, and never retry it.
        this.requestError.set(
          status === 429
            ? 'teams.detail.requestLimited'
            : status === 409
              ? 'teams.detail.alreadyMember'
              : 'teams.detail.requestFailed',
        );
        if (status === 409) {
          // They are on the team after all: show the page as a member sees it. The button that asked
          // goes with that, so focus lands on the note that says why.
          this.load(() => this.focus('[data-testid="request-error"]'));
        } else {
          this.focusAfterRender(REQUEST_BUTTON);
        }
      },
    });
  }

  /** Feature 009 — withdraw the caller's own pending request; relation → NonMember. */
  private cancelRequest(): void {
    if (this.requestBusy()) {
      return;
    }
    this.requestBusy.set(true);
    this.requestError.set(null);
    const visit = this.visit;
    const slug = this.slug();
    this.teams.cancelJoinRequest(slug).subscribe({
      next: () => {
        if (this.visit !== visit) {
          // The page moved on meanwhile (even if it came back); this answer is not acted on.
          this.showAfreshAfterLateSuccess(slug);
          return;
        }
        this.requestBusy.set(false);
        this.confirmIntent.set(null);
        // relation → NonMember: the page offers the request again, and focus lands there.
        this.load(() => this.focus(REQUEST_BUTTON));
      },
      error: () => {
        if (this.visit !== visit) {
          return;
        }
        this.requestBusy.set(false);
        this.confirmIntent.set(null);
        this.requestError.set('teams.detail.cancelFailed');
        this.focusAfterRender(CANCEL_BUTTON);
      },
    });
  }

  // --- Answering join requests (feature 058) ------------------------------------------------------

  /** A neutral note at the queue's place — translation keys, not text, so a language switch re-renders them. */
  protected readonly joinNotice = signal<string | null>(null);
  protected readonly answerError = signal<string | null>(null);

  protected approve(request: JoinRequest): void {
    // The roster changes too, so the whole page reloads.
    this.answer(this.teams.approveJoinRequest(this.slug(), request.id), () => this.load());
  }

  protected decline(request: JoinRequest): void {
    this.answer(this.teams.declineJoinRequest(this.slug(), request.id), () => this.loadJoinRequests());
  }

  private answer(call: Observable<void>, done: () => void): void {
    this.joinNotice.set(null);
    this.answerError.set(null);
    call.subscribe({
      next: done,
      error: (err) => {
        if (isGone(err)) {
          // Another admin answered first, or the player withdrew or joined another way: the request
          // no longer waits (FR-012, FR-016). Say so, and show the queue as it is now.
          this.joinNotice.set('teams.detail.answerGone');
          this.load();
          return;
        }
        // Branch on the status, never the server's English `detail` (GH #179).
        this.answerError.set('teams.detail.answerFailed');
      },
    });
  }

  protected toggleMenu(userId: string): void {
    this.openMenu.update((u) => (u === userId ? null : userId));
  }

  protected toggleAdmin(member: TeamMember): void {
    const role = member.role === 'Admin' ? 'Member' : 'Admin';
    this.error.set(null);
    this.teams.setRole(this.slug(), member.userId, role).subscribe({
      next: () => {
        this.openMenu.set(null);
        this.loadMembers();
      },
      error: (err) => this.error.set(problemDetail(err)),
    });
  }

  // --- Removing a teammate (feature 064): asked first, told in our own words ----------------------

  /** The teammate the confirmation is asking about (null = closed). */
  protected readonly removing = signal<TeamMember | null>(null);
  protected readonly removeBusy = signal(false);
  /** Why the last attempt failed, shown in the dialog. A translation key. */
  protected readonly removeError = signal<string | null>(null);
  /**
   * What became of an attempt the dialog could not finish — the player already left, or the viewer
   * is no longer an admin. Page level: the reload that follows redraws the roster (and may take the
   * menus away). A translation key and the name it needs.
   */
  protected readonly removeNotice = signal<{ key: string; name: string } | null>(null);

  protected askRemove(member: TeamMember): void {
    this.openMenu.set(null);
    this.removeError.set(null);
    this.removeNotice.set(null);
    this.removing.set(member);
  }

  protected dismissRemove(): void {
    const member = this.removing();
    if (!member || this.removeBusy()) {
      return;
    }
    this.removing.set(null);
    this.removeError.set(null);
    // Back to the button that asked, or a keyboard user is left on the page body.
    afterNextRender(() => this.focus(`[data-member-menu="${member.userId}"]`), { injector: this.injector });
  }

  protected confirmRemove(): void {
    const member = this.removing();
    if (!member || this.removeBusy()) {
      return;
    }
    this.removeBusy.set(true);
    this.removeError.set(null);
    const visit = this.visit;
    const slug = this.slug();
    this.teams.removeMember(slug, member.userId).subscribe({
      next: () => {
        if (this.visit !== visit) {
          // The page moved on meanwhile (even if it came back); this answer is not acted on.
          this.showAfreshAfterLateSuccess(slug);
          return;
        }
        this.removeBusy.set(false);
        this.removing.set(null);
        // The row and its menu button went with the teammate; land on the roster's heading once the
        // reloaded roster is on screen.
        this.load(() => this.focus('#team-roster-heading'));
      },
      error: (err) => {
        if (this.visit !== visit) {
          return;
        }
        this.removeBusy.set(false);
        const status = err instanceof HttpErrorResponse ? err.status : 0;
        // Branch on the status, never the server's English `detail` (GH #179). A 404: they already
        // left, or another admin removed them. A 403: the viewer is no longer an admin.
        if (status === 404 || status === 403) {
          this.removing.set(null);
          this.removeNotice.set({
            key: status === 404 ? 'teams.detail.removeGone' : 'teams.detail.removeForbidden',
            name: this.memberName(member),
          });
          // Focus the note that says why: the button that asked may be gone with the reload.
          this.load(() => this.focus('[data-testid="remove-notice"]'));
          return;
        }
        // The dialog stays open; confirming again is the retry (never automatic).
        this.removeError.set('teams.detail.removeFailed');
      },
    });
  }

  /** The name the roster shows for a teammate. */
  protected memberName(member: TeamMember): string {
    return member.displayName || member.handle;
  }

  protected positions(pompfen: Pompfe[]): string {
    return pompfen.map((p) => this.transloco.translate(pompfeLabelKey(p))).join(' · ');
  }

  /**
   * First letter for an avatar fallback, null-safe. The roster DTO can hand back a
   * null name for a member whose account has no profile row (an EF LEFT-JOIN projection
   * yields null despite the non-null type). Calling `.charAt` on that threw during change
   * detection and — because the app is zoneless — aborted the whole tick, which silently
   * broke unrelated UI on the page (e.g. the account menu wouldn't open). Coalesce instead.
   */
  protected initial(name: string | null | undefined): string {
    return (name?.trim()?.charAt(0) || '?').toUpperCase();
  }

  protected avatarUrl(handle: string): string {
    return `/api/v1/profiles/${encodeURIComponent(handle)}/avatar`;
  }

  /** Feature 051 — through the service, so an admin's own upload busts the cached image. */
  protected logoUrl(slug: string): string {
    return this.teams.logoUrl(slug);
  }

  /** Feature 061 — the site each link leads to, shown beside its label (FR-017). */
  protected readonly linkHost = linkHost;

  /** Public roster rows for the non-member view. */
  protected readonly publicRoster = computed<PublicMember[]>(() => this.pub()?.roster ?? []);
}

/** The header's two join actions: each opens the confirmation, and takes the focus back when it closes. */
const REQUEST_BUTTON = '[data-testid="request-to-join"]';
const CANCEL_BUTTON = '[data-testid="cancel-request"]';

/**
 * A join-request answer came back 404: the request no longer waits (feature 058). Branch on the
 * status, never the message — the server's text is English in every language.
 */
function isGone(err: unknown): boolean {
  return err instanceof HttpErrorResponse && err.status === 404;
}
