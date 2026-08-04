import { Component, computed, inject, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { RouterLink } from '@angular/router';
import { CardComponent } from '../../../shared/ui';
import { ActivityEntry, ActivityKind } from '../../../core/models/home.models';
import { injectRelativeTime } from '../../../core/i18n/locale-format';

/** One rendered line: the localized sentence plus where it navigates. */
interface ActivityRow {
  kind: ActivityKind;
  text: string;
  route: unknown[] | null;
  time: string;
}

/**
 * "What's going on" (feature 025, US4) — a quiet, read-only activity log rendered as the last home
 * section. Passive signals only (a teammate signed up, a new team member, a badge, a party member
 * joined, a role change, a training reschedule); no action affordances. Deliberately lighter-weight
 * than News so authored posts are never buried. Renders nothing when empty.
 *
 * The sentence is composed *here*, not on the server: the API sends `kind` + untranslated names
 * (`ActivityParams`) and this component picks the matching `home.activity.*` key. The server has no
 * idea which language the viewer chose, so a summary built there is English on every dashboard —
 * and invisible to the catalogue parity guard, because prose that never became a key can't be
 * missing from `de.json`. Mirrors how `jh-notification-row` renders alert titles.
 */
@Component({
  selector: 'jh-activity-list',
  imports: [RouterLink, CardComponent, TranslocoPipe],
  templateUrl: './activity-list.component.html',
  styleUrl: './activity-list.component.css',
})
export class ActivityListComponent {
  private readonly transloco = inject(TranslocoService);
  /** Re-composes every sentence when the active language changes. */
  private readonly lang = toSignal(this.transloco.langChanges$, {
    initialValue: this.transloco.getActiveLang(),
  });

  private readonly rel = injectRelativeTime();

  readonly items = input.required<ActivityEntry[]>();

  protected readonly rows = computed<ActivityRow[]>(() => {
    this.lang(); // re-translate on language switch
    return this.items()
      .map((item) => ({
        kind: item.kind,
        text: this.text(item),
        route: this.link(item),
        time: this.rel(item.occurredAt),
      }))
      // An unrecognized kind (a newer server) yields no sentence — drop it rather than render a blank line.
      .filter((row) => row.text.length > 0);
  });

  protected readonly hasAny = computed(() => this.rows().length > 0);

  /** The localized sentence for an entry. Names stay as the server sent them; only prose is translated. */
  private text(item: ActivityEntry): string {
    const t = (key: string, params?: Record<string, unknown>) => this.transloco.translate(key, params);
    const p = item.params;
    // A profile with no display name gets a *translated* stand-in — the whole point of this change.
    const actorName =
      p.actorName ??
      t(
        item.kind === 'TeammateJoinedEvent' || item.kind === 'BadgeAwarded'
          ? 'home.activity.aTeammate'
          : 'home.activity.someone',
      );

    switch (item.kind) {
      case 'TeammateJoinedEvent':
        return t('home.activity.teammateJoinedEvent', { actorName, eventName: p.eventName });
      case 'NewTeamMember':
        return t('home.activity.newTeamMember', { actorName, teamName: p.teamName });
      case 'PartyMemberJoined':
        return t('home.activity.partyMemberJoined', { actorName, eventName: p.eventName });
      case 'BadgeAwarded':
        return p.isMine
          ? t('home.activity.badgeAwardedMine', { badgeName: p.badgeName })
          : t('home.activity.badgeAwarded', { actorName, badgeName: p.badgeName });
      case 'RoleChanged':
        if (!p.teamName || !p.newRole) {
          return t('home.activity.roleChangedUnknown');
        }
        return p.newRole === 'Admin'
          ? t('home.activity.roleChangedAdmin', { teamName: p.teamName })
          : t('home.activity.roleChangedMember', { teamName: p.teamName });
      case 'TrainingChanged': {
        const cancelled = p.changeKind === 'Cancelled';
        return p.trainingName
          ? t(cancelled ? 'home.activity.trainingCancelled' : 'home.activity.trainingUpdated', {
              trainingName: p.trainingName,
            })
          : t(cancelled ? 'home.activity.trainingCancelledUnnamed' : 'home.activity.trainingUpdatedUnnamed');
      }
      default:
        return '';
    }
  }

  /** The navigation route for an entry, by kind; null when there is no target. */
  private link(item: ActivityEntry): unknown[] | null {
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
