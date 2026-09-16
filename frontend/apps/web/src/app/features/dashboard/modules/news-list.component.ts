import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { HomeNews } from '../../../core/models/home.models';
import { injectRelativeTime } from '../../../core/i18n/locale-format';
import { ChipDirective, ChipTone } from '../../../shared/ui';

/**
 * The Home News module (feature 008, party source added by feature 025): authored items tagged by
 * source (team / event / party) with a relative timestamp, newest-first. Pure presentation over the
 * already-aggregated feed.
 */
@Component({
  selector: 'jh-news-list',
  imports: [RouterLink, ChipDirective, TranslocoPipe],
  templateUrl: './news-list.component.html',
  styleUrl: './news-list.component.css',
})
export class NewsListComponent {
  readonly news = input.required<HomeNews[]>();

  protected readonly rel = injectRelativeTime();

  /** Link target for an item by its source (team → team page; event & party → event page). */
  protected link(item: HomeNews): string[] {
    return item.source === 'team' ? ['/t', item.sourceSlugOrId] : ['/events', item.sourceSlugOrId];
  }

  /**
   * The source chip's label. The chip used to print the raw `source` enum under a CSS
   * `uppercase`, which rendered an untranslated "TEAM" / "EVENT" / "PARTY"; the chip is
   * one text step now (GH #301), so the label is a translated word like any other.
   */
  protected sourceLabel(item: HomeNews): string {
    switch (item.source) {
      case 'team':
        return 'home.newsSourceTeam';
      case 'party':
        return 'home.newsSourceParty';
      default:
        return 'home.newsSourceEvent';
    }
  }

  /** The source chip's tone, distinct per source. */
  protected pillTone(item: HomeNews): ChipTone {
    switch (item.source) {
      case 'team':
        return 'secondary';
      case 'party':
        return 'muted';
      default:
        return 'info';
    }
  }
}
