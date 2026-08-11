import { Component, computed, inject, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { CardComponent, EmptyStateComponent } from '../../../../shared/ui';
import { TeamHappening, TeamHappeningKind } from '../../../../core/models/team.models';
import { injectDateFormats, injectRelativeTime } from '../../../../core/i18n/locale-format';

/** One rendered line: the localized sentence plus where it navigates. */
interface HappeningRow {
  kind: TeamHappeningKind;
  text: string;
  route: unknown[] | null;
  time: string;
}

/**
 * "What's happening" (feature 044, GH #178) — the team-internal catch-up card: who joined, what the
 * team was awarded, a training series added, a session called off. **Members only**; the team page
 * renders it inside its member branch and the server refuses the data to anyone else.
 *
 * Deliberately *not* the team's event history — that stays in the separate "Recent events" card
 * above, which is visible to any signed-in viewer (spec decision D5). The two are disjoint.
 *
 * The sentence is composed *here*, not on the server: the API sends `kind` + untranslated names and
 * this component picks the matching `teams.detail.happening.*` key. The server has no idea which
 * language the viewer chose, so a summary built there is English on every team page — and invisible
 * to the catalogue parity guard, because prose that never became a key can't be missing from
 * `de.json`. Mirrors `jh-activity-list` on the dashboard.
 *
 * Three deliberate divergences from that sibling:
 *  1. It renders an **empty state** rather than nothing when quiet (FR-014) — a member should see
 *     that the section exists and is simply quiet, not wonder where it went.
 *  2. It takes the team `slug`, because a created training series has no route of its own and the
 *     link has to be built from the team's trainings tab.
 *  3. There is no viewer-relative ("you earned…") form — these entries are about the team.
 */
@Component({
  selector: 'jh-team-happenings',
  imports: [RouterLink, CardComponent, EmptyStateComponent, TranslocoPipe],
  templateUrl: './team-happenings.component.html',
  styleUrl: './team-happenings.component.css',
})
export class TeamHappeningsComponent {
  private readonly transloco = inject(TranslocoService);
  /** Re-composes every sentence when the active language changes. */
  private readonly lang = toSignal(this.transloco.langChanges$, {
    initialValue: this.transloco.getActiveLang(),
  });

  private readonly rel = injectRelativeTime();
  private readonly fmt = injectDateFormats();

  readonly items = input.required<TeamHappening[]>();
  /** The team whose page this is — the only way to link a created training series anywhere useful. */
  readonly slug = input.required<string>();

  protected readonly rows = computed<HappeningRow[]>(() => {
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
  private text(item: TeamHappening): string {
    const t = (key: string, params?: Record<string, unknown>) => this.transloco.translate(key, params);
    const p = item.params;

    switch (item.kind) {
      case 'MemberJoined':
        // A suppressed or missing profile gets a *translated* stand-in, never an English one.
        return t('teams.detail.happening.memberJoined', {
          actorName: p.actorName ?? t('teams.detail.happening.someone'),
        });
      case 'RecognitionAwarded':
        return t('teams.detail.happening.recognitionAwarded', { recognitionName: p.recognitionName });
      case 'TrainingSeriesCreated':
        return t('teams.detail.happening.trainingSeriesCreated', { trainingName: p.trainingName });
      case 'TrainingSessionCancelled':
        return t('teams.detail.happening.trainingSessionCancelled', {
          trainingName: p.trainingName,
          sessionDate: p.sessionDate ? this.fmt.shortDate(p.sessionDate) : '',
        });
      default:
        return '';
    }
  }

  /** The navigation route for an entry, by kind; null when there is no target. */
  private link(item: TeamHappening): unknown[] | null {
    switch (item.kind) {
      case 'MemberJoined':
        return item.linkTarget ? ['/u', item.linkTarget] : null;
      case 'TrainingSessionCancelled':
        return item.linkTarget ? ['/trainings/sessions', item.linkTarget] : null;
      // A series has no page of its own — the trainings tab is the nearest honest destination.
      case 'TrainingSeriesCreated':
        return ['/t', this.slug(), 'trainings'];
      // An award's home is the "Badges & achievements" card further down this same page.
      case 'RecognitionAwarded':
      default:
        return null;
    }
  }
}
