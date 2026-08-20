import { TranslocoDatePipe } from '@jsverse/transloco-locale';
import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ButtonDirective, EmptyStateComponent, CardComponent } from '../../../shared/ui';
import { Pompfe, pompfeLabelKey } from '../../../shared/pompfen.catalog';
import {
  JoinRequest,
  PublicMember,
  TeamHappening,
  TeamMember,
  TeamNews,
  TeamPublicDetail,
} from '../../../core/models/team.models';
import { TeamService } from '../../../core/services/team.service';
import { PartyService } from '../../../core/services/party.service';
import { PartyRequestCard } from '../../../core/models/party.models';
import { problemDetail } from '../../../core/utils/problem';
import { RecognitionDisplayComponent } from '../../profile/components/recognition-display/recognition-display.component';
import { TeamHappeningsComponent } from './happenings/team-happenings.component';
import { SHOWCASE_MAX_IMAGES, ShowcaseGalleryComponent, ShowcaseManagerComponent } from '../../../shared/showcase';
import { ShowcaseImage } from '../../../core/models/showcase.models';
import { ShowcaseService } from '../../../core/services/showcase.service';

/**
 * The team page (feature 009). Public to everyone: overview, roster (names + positions),
 * recent activity, and upcoming trainings, plus a state-aware request-to-join action. Members
 * additionally see the news feed; admins additionally see the join-request queue and the roster
 * admin controls + team tools. The viewer's relation is decided server-side.
 */
@Component({
  selector: 'jh-team-detail',
  imports: [RouterLink, TranslocoDatePipe, RecognitionDisplayComponent, TeamHappeningsComponent, ShowcaseGalleryComponent, ShowcaseManagerComponent, ButtonDirective, EmptyStateComponent, CardComponent, TranslocoPipe],
  templateUrl: './team-detail.component.html',
  styleUrl: './team-detail.component.css',
})
export class TeamDetailComponent {
  private readonly teams = inject(TeamService);
  private readonly parties = inject(PartyService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);
  private readonly showcase = inject(ShowcaseService);

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

  /**
   * The team's showcase gallery (feature 046). Read by every signed-in viewer; managed only by
   * admins, whose controls are a separate component that is not even instantiated for anyone
   * else.
   */
  protected readonly showcaseImages = signal<ShowcaseImage[]>([]);
  protected readonly showcaseLoading = signal(false);
  protected readonly showcaseError = signal<string | null>(null);

  protected readonly showcaseOwner = computed(() => ({ kind: 'team', slug: this.slug() }) as const);

  protected readonly maxShowcaseImages = SHOWCASE_MAX_IMAGES;

  /** An admin looking at an empty gallery gets the invitation, not a bare heading. */
  protected readonly showcaseEmptyForAdmin = computed(
    () =>
      this.isAdmin() &&
      !this.showcaseLoading() &&
      this.showcaseError() === null &&
      this.showcaseImages().length === 0,
  );

  /** Hidden entirely for a viewer who cannot add pictures when there are none (spec FR-026). */
  protected readonly showShowcase = computed(
    () =>
      this.isAdmin() ||
      this.showcaseLoading() ||
      this.showcaseError() !== null ||
      this.showcaseImages().length > 0,
  );

  /** Admins switch the card between looking at the gallery and changing it. */
  protected readonly showcaseManaging = signal(false);

  protected toggleShowcaseManaging(): void {
    this.showcaseManaging.update((managing) => !managing);
  }

  protected onShowcaseChanged(images: ShowcaseImage[]): void {
    this.showcaseImages.set(images);
  }

  protected readonly reloadShowcase = (): void => {
    const slug = this.slug();
    if (!slug) {
      return;
    }

    this.showcaseLoading.set(true);
    this.showcaseError.set(null);
    this.showcase.list({ kind: 'team', slug }).subscribe({
      next: (images) => {
        this.showcaseImages.set(images);
        this.showcaseLoading.set(false);
      },
      error: () => {
        this.showcaseError.set(this.transloco.translate('showcase.loadFailed'));
        this.showcaseLoading.set(false);
      },
    });
  };

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
    this.news.set([]);
    this.happenings.set([]);
    this.joinRequests.set([]);
    this.showcaseImages.set([]);

    this.teams.getPublicDetail(this.slug()).subscribe({
      next: (d) => {
        this.pub.set(d);
        this.loading.set(false);
        // The gallery is visible to every signed-in viewer, member or not (spec FR-020), so it
        // is loaded outside the membership branches below.
        this.reloadShowcase();
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

  /** Public roster rows for the non-member view. */
  protected readonly publicRoster = computed<PublicMember[]>(() => this.pub()?.roster ?? []);
}
