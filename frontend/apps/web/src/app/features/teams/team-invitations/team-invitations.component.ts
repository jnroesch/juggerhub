import { Component, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AlertComponent, ButtonDirective, IconComponent, LoadingComponent } from '../../../shared/ui';
import { TeamInvitation } from '../../../core/models/team.models';
import { TeamService } from '../../../core/services/team.service';
import { InviteLinkComponent } from '../components/invite-link/invite-link.component';
import { InviteSearchComponent } from '../components/invite-search/invite-search.component';
import { problemDetail } from '../../../core/utils/problem';
import { expiresIn } from '../invitation-expiry';

/**
 * US3 — invite people. The single reusable invite link (copy / regenerate / revoke),
 * the pending-invite list, and a user search to invite players directly (emailed).
 * Admin-only; the API returns 403/404 for anyone else.
 *
 * The link and the search are `jh-invite-link` and `jh-invite-search` (feature 052), which the
 * create wizard's last step also renders. This screen keeps everything around them — the pending
 * list, the revoking, the navigation — and learns that either of them changed something from
 * their outputs, which is why {@link reload} is the handler rather than something they call back
 * into.
 */
@Component({
  selector: 'jh-team-invitations',
  imports: [RouterLink, ButtonDirective, LoadingComponent, AlertComponent, TranslocoPipe, IconComponent, InviteLinkComponent, InviteSearchComponent],
  templateUrl: './team-invitations.component.html',
  styleUrl: './team-invitations.component.css',
})
export class TeamInvitationsComponent {
  private readonly teams = inject(TeamService);
  private readonly route = inject(ActivatedRoute);
  private readonly t = inject(TranslocoService);

  protected readonly slug = signal('');
  protected readonly pending = signal<TeamInvitation[]>([]);
  protected readonly loading = signal(true);
  protected readonly denied = signal(false);
  protected readonly error = signal<string | null>(null);

  /** The link block, so that revoking the link's own pending row can send it back to the server. */
  private readonly inviteLink = viewChild(InviteLinkComponent);

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
  }

  /**
   * Re-read the pending list after one of the extracted components changed it: a targeted invite
   * adds a row, and a new shared link both adds one and retires the row of the link it replaced.
   */
  protected reload(): void {
    this.teams.getInvitations(this.slug()).subscribe({ next: (p) => this.pending.set(p.items) });
  }

  protected revoke(id: string): void {
    this.error.set(null);
    this.teams.revokeInvite(this.slug(), id).subscribe({
      next: () => {
        this.reload();
        // The revoked row may have been the shared link itself, which the block beside the list
        // would otherwise go on offering long after it stopped admitting anyone.
        this.inviteLink()?.reload();
      },
      error: (err) => this.error.set(problemDetail(err)),
    });
  }

  /** Shared with `jh-invite-link`, so a pending row and the link block date themselves alike. */
  protected expiresIn(iso: string): string {
    return expiresIn(iso, this.t);
  }
}
