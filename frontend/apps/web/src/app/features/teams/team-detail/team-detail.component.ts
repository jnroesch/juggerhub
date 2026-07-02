import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Pompfe, pompfeLabel } from '../../../shared/pompfen.catalog';
import { ActivityItem, TeamDetail, TeamMember, TeamNews } from '../../../core/models/team.models';
import { TeamService } from '../../../core/services/team.service';
import { problemDetail } from '../../../core/utils/problem';

type Tab = 'members' | 'activity' | 'news';

/**
 * US2 — the team space (members-only). A cover header + underline tabs: Members
 * (roster with role tags + profile positions and an admin "…" menu), Activity
 * (events the team played), News (read-only feed). Trainings is disabled (later).
 * Non-members get a friendly not-found (the API returns 404).
 */
@Component({
  selector: 'jh-team-detail',
  imports: [RouterLink],
  templateUrl: './team-detail.component.html',
  styleUrl: './team-detail.component.css',
})
export class TeamDetailComponent {
  private readonly teams = inject(TeamService);
  private readonly route = inject(ActivatedRoute);

  protected readonly slug = signal('');
  protected readonly detail = signal<TeamDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly tab = signal<Tab>('members');
  protected readonly members = signal<TeamMember[]>([]);
  protected readonly activity = signal<ActivityItem[]>([]);
  protected readonly news = signal<TeamNews[]>([]);
  protected readonly tabLoading = signal(false);
  protected readonly openMenu = signal<string | null>(null);

  protected readonly isAdmin = computed(() => this.detail()?.myRole === 'Admin');

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((pm) => {
      const slug = pm.get('slug') ?? '';
      this.slug.set(slug);
      this.load();
    });
  }

  private load(): void {
    this.loading.set(true);
    this.notFound.set(false);
    this.members.set([]);
    this.teams.getDetail(this.slug()).subscribe({
      next: (d) => {
        this.detail.set(d);
        this.loading.set(false);
        this.selectTab('members');
      },
      error: () => {
        this.loading.set(false);
        this.notFound.set(true);
      },
    });
  }

  protected selectTab(tab: Tab): void {
    this.tab.set(tab);
    this.openMenu.set(null);
    if (tab === 'members') {
      this.loadMembers();
    } else if (tab === 'activity') {
      this.loadActivity();
    } else {
      this.loadNews();
    }
  }

  private loadMembers(): void {
    this.tabLoading.set(true);
    this.teams.getMembers(this.slug()).subscribe({
      next: (p) => {
        this.members.set(p.items);
        this.tabLoading.set(false);
      },
      error: () => this.tabLoading.set(false),
    });
  }

  private loadActivity(): void {
    this.tabLoading.set(true);
    this.teams.getActivity(this.slug()).subscribe({
      next: (p) => {
        this.activity.set(p.items);
        this.tabLoading.set(false);
      },
      error: () => this.tabLoading.set(false),
    });
  }

  private loadNews(): void {
    this.tabLoading.set(true);
    this.teams.getNews(this.slug()).subscribe({
      next: (p) => {
        this.news.set(p.items);
        this.tabLoading.set(false);
      },
      error: () => this.tabLoading.set(false),
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
        this.loadMembers();
        this.refreshCount();
      },
      error: (err) => this.error.set(problemDetail(err)),
    });
  }

  private refreshCount(): void {
    this.teams.getDetail(this.slug()).subscribe({ next: (d) => this.detail.set(d) });
  }

  protected positions(member: TeamMember): string {
    return member.pompfen.map((p: Pompfe) => pompfeLabel(p)?.de ?? p).join(' · ');
  }

  protected avatarUrl(handle: string): string {
    return `/api/v1/profiles/${encodeURIComponent(handle)}/avatar`;
  }
}
