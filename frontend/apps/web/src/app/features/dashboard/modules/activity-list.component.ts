import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CardComponent } from '../../../shared/ui';
import { ActivityEntry } from '../../../core/models/home.models';
import { relativeTime } from '../../../core/utils/format';

/**
 * "What's going on" (feature 025, US4) — a quiet, read-only activity log rendered as the last home
 * section. Passive signals only (a teammate signed up, a new team member, a badge, a party member
 * joined, a role change, a training reschedule); no action affordances. Deliberately lighter-weight
 * than News so authored posts are never buried. Renders nothing when empty.
 */
@Component({
  selector: 'jh-activity-list',
  imports: [RouterLink, CardComponent],
  templateUrl: './activity-list.component.html',
  styleUrl: './activity-list.component.css',
})
export class ActivityListComponent {
  readonly items = input.required<ActivityEntry[]>();

  protected readonly hasAny = computed(() => this.items().length > 0);

  protected rel(iso: string): string {
    return relativeTime(iso);
  }

  /** The navigation route for an entry, by kind; null when there is no target. */
  protected link(item: ActivityEntry): unknown[] | null {
    if (!item.linkTarget) return null;
    switch (item.kind) {
      case 'TeammateJoinedEvent':
      case 'PartyMemberJoined':
        return ['/events', item.linkTarget];
      case 'NewTeamMember':
      case 'RoleChanged':
        return ['/t', item.linkTarget];
      case 'BadgeAwarded':
        return ['/u', item.linkTarget];
      case 'TrainingChanged':
        return ['/trainings/sessions', item.linkTarget];
      default:
        return null;
    }
  }
}
