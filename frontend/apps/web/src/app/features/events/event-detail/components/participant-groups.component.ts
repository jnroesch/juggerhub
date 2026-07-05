import { Component, computed, input, output } from '@angular/core';
import { Signup } from '../../../../core/models/event.models';

/** The public "who's taking part" section — joined / awaiting / waiting-list groups as avatar rows. */
@Component({
  selector: 'jh-event-participant-groups',
  templateUrl: './participant-groups.component.html',
  styleUrl: './participant-groups.component.css',
})
export class EventParticipantGroupsComponent {
  readonly joined = input.required<Signup[]>();
  readonly awaiting = input.required<Signup[]>();
  readonly waitlist = input.required<Signup[]>();
  readonly openProfile = output<string>();

  protected readonly groups = computed(() => [
    { title: 'In', items: this.joined(), pending: false },
    { title: 'Awaiting approval', items: this.awaiting(), pending: true },
    { title: 'Waiting list', items: this.waitlist(), pending: false },
  ]);

  protected label(s: Signup): string {
    return s.teamName ?? s.userDisplayName ?? 'A participant';
  }

  protected initial(s: Signup): string {
    return (this.label(s).trim()[0] ?? '?').toUpperCase();
  }

  /** Secondary line: the team's address for a team, or the player's @handle. */
  protected secondary(s: Signup): string | null {
    if (s.teamSlug) {
      return `/t/${s.teamSlug}`;
    }
    return s.userHandle ? `@${s.userHandle}` : null;
  }
}
