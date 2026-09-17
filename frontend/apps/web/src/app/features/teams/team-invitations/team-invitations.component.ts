import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AlertComponent, ButtonDirective, IconComponent, LoadingComponent } from '../../../shared/ui';
import { InviteLink, TeamInvitation } from '../../../core/models/team.models';
import { TeamService } from '../../../core/services/team.service';
import { InviteSearchComponent } from '../components/invite-search/invite-search.component';
import { problemDetail } from '../../../core/utils/problem';

/**
 * US3 — invite people. The single reusable invite link (copy / regenerate / revoke),
 * the pending-invite list, and a user search to invite players directly (emailed).
 * Admin-only; the API returns 403/404 for anyone else.
 *
 * The search itself lives in `jh-invite-search` (feature 052), which the create wizard's last
 * step also renders. This screen keeps everything around it and learns that an invitation was
 * created from the component's `invited` output, which is why {@link reload} is the handler
 * rather than something the search calls back into.
 */
@Component({
  selector: 'jh-team-invitations',
  imports: [RouterLink, ButtonDirective, LoadingComponent, AlertComponent, TranslocoPipe, IconComponent, InviteSearchComponent],
  templateUrl: './team-invitations.component.html',
  styleUrl: './team-invitations.component.css',
})
export class TeamInvitationsComponent {
  private readonly teams = inject(TeamService);
  private readonly route = inject(ActivatedRoute);
  private readonly t = inject(TranslocoService);

  protected readonly slug = signal('');
  protected readonly link = signal<InviteLink | null>(null);
  protected readonly pending = signal<TeamInvitation[]>([]);
  protected readonly copied = signal(false);
  protected readonly loading = signal(true);
  protected readonly denied = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((pm) => {
      this.slug.set(pm.get('slug') ?? '');
      this.load();
    });
  }

  private load(): void {
    this.loading.set(true);
    this.denied.set(false);
    this.teams.getInvitations(this.slug()).subscribe({
      next: (p) => {
        this.pending.set(p.items);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.denied.set(true);
      },
    });
    this.teams.getInviteLink(this.slug()).subscribe({ next: (l) => this.link.set(l) });
  }

  /** Re-read what the extracted search just changed: the pending list, and the link's state. */
  protected reload(): void {
    this.teams.getInvitations(this.slug()).subscribe({ next: (p) => this.pending.set(p.items) });
    this.teams.getInviteLink(this.slug()).subscribe({ next: (l) => this.link.set(l) });
  }

  protected copyLink(): void {
    const l = this.link();
    if (!l) {
      return;
    }
    navigator.clipboard?.writeText(l.url);
    this.copied.set(true);
    setTimeout(() => this.copied.set(false), 1500);
  }

  protected rotate(): void {
    this.error.set(null);
    this.teams.rotateInviteLink(this.slug()).subscribe({
      next: (l) => {
        this.link.set(l);
        this.reload();
      },
      error: (err) => this.error.set(problemDetail(err)),
    });
  }

  protected revoke(id: string): void {
    this.error.set(null);
    this.teams.revokeInvite(this.slug(), id).subscribe({
      next: () => this.reload(),
      error: (err) => this.error.set(problemDetail(err)),
    });
  }

  protected expiresIn(iso: string): string {
    const days = Math.ceil((new Date(iso).getTime() - Date.now()) / 86_400_000);
    if (days <= 0) {
      return this.t.translate('teams.invitations.expiresToday');
    }
    if (days === 1) {
      return this.t.translate('teams.invitations.expiresTomorrow');
    }
    return this.t.translate('teams.invitations.expiresInDays', { count: days });
  }
}
