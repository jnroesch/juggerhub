import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ButtonDirective, EmptyStateComponent } from '../../../shared/ui';
import { Pompfe, pompfeLabel } from '../../../shared/pompfen.catalog';
import {
  JoinRequest,
  PublicMember,
  TeamMember,
  TeamNews,
  TeamPublicDetail,
} from '../../../core/models/team.models';
import { TeamService } from '../../../core/services/team.service';
import { PartyService } from '../../../core/services/party.service';
import { PartyRequestCard } from '../../../core/models/party.models';
import { problemDetail } from '../../../core/utils/problem';
import { RecognitionDisplayComponent } from '../../profile/components/recognition-display/recognition-display.component';

/**
 * The team page (feature 009). Public to everyone: overview, roster (names + positions),
 * recent activity, and upcoming trainings, plus a state-aware request-to-join action. Members
 * additionally see the news feed; admins additionally see the join-request queue and the roster
 * admin controls + team tools. The viewer's relation is decided server-side.
 */
@Component({
  selector: 'jh-team-detail',
  imports: [RouterLink, DatePipe, RecognitionDisplayComponent, ButtonDirective, EmptyStateComponent],
  templateUrl: './team-detail.component.html',
  styleUrl: './team-detail.component.css',
})
export class TeamDetailComponent {
  private readonly teams = inject(TeamService);
  private readonly parties = inject(PartyService);
  private readonly route = inject(ActivatedRoute);

  protected readonly slug = signal('');
  protected readonly pub = signal<TeamPublicDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly error = signal<string | null>(null);

  // Members/admins load the full roster (with ids + admin menu) + news; admins load the queue.
  protected readonly members = signal<TeamMember[]>([]);
  protected readonly news = signal<TeamNews[]>([]);
  protected readonly joinRequests = signal<JoinRequest[]>([]);
  // Feature 016: pinned party-request cards a member can answer.
  protected readonly partyRequests = signal<PartyRequestCard[]>([]);
  protected readonly partyBusy = signal(false);
  protected readonly openMenu = signal<string | null>(null);

  protected readonly requestBusy = signal(false);

  protected readonly relation = computed(() => this.pub()?.viewerRelation ?? 'Anonymous');
  protected readonly isMember = computed(() => this.relation() === 'Member' || this.relation() === 'Admin');
  protected readonly isAdmin = computed(() => this.relation() === 'Admin');
  protected readonly isAnon = computed(() => this.relation() === 'Anonymous');
  protected readonly canRequest = computed(() => this.relation() === 'NonMember');
  protected readonly requested = computed(() => this.relation() === 'Requested');

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
    this.joinRequests.set([]);

    this.teams.getPublicDetail(this.slug()).subscribe({
      next: (d) => {
        this.pub.set(d);
        this.loading.set(false);
        if (d.viewerRelation === 'Member' || d.viewerRelation === 'Admin') {
          this.loadMembers();
          this.loadNews();
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

  protected requestToJoin(): void {
    if (this.requestBusy()) {
      return;
    }
    this.requestBusy.set(true);
    this.error.set(null);
    this.teams.requestToJoin(this.slug()).subscribe({
      next: () => {
        this.requestBusy.set(false);
        this.load(); // relation → Requested
      },
      error: (err) => {
        this.requestBusy.set(false);
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
    return pompfen.map((p) => pompfeLabel(p)?.de ?? p).join(' · ');
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
