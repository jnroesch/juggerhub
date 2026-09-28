import { TranslocoDatePipe } from '@jsverse/transloco-locale';
import { Component, HostListener, computed, inject, signal } from '@angular/core';
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
import { MembershipService } from '../../../core/services/membership.service';
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
 *
 * GH #361: a member leaves the team from here (the "Your membership" card in the right rail) —
 * settings is admin-only, so the team page is the one member-facing home for it. The last-admin
 * guard is the server's (`MutateMembershipAsync`); the sole admin sees an explanation instead of
 * a button that would only ever answer 409.
 */

/** The three actions the page's confirmation modal gates, with the copy each one shows. */
type ConfirmIntent = 'join' | 'cancel' | 'leave';

interface ConfirmCopy {
  title: string;
  body: string;
  submit: string;
  dismiss: string;
}

const CONFIRM_COPY: Record<ConfirmIntent, ConfirmCopy> = {
  join: {
    title: 'teams.detail.confirmJoinTitle',
    body: 'teams.detail.confirmJoinBody',
    submit: 'teams.detail.confirmJoinSubmit',
    dismiss: 'teams.detail.dismiss',
  },
  cancel: {
    title: 'teams.detail.confirmCancelTitle',
    body: 'teams.detail.confirmCancelBody',
    submit: 'teams.detail.confirmCancelSubmit',
    dismiss: 'teams.detail.keepRequest',
  },
  leave: {
    title: 'teams.detail.confirmLeaveTitle',
    body: 'teams.detail.confirmLeaveBody',
    submit: 'teams.detail.leaveTeam',
    dismiss: 'teams.detail.stayInTeam',
  },
};
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
  private readonly membership = inject(MembershipService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);

  protected readonly slug = signal('');
  protected readonly pub = signal<TeamPublicDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly error = signal<string | null>(null);

  // Members/admins load the full roster (with ids + admin menu) + news; admins load the queue.
  protected readonly members = signal<TeamMember[]>([]);
  /**
   * Whether the roster has arrived. The leave control for an ADMIN depends on how many admins the
   * roster holds, and `members()` is empty until it loads — without this flag the sole-admin
   * explanation would flash for every admin on every load.
   */
  protected readonly membersLoaded = signal(false);
  protected readonly news = signal<TeamNews[]>([]);
  /** Feature 044 — the team-internal "What's happening" feed (members only). */
  protected readonly happenings = signal<TeamHappening[]>([]);
  protected readonly joinRequests = signal<JoinRequest[]>([]);
  // Feature 016: pinned party-request cards a member can answer.
  protected readonly partyRequests = signal<PartyRequestCard[]>([]);
  protected readonly partyBusy = signal(false);
  protected readonly openMenu = signal<string | null>(null);

  protected readonly requestBusy = signal(false);
  /** Which action the confirmation modal is currently gating (null = closed). */
  protected readonly confirmIntent = signal<ConfirmIntent | null>(null);
  /** The copy keys for the open confirmation — the template reads one record, not three ternaries. */
  protected readonly confirmCopy = computed<ConfirmCopy | null>(() => {
    const intent = this.confirmIntent();
    return intent ? CONFIRM_COPY[intent] : null;
  });

  protected readonly relation = computed(() => this.pub()?.viewerRelation ?? 'Anonymous');
  protected readonly isMember = computed(() => this.relation() === 'Member' || this.relation() === 'Admin');
  protected readonly isAdmin = computed(() => this.relation() === 'Admin');
  protected readonly isAnon = computed(() => this.relation() === 'Anonymous');
  protected readonly canRequest = computed(() => this.relation() === 'NonMember');
  protected readonly requested = computed(() => this.relation() === 'Requested');
  /** Feature 027: any signed-in non-admin may contact the team's admins (FR-001/FR-002). */
  protected readonly canContactAdmins = computed(() => !this.isAnon() && !this.isAdmin());

  private readonly adminCount = computed(() => this.members().filter((m) => m.role === 'Admin').length);
  /**
   * GH #361 — the viewer is the team's only admin, so the server would refuse a leave (409
   * LastAdmin, the same guard as step-down). Known only once the roster is in.
   */
  protected readonly soleAdmin = computed(() => this.isAdmin() && this.membersLoaded() && this.adminCount() <= 1);
  /**
   * GH #361 — a non-admin member may always leave; an admin only once the roster shows another
   * admin. Until the roster loads an admin sees neither the button nor the explanation.
   */
  protected readonly canLeave = computed(
    () => this.isMember() && (!this.isAdmin() || (this.membersLoaded() && this.adminCount() > 1)),
  );

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
      this.load();
    });
  }

  private load(): void {
    this.loading.set(true);
    this.notFound.set(false);
    this.members.set([]);
    this.membersLoaded.set(false);
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
    this.teams.getMembers(this.slug()).subscribe({
      next: (p) => {
        this.members.set(p.items);
        this.membersLoaded.set(true);
      },
    });
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

  /** Open the confirmation modal for an action (feature 009 — guards accidental clicks). */
  protected askConfirm(intent: ConfirmIntent): void {
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
    } else if (this.confirmIntent() === 'leave') {
      this.leaveTeam();
    }
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    if (this.confirmIntent()) {
      this.dismissConfirm();
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

  /**
   * GH #361 — leave the team: the member removes THEMSELVES through the same endpoint an admin
   * removes anyone with (`DELETE /teams/{slug}/members/{userId}`; the server allows self-removal
   * for a plain member). On success the cached memberships are refreshed (023 FR-017) so the nav
   * and the team chooser stop naming this team, then the player lands on "My team".
   */
  private leaveTeam(): void {
    const userId = this.auth.currentUser()?.id;
    if (this.requestBusy() || !userId) {
      return;
    }
    this.requestBusy.set(true);
    this.error.set(null);
    this.teams.removeMember(this.slug(), userId).subscribe({
      next: () => {
        this.membership.load();
        void this.router.navigate(['/my-team']);
      },
      error: (err) => {
        this.requestBusy.set(false);
        this.confirmIntent.set(null);
        this.error.set(problemDetail(err));
        // A 409 here means the other admin stepped down or left while the modal was open — the
        // roster decides whether the button or the sole-admin explanation is right now.
        this.loadMembers();
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
