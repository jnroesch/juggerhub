import { Component, computed, input, output } from '@angular/core';
import { ButtonDirective } from '../../../shared/ui';
import { RouterLink } from '@angular/router';
import {
  AppNotification,
  isMarketInvite,
  isPartyNews,
  isPartyRequest,
  isTeamInvite,
  isTeamNews,
  isTeamRoleChanged,
  isTrainingScheduled,
  isTrainingUpdated,
} from '../../../core/models/notification.models';
import { relativeTime } from '../../../core/utils/format';

/**
 * One notification row (feature 010). Presentational: it renders a type-appropriate icon, a title,
 * a supporting line, a relative time, and an unread marker, and — for an unresolved team invite —
 * inline Accept / Decline. It owns no data or API calls; it emits intent (`accept` / `decline` /
 * `open`) and the Alerts inbox performs the authoritative action.
 */
@Component({
  selector: 'jh-notification-row',
  imports: [RouterLink, ButtonDirective],
  templateUrl: './notification-row.component.html',
  styleUrl: './notification-row.component.css',
})
export class NotificationRowComponent {
  readonly notification = input.required<AppNotification>();

  /** Inline invite actions (only fired for an actionable TeamInvite). */
  readonly accept = output<void>();
  readonly decline = output<void>();
  /** The row was opened (navigated / tapped) — mark it read. */
  readonly open = output<void>();

  protected readonly time = computed(() => relativeTime(this.notification().createdDate));

  /** A route target for link-only types; null when the row acts inline (invite) or can't navigate. */
  protected readonly link = computed<string | null>(() => {
    const n = this.notification();
    if (isTeamRoleChanged(n) || isTeamNews(n)) {
      return `/t/${n.payload.teamSlug}`;
    }
    if (isTeamInvite(n) && n.resolved) {
      // A handled invite still links to the team it concerned.
      return `/t/${n.payload.teamSlug}`;
    }
    if (isPartyRequest(n)) {
      return `/parties/${n.payload.partyId}`;
    }
    if (isPartyNews(n)) {
      return `/parties/${n.payload.partyId}/news`;
    }
    if (isMarketInvite(n)) {
      // Links to the event page, where the market inbox answers the invite (feature 017).
      return `/events/${n.payload.eventId}`;
    }
    if (isTrainingScheduled(n) || isTrainingUpdated(n)) {
      return n.payload.sessionId ? `/trainings/sessions/${n.payload.sessionId}` : `/t/${n.payload.teamSlug}/trainings`;
    }
    return null;
  });

  protected readonly title = computed(() => {
    const n = this.notification();
    if (isTeamInvite(n)) {
      return `Invitation to join ${n.payload.teamName}`;
    }
    if (isTeamRoleChanged(n)) {
      const role = n.payload.newRole === 'Admin' ? 'an admin' : 'a member';
      return `You're now ${role} of ${n.payload.teamName}`;
    }
    if (isTeamNews(n)) {
      return `News from ${n.payload.teamName}`;
    }
    if (isPartyRequest(n)) {
      return `${n.payload.teamName} is forming a party`;
    }
    if (isPartyNews(n)) {
      return `Party update — ${n.payload.teamName} @ ${n.payload.eventName}`;
    }
    if (isMarketInvite(n)) {
      return `${n.payload.teamName} invited you to their crew`;
    }
    if (isTrainingScheduled(n)) {
      return `New training: ${n.payload.trainingName}`;
    }
    if (isTrainingUpdated(n)) {
      return n.payload.kind === 'cancelled' ? `Training cancelled: ${n.payload.trainingName}` : `Training changed: ${n.payload.trainingName}`;
    }
    return 'Notification';
  });

  protected readonly supporting = computed(() => {
    const n = this.notification();
    if (isTeamInvite(n)) {
      return n.resolved ? 'Invitation handled' : `${n.payload.inviterName} invited you to the team`;
    }
    if (isTeamRoleChanged(n)) {
      return n.actorDisplayName ? `${n.actorDisplayName} updated your role` : 'Your role was updated';
    }
    if (isTeamNews(n)) {
      return n.payload.excerpt;
    }
    if (isPartyRequest(n)) {
      return `Tap to answer — are you in for ${n.payload.eventName}?`;
    }
    if (isPartyNews(n)) {
      return `New update for the ${n.payload.eventName} party`;
    }
    if (isTrainingScheduled(n)) {
      return n.payload.isRecurring ? 'A new series was added — say if you can make it' : 'A one-off was added — say if you can make it';
    }
    if (isTrainingUpdated(n)) {
      return n.payload.kind === 'cancelled' ? 'A session you responded to was cancelled' : 'An upcoming session changed';
    }
    return '';
  });

  /** The icon family drives the color scheme (invite=brand, role=info, news=secondary). */
  protected readonly kind = computed(() => this.notification().type);

  /** Only an unresolved invite is actionable inline. */
  protected readonly actionable = computed(() => {
    const n = this.notification();
    return isTeamInvite(n) && !n.resolved;
  });
}
