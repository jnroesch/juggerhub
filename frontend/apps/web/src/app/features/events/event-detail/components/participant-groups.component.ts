import { Component, computed, input, output } from '@angular/core';
import { Signup } from '../../../../core/models/event.models';

/** The public "who's taking part" section — joined / awaiting / waiting-list groups. */
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
    { title: 'Joined', items: this.joined() },
    { title: 'Awaiting approval', items: this.awaiting() },
    { title: 'Waiting list', items: this.waitlist() },
  ]);

  protected label(s: Signup): string {
    return s.teamName ?? s.userDisplayName ?? 'A participant';
  }
}
