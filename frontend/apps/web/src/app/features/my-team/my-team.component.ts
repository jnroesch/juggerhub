import { Component, OnInit, effect, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MembershipService } from '../../core/services/membership.service';
import { InvitationService } from '../../core/services/invitation.service';
import { TeamService } from '../../core/services/team.service';
import { MyInvitation } from '../../core/models/team.models';

/**
 * The "My team" destination (features 008 + 023). Its shape follows the caller's memberships:
 * a player on more than one team gets the chooser; a **teamless** player gets the home/empty state
 * (feature 023) — their pending invitations to accept/decline, plus finding or creating a team.
 * (0/1-team players are routed here or straight to their team by the nav; this component also
 * degrades gracefully when reached directly.)
 */
@Component({
  selector: 'jh-my-team',
  imports: [RouterLink],
  templateUrl: './my-team.component.html',
  styleUrl: './my-team.component.css',
})
export class MyTeamComponent implements OnInit {
  private readonly membership = inject(MembershipService);
  private readonly invitations = inject(InvitationService);
  private readonly teamApi = inject(TeamService);
  private readonly router = inject(Router);

  protected readonly teams = this.membership.teams;
  protected readonly loaded = this.membership.loaded;

  /** The teamless player's pending invitations (feature 023). */
  protected readonly invites = signal<MyInvitation[]>([]);
  /** A friendly notice, e.g. when an invitation went stale before it could be acted on. */
  protected readonly notice = signal<string | null>(null);
  private invitesRequested = false;

  constructor() {
    // Once memberships are known and the player is teamless, load their pending invitations once.
    effect(() => {
      if (this.loaded() && this.teams().length === 0 && !this.invitesRequested) {
        this.invitesRequested = true;
        this.loadInvites();
      }
    });
  }

  ngOnInit(): void {
    if (!this.membership.loaded()) {
      this.membership.load();
    }
  }

  private loadInvites(): void {
    this.invitations.listMine().subscribe({
      next: (page) => this.invites.set(page.items),
      // Transient/not-signed-in: leave the invites section empty; find/create still work.
      error: () => this.invites.set([]),
    });
  }

  /** Accept an invitation: join, refresh the nav's team cache, and land in the joined team's space. */
  protected accept(inv: MyInvitation): void {
    this.notice.set(null);
    this.teamApi.acceptInvite(inv.token).subscribe({
      next: (r) => {
        // Joining changed this player's teams — refresh the cache the nav's "My team" target reads,
        // then navigate into the team just joined (FR-017, FR-018).
        this.membership.load();
        this.router.navigateByUrl(`/t/${r.teamSlug}`);
      },
      // Expired/revoked/consumed since load — reconcile the row and tell the player, never error raw.
      error: () => {
        this.removeInvite(inv.token);
        this.notice.set(`That invitation to ${inv.teamName} is no longer available.`);
      },
    });
  }

  /** Decline an invitation: drop it from the list (declining a stale invite is also a no-op success). */
  protected decline(inv: MyInvitation): void {
    this.notice.set(null);
    this.teamApi.declineInvite(inv.token).subscribe({
      next: () => this.removeInvite(inv.token),
      error: () => this.removeInvite(inv.token),
    });
  }

  private removeInvite(token: string): void {
    this.invites.update((list) => list.filter((i) => i.token !== token));
  }
}
