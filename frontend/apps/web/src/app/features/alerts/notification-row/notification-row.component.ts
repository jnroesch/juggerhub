import { Component, computed, inject, input, output, ChangeDetectionStrategy } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ButtonDirective, CardComponent } from '../../../shared/ui';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import {
  AppNotification,
  isEventCancelled,
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
  imports: [RouterLink, ButtonDirective, CardComponent, TranslocoPipe],
  templateUrl: './notification-row.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './notification-row.component.css',
})
export class NotificationRowComponent {
  private readonly transloco = inject(TranslocoService);
  /** Re-evaluates the title/supporting computeds when the active language changes. */
  private readonly lang = toSignal(this.transloco.langChanges$, {
    initialValue: this.transloco.getActiveLang(),
  });

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
    if (isEventCancelled(n)) {
      // The event page stays viewable after a cancellation, which is what the email promises too.
      return `/events/${n.payload.eventId}`;
    }
    return null;
  });

  protected readonly title = computed(() => {
    this.lang(); // re-translate on language switch
    const t = (key: string, params?: Record<string, unknown>) => this.transloco.translate(key, params);
    const n = this.notification();
    if (isTeamInvite(n)) {
      return t('alerts.row.teamInviteTitle', { team: n.payload.teamName });
    }
    if (isTeamRoleChanged(n)) {
      return n.payload.newRole === 'Admin'
        ? t('alerts.row.roleChangedTitleAdmin', { team: n.payload.teamName })
        : t('alerts.row.roleChangedTitleMember', { team: n.payload.teamName });
    }
    if (isTeamNews(n)) {
      return t('alerts.row.teamNewsTitle', { team: n.payload.teamName });
    }
    if (isPartyRequest(n)) {
      return t('alerts.row.partyRequestTitle', { team: n.payload.teamName });
    }
    if (isPartyNews(n)) {
      return t('alerts.row.partyNewsTitle', { team: n.payload.teamName, event: n.payload.eventName });
    }
    if (isMarketInvite(n)) {
      return t('alerts.row.marketInviteTitle', { team: n.payload.teamName });
    }
    if (isTrainingScheduled(n)) {
      return t('alerts.row.trainingScheduledTitle', { name: n.payload.trainingName });
    }
    if (isTrainingUpdated(n)) {
      return n.payload.kind === 'cancelled'
        ? t('alerts.row.trainingCancelledTitle', { name: n.payload.trainingName })
        : t('alerts.row.trainingChangedTitle', { name: n.payload.trainingName });
    }
    if (isEventCancelled(n)) {
      return t('alerts.row.eventCancelledTitle', { event: n.payload.eventName });
    }
    return t('alerts.row.fallbackTitle');
  });

  protected readonly supporting = computed(() => {
    this.lang(); // re-translate on language switch
    const t = (key: string, params?: Record<string, unknown>) => this.transloco.translate(key, params);
    const n = this.notification();
    if (isTeamInvite(n)) {
      return n.resolved
        ? t('alerts.row.teamInviteHandled')
        : t('alerts.row.teamInviteSupporting', { inviter: n.payload.inviterName });
    }
    if (isTeamRoleChanged(n)) {
      return n.actorDisplayName
        ? t('alerts.row.roleChangedSupportingActor', { actor: n.actorDisplayName })
        : t('alerts.row.roleChangedSupporting');
    }
    if (isTeamNews(n)) {
      return n.payload.excerpt;
    }
    if (isPartyRequest(n)) {
      return t('alerts.row.partyRequestSupporting', { event: n.payload.eventName });
    }
    if (isPartyNews(n)) {
      return t('alerts.row.partyNewsSupporting', { event: n.payload.eventName });
    }
    if (isTrainingScheduled(n)) {
      return n.payload.isRecurring
        ? t('alerts.row.trainingScheduledSeries')
        : t('alerts.row.trainingScheduledOneoff');
    }
    if (isTrainingUpdated(n)) {
      return n.payload.kind === 'cancelled'
        ? t('alerts.row.trainingUpdatedCancelled')
        : t('alerts.row.trainingUpdatedChanged');
    }
    if (isEventCancelled(n)) {
      return t('alerts.row.eventCancelledSupporting');
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
