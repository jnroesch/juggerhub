import { Component, computed, input, output, signal } from '@angular/core';
import { ButtonDirective, AlertComponent } from '../../../../shared/ui';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { EventDetail } from '../../../../core/models/event.models';
import { PartyContext, PartyContextTeam } from '../../../../core/models/party.models';

/**
 * The prominent bottom sign-up call to action: join / join-waitlist / withdraw for a
 * signed-in non-admin, with a team picker for teams-only events and a full-state
 * callout. Emits the chosen action; the parent performs the call and reloads.
 */
@Component({
  selector: 'jh-event-join-actions',
  imports: [FormsModule, RouterLink, ButtonDirective, AlertComponent],
  templateUrl: './join-actions.component.html',
  styleUrl: './join-actions.component.css',
})
export class EventJoinActionsComponent {
  readonly detail = input.required<EventDetail>();
  /** Feature 016: the caller's party affordances for a teams-only event. */
  readonly partyContext = input<PartyContext | null>(null);
  readonly acting = input(false);
  readonly actionError = input<string | null>(null);
  /** Emits the team id for teams-only events, or null for individuals. */
  readonly join = output<string | null>();
  readonly withdraw = output<void>();

  private readonly picked = signal<string | null>(null);

  protected readonly teamOptions = computed(() => this.detail().viewer.teamsICanEnter);
  protected readonly selectedTeamId = computed(() => this.picked() ?? this.teamOptions()[0]?.teamId ?? '');

  /** Teams the caller already has a party in — show a "view party" card instead of the form button. */
  protected readonly partyCards = computed<PartyContextTeam[]>(
    () => this.partyContext()?.teams.filter((t) => t.partyId) ?? [],
  );
  /** True when the caller administers a team here with no party yet (can form one). */
  protected readonly canFormParty = computed(() => this.partyContext()?.teams.some((t) => t.canForm) ?? false);

  protected readonly cancelled = computed(() => this.detail().status === 'Cancelled');
  protected readonly canJoin = computed(() => {
    const d = this.detail();
    return d.status === 'Published' && d.viewer.isAuthenticated && !d.viewer.isAdmin && d.viewer.mySignupStatus === null;
  });

  protected readonly statusLabel = computed(() => {
    switch (this.detail().viewer.mySignupStatus) {
      case 'Joined':
        return "You're in";
      case 'AwaitingApproval':
        return 'Awaiting approval — pay to confirm your spot';
      case 'Waitlisted':
        return "On the waiting list — you're not charged unless a spot opens";
      default:
        return '';
    }
  });

  protected pick(teamId: string): void {
    this.picked.set(teamId);
  }

  protected emitJoin(): void {
    const d = this.detail();
    this.join.emit(d.participantMode === 'Teams' ? this.selectedTeamId() || null : null);
  }
}
