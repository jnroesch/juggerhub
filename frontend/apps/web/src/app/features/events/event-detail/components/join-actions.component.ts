import { Component, computed, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { EventDetail } from '../../../../core/models/event.models';

/**
 * The prominent bottom sign-up call to action: join / join-waitlist / withdraw for a
 * signed-in non-admin, with a team picker for teams-only events and a full-state
 * callout. Emits the chosen action; the parent performs the call and reloads.
 */
@Component({
  selector: 'jh-event-join-actions',
  imports: [FormsModule, RouterLink],
  templateUrl: './join-actions.component.html',
  styleUrl: './join-actions.component.css',
})
export class EventJoinActionsComponent {
  readonly detail = input.required<EventDetail>();
  readonly acting = input(false);
  readonly actionError = input<string | null>(null);
  /** Emits the team id for teams-only events, or null for individuals. */
  readonly join = output<string | null>();
  readonly withdraw = output<void>();

  private readonly picked = signal<string | null>(null);

  protected readonly teamOptions = computed(() => this.detail().viewer.teamsICanEnter);
  protected readonly selectedTeamId = computed(() => this.picked() ?? this.teamOptions()[0]?.teamId ?? '');

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
