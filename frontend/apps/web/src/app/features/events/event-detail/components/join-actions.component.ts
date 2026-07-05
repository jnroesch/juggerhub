import { Component, computed, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { EventDetail } from '../../../../core/models/event.models';

/**
 * Sidebar sign-up actions: remaining-spots label, and — for a signed-in non-admin —
 * join / join-waitlist / withdraw, with a team picker for teams-only events. Emits the
 * chosen action; the parent performs the call and reloads. Cancelled events show no actions.
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

  protected readonly spotsLabel = computed(() => {
    const d = this.detail();
    const remaining = Math.max(d.participationLimit - d.occupiedSpots, 0);
    return d.isFull ? 'Full' : `${remaining} of ${d.participationLimit} spots open`;
  });

  protected pick(teamId: string): void {
    this.picked.set(teamId);
  }

  protected emitJoin(): void {
    const d = this.detail();
    this.join.emit(d.participantMode === 'Teams' ? this.selectedTeamId() || null : null);
  }
}
