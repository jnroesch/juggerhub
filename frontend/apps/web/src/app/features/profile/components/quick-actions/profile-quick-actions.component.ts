import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, forkJoin, map, of } from 'rxjs';
import { AuthService } from '../../../../core/services/auth.service';
import { ChatService } from '../../../../core/services/chat.service';
import { ProfileService } from '../../../../core/services/profile.service';
import { TeamService } from '../../../../core/services/team.service';
import { ProfileTeam } from '../../../../core/models/profile.models';
import { ButtonDirective, AlertComponent } from '../../../../shared/ui';

/** A team the viewer administers and to which the target player can be invited. */
interface EligibleTeam {
  readonly slug: string;
  readonly name: string;
  readonly userId: string;
}

/**
 * Feature 021 — the Message + Invite shorthand actions on a player's public profile.
 *
 * Shown only to a signed-in viewer, never on the viewer's own profile. Identity is
 * resolved from the public HANDLE via existing authenticated search (chat people-search
 * for messaging, team user-search for inviting), so the public profile response is never
 * asked for an account id. The server remains the authorization boundary — these checks
 * are UX only.
 */
@Component({
  selector: 'jh-profile-quick-actions',
  templateUrl: './profile-quick-actions.component.html',
  styleUrl: './profile-quick-actions.component.css',
  imports: [ButtonDirective, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileQuickActionsComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly profiles = inject(ProfileService);
  private readonly chat = inject(ChatService);
  private readonly teams = inject(TeamService);
  private readonly router = inject(Router);

  /** The target player's public handle (the profile being viewed). */
  readonly handle = input.required<string>();

  private readonly viewerHandle = signal<string | null>(null);
  private readonly viewerLoaded = signal(false);

  // Message state
  protected readonly messaging = signal(false);
  protected readonly messageError = signal<string | null>(null);

  // Invite state
  protected readonly adminTeamCount = signal(0);
  protected readonly eligibleTeams = signal<EligibleTeam[]>([]);
  protected readonly inviteResolved = signal(false);
  protected readonly pickerOpen = signal(false);
  protected readonly inviting = signal(false);
  protected readonly invitedTo = signal<string | null>(null);
  protected readonly inviteError = signal<string | null>(null);

  /** Never render for anonymous viewers or on the viewer's own profile. */
  protected readonly isSelf = computed(() => {
    const v = this.viewerHandle();
    return v !== null && v.toLowerCase() === this.handle().toLowerCase();
  });
  protected readonly visible = computed(
    () => this.auth.isAuthenticated() && this.viewerLoaded() && !this.isSelf(),
  );

  /** Invite is offered only to admins of ≥1 team; disabled-with-reason when none eligible. */
  protected readonly showInvite = computed(() => this.adminTeamCount() > 0);
  protected readonly inviteEligible = computed(() => this.eligibleTeams().length > 0);

  ngOnInit(): void {
    if (!this.auth.isAuthenticated()) {
      this.viewerLoaded.set(true);
      return;
    }

    this.profiles.getMineCached().subscribe({
      next: (me) => {
        this.viewerHandle.set(me.handle);
        this.viewerLoaded.set(true);

        const adminTeams = me.teams.filter((t) => t.role === 'Admin');
        this.adminTeamCount.set(adminTeams.length);

        const isSelf = me.handle.toLowerCase() === this.handle().toLowerCase();
        if (adminTeams.length > 0 && !isSelf) {
          this.resolveEligibility(adminTeams);
        } else {
          this.inviteResolved.set(true);
        }
      },
      error: () => this.viewerLoaded.set(true),
    });
  }

  // --- Message --------------------------------------------------------------

  protected message(): void {
    if (this.messaging()) {
      return;
    }
    this.messaging.set(true);
    this.messageError.set(null);
    const target = this.handle();

    this.chat.search(target).subscribe({
      next: (res) => {
        // Exact-handle match. A blocked player is excluded from search server-side, so
        // "not found" also covers the blocked case (FR-004) — no separate block check.
        const hit = res.people.items.find(
          (p) => (p.handle ?? '').toLowerCase() === target.toLowerCase(),
        );
        if (!hit) {
          this.messaging.set(false);
          this.messageError.set("You can't message this player right now.");
          return;
        }
        if (hit.existingConversationId) {
          void this.router.navigate(['/chat', hit.existingConversationId]);
          return;
        }
        // No conversation yet — open a compose draft (feature 022 lazy creation). Nothing is created
        // until the first message is sent, so opening Message and leaving pollutes nothing.
        void this.router.navigate(['/chat/compose', target], {
          state: { userId: hit.userId, displayName: hit.displayName },
        });
      },
      error: () => this.failMessage(),
    });
  }

  private failMessage(): void {
    this.messaging.set(false);
    this.messageError.set("That message couldn't be started. Try again shortly.");
  }

  // --- Invite ---------------------------------------------------------------

  private resolveEligibility(adminTeams: ProfileTeam[]): void {
    const target = this.handle();
    forkJoin(
      adminTeams.map((team) =>
        this.teams.searchUsers(team.slug, target).pipe(
          map((page) => {
            const hit = page.items.find((u) => u.handle.toLowerCase() === target.toLowerCase());
            return hit && hit.relation === 'Invitable'
              ? ({ slug: team.slug, name: team.name, userId: hit.userId } as EligibleTeam)
              : null;
          }),
          catchError(() => of(null)),
        ),
      ),
    ).subscribe({
      next: (results) => {
        this.eligibleTeams.set(results.filter((r): r is EligibleTeam => r !== null));
        this.inviteResolved.set(true);
      },
      error: () => this.inviteResolved.set(true),
    });
  }

  /** One eligible team → invite directly; several → open the picker. */
  protected onInviteClick(): void {
    const eligible = this.eligibleTeams();
    if (eligible.length === 1) {
      this.sendInvite(eligible[0]);
    } else if (eligible.length > 1) {
      this.pickerOpen.update((open) => !open);
    }
  }

  protected sendInvite(team: EligibleTeam): void {
    if (this.inviting()) {
      return;
    }
    this.inviting.set(true);
    this.inviteError.set(null);
    this.pickerOpen.set(false);

    this.teams.createTargetedInvite(team.slug, team.userId).subscribe({
      next: () => {
        this.inviting.set(false);
        this.invitedTo.set(team.name);
        // Prevent a duplicate: that team is no longer invitable this session.
        this.eligibleTeams.update((list) => list.filter((t) => t.slug !== team.slug));
      },
      error: () => {
        this.inviting.set(false);
        this.inviteError.set("That invite couldn't be sent. Try again shortly.");
      },
    });
  }
}
