import { TranslocoDatePipe } from '@jsverse/transloco-locale';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, HostListener, Injector, afterNextRender, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { PluralKeyPipe } from '../../../core/i18n/plural-key.pipe';
import { ButtonDirective, CardComponent, ChipDirective, EmptyStateComponent, IconComponent, LoadingComponent } from '../../../shared/ui';
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
import { TeamService } from '../../../core/services/team.service';
import { PartyService } from '../../../core/services/party.service';
import { PartyRequestCard } from '../../../core/models/party.models';
import { problemDetail } from '../../../core/utils/problem';
import { RecognitionDisplayComponent } from '../../profile/components/recognition-display/recognition-display.component';
import { TeamHappeningsComponent } from './happenings/team-happenings.component';
import { TeamPlacementsComponent } from './placements/team-placements.component';

/**
 * The team page (feature 009). Public to everyone: overview, roster (names + positions),
 * recent activity, and upcoming trainings, plus a state-aware request-to-join action. Members
 * additionally see the news feed; admins additionally see the join-request queue and the roster
 * admin controls + team tools. The viewer's relation is decided server-side.
 */
@Component({
  selector: 'jh-team-detail',
  imports: [LoadingComponent, RouterLink, TranslocoDatePipe, RecognitionDisplayComponent, TeamHappeningsComponent, TeamPlacementsComponent, ButtonDirective, ChipDirective, EmptyStateComponent, CardComponent, TranslocoPipe, PluralKeyPipe, IconComponent],
  templateUrl: './team-detail.component.html',
  styleUrl: './team-detail.component.css',
})
export class TeamDetailComponent {
  private readonly teams = inject(TeamService);
  private readonly parties = inject(PartyService);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly slug = signal('');
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

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((pm) => {
      this.slug.set(pm.get('slug') ?? '');
      // The router reuses this component from one team to the next. An editor left open on the
      // previous team would otherwise keep every post menu here disabled (feature 057). Only on
      // a switch, not in load(): approving a join request reloads too, mid-edit.
      this.newsMenu.set(null);
      this.editingNewsId.set(null);
      this.newsNotice.set(null);
      this.deleteNewsTarget.set(null);
      this.load();
    });
  }

  private load(): void {
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

  /** The post whose menu is open, if any. */
  protected readonly newsMenu = signal<string | null>(null);
  /** The post being edited in place, if any. While one is open, no other menu opens. */
  protected readonly editingNewsId = signal<string | null>(null);
  protected readonly newsDraft = signal('');
  protected readonly savingNews = signal(false);
  /** Translation keys, not text, so a language switch re-renders them. */
  protected readonly newsEditError = signal<string | null>(null);
  protected readonly newsNotice = signal<string | null>(null);

  protected toggleNewsMenu(id: string): void {
    this.newsMenu.update((open) => (open === id ? null : id));
  }

  protected startEdit(post: TeamNews): void {
    this.newsMenu.set(null);
    this.newsNotice.set(null);
    this.newsEditError.set(null);
    this.newsDraft.set(post.body);
    this.editingNewsId.set(post.id);
    // Zoneless: the textarea exists only after the next render (GH #344's lesson — not an effect).
    afterNextRender(() => this.focus('[data-testid="news-edit-input"]'), { injector: this.injector });
  }

  protected cancelEdit(): void {
    const id = this.editingNewsId();
    this.editingNewsId.set(null);
    this.newsEditError.set(null);
    if (id) {
      afterNextRender(() => this.focus(`[data-news-menu-trigger="${id}"]`), { injector: this.injector });
    }
  }

  protected saveEdit(post: TeamNews): void {
    const body = this.newsDraft().trim();
    if (body.length === 0 || this.savingNews()) {
      return;
    }
    if (body === post.body) {
      // Nothing changed, so there is nothing to send (FR-003).
      this.cancelEdit();
      return;
    }
    this.savingNews.set(true);
    this.newsEditError.set(null);
    this.teams.editNews(this.slug(), post.id, body).subscribe({
      next: (updated) => {
        this.news.update((list) => list.map((n) => (n.id === updated.id ? updated : n)));
        this.savingNews.set(false);
        this.cancelEdit();
      },
      error: (err) => {
        this.savingNews.set(false);
        if (isGone(err)) {
          this.dropNews(post.id);
          return;
        }
        // The editor stays open with the typed text (FR-020). Our own sentence, never the server's
        // English `detail` (GH #179).
        this.newsEditError.set('teams.detail.newsSaveFailed');
      },
    });
  }

  /** The post the delete dialog is asking about, if it is open. */
  protected readonly deleteNewsTarget = signal<TeamNews | null>(null);
  protected readonly deletingNews = signal(false);
  protected readonly newsDeleteError = signal<string | null>(null);

  protected askDeleteNews(post: TeamNews): void {
    this.newsMenu.set(null);
    this.newsNotice.set(null);
    this.newsDeleteError.set(null);
    this.deleteNewsTarget.set(post);
    // The safe answer takes focus, so Enter on arrival keeps the post.
    afterNextRender(() => this.focus('[data-testid="news-delete-keep"]'), { injector: this.injector });
  }

  protected dismissDeleteNews(): void {
    const target = this.deleteNewsTarget();
    if (!target || this.deletingNews()) {
      return;
    }
    this.deleteNewsTarget.set(null);
    this.newsDeleteError.set(null);
    afterNextRender(() => this.focus(`[data-news-menu-trigger="${target.id}"]`), { injector: this.injector });
  }

  protected confirmDeleteNews(): void {
    const target = this.deleteNewsTarget();
    if (!target || this.deletingNews()) {
      return;
    }
    this.deletingNews.set(true);
    this.newsDeleteError.set(null);
    this.teams.deleteNews(this.slug(), target.id).subscribe({
      next: () => {
        this.deletingNews.set(false);
        this.deleteNewsTarget.set(null);
        this.news.update((list) => list.filter((n) => n.id !== target.id));
        // The button that opened the menu went with the post; land on the list's heading instead.
        afterNextRender(() => this.focus('#team-news-heading'), { injector: this.injector });
      },
      error: (err) => {
        this.deletingNews.set(false);
        if (isGone(err)) {
          this.deleteNewsTarget.set(null);
          this.dropNews(target.id);
          afterNextRender(() => this.focus('#team-news-heading'), { injector: this.injector });
          return;
        }
        // The dialog stays open; confirming again is the retry (never automatic).
        this.newsDeleteError.set('teams.detail.newsDeleteFailed');
      },
    });
  }

  /** Keep Tab inside the open dialog: `aria-modal` promises that the page behind it is inert. */
  protected trapTab(event: Event): void {
    const key = event as KeyboardEvent;
    const buttons = Array.from(
      (key.currentTarget as HTMLElement).querySelectorAll<HTMLElement>('button:not([disabled])'),
    );
    if (buttons.length === 0) {
      return;
    }
    const first = buttons[0];
    const last = buttons[buttons.length - 1];
    if (key.shiftKey && document.activeElement === first) {
      last.focus();
      key.preventDefault();
    } else if (!key.shiftKey && document.activeElement === last) {
      first.focus();
      key.preventDefault();
    }
  }

  /** Another admin removed the post meanwhile: take it off the list and say so (FR-019). */
  private dropNews(id: string): void {
    this.news.update((list) => list.filter((n) => n.id !== id));
    if (this.editingNewsId() === id) {
      this.editingNewsId.set(null);
    }
    this.newsNotice.set('teams.detail.newsGone');
  }

  private focus(selector: string): void {
    this.host.nativeElement.querySelector<HTMLElement>(selector)?.focus();
  }

  /** A click anywhere outside a post's menu closes it. */
  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: MouseEvent): void {
    if (this.newsMenu() && !(event.target as Element | null)?.closest('[data-news-menu]')) {
      this.newsMenu.set(null);
    }
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

  /** Open the confirmation modal for a join action (feature 009 — guards accidental clicks). */
  protected askConfirm(intent: 'join' | 'cancel'): void {
    if (this.requestBusy()) {
      return;
    }
    this.confirmIntent.set(intent);
  }

  protected dismissConfirm(): void {
    this.confirmIntent.set(null);
  }

  /** Run the action the confirmation modal is gating. */
  protected confirmAction(): void {
    if (this.confirmIntent() === 'join') {
      this.requestToJoin();
    } else if (this.confirmIntent() === 'cancel') {
      this.cancelRequest();
    }
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    if (this.confirmIntent()) {
      this.dismissConfirm();
    }
    if (this.deleteNewsTarget()) {
      this.dismissDeleteNews();
    }
    const menu = this.newsMenu();
    if (menu) {
      // Back to the button that opened it, or a keyboard user is left on the page body.
      this.newsMenu.set(null);
      this.focus(`[data-news-menu-trigger="${menu}"]`);
    }
  }

  private requestToJoin(): void {
    if (this.requestBusy()) {
      return;
    }
    this.requestBusy.set(true);
    this.error.set(null);
    this.teams.requestToJoin(this.slug()).subscribe({
      next: () => {
        this.requestBusy.set(false);
        this.confirmIntent.set(null);
        this.load(); // relation → Requested
      },
      error: (err) => {
        this.requestBusy.set(false);
        this.confirmIntent.set(null);
        this.error.set(problemDetail(err));
      },
    });
  }

  /** Feature 009 — withdraw the caller's own pending request; relation → NonMember. */
  private cancelRequest(): void {
    if (this.requestBusy()) {
      return;
    }
    this.requestBusy.set(true);
    this.error.set(null);
    this.teams.cancelJoinRequest(this.slug()).subscribe({
      next: () => {
        this.requestBusy.set(false);
        this.confirmIntent.set(null);
        this.load(); // relation → NonMember
      },
      error: (err) => {
        this.requestBusy.set(false);
        this.confirmIntent.set(null);
        this.error.set(problemDetail(err));
      },
    });
  }

  protected approve(request: JoinRequest): void {
    this.teams.approveJoinRequest(this.slug(), request.id).subscribe({
      next: () => this.load(),
      error: (err) => this.error.set(problemDetail(err)),
    });
  }

  protected decline(request: JoinRequest): void {
    this.teams.declineJoinRequest(this.slug(), request.id).subscribe({
      next: () => this.loadJoinRequests(),
      error: (err) => this.error.set(problemDetail(err)),
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

  protected remove(member: TeamMember): void {
    this.error.set(null);
    this.teams.removeMember(this.slug(), member.userId).subscribe({
      next: () => {
        this.openMenu.set(null);
        this.load();
      },
      error: (err) => this.error.set(problemDetail(err)),
    });
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

  /** Public roster rows for the non-member view. */
  protected readonly publicRoster = computed<PublicMember[]>(() => this.pub()?.roster ?? []);
}

/**
 * A news edit or delete answered 404: the post is gone (or the viewer no longer has access).
 * Branch on the status, never the message — the server's text is English in every language.
 */
function isGone(err: unknown): boolean {
  return err instanceof HttpErrorResponse && err.status === 404;
}
