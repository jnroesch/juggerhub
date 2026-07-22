import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HomeNews } from '../../../core/models/home.models';
import { relativeTime } from '../../../core/utils/format';

/**
 * The Home News module (feature 008, party source added by feature 025): authored items tagged by
 * source (team / event / party) with a relative timestamp, newest-first. Pure presentation over the
 * already-aggregated feed.
 */
@Component({
  selector: 'jh-news-list',
  imports: [RouterLink],
  templateUrl: './news-list.component.html',
  styleUrl: './news-list.component.css',
})
export class NewsListComponent {
  readonly news = input.required<HomeNews[]>();

  protected rel(iso: string): string {
    return relativeTime(iso);
  }

  /** Link target for an item by its source (team → team page; event & party → event page). */
  protected link(item: HomeNews): string[] {
    return item.source === 'team' ? ['/t', item.sourceSlugOrId] : ['/events', item.sourceSlugOrId];
  }

  /** The source-pill styling, distinct per source. */
  protected pillClass(item: HomeNews): string {
    switch (item.source) {
      case 'team':
        return 'bg-surface-secondary-soft text-secondary';
      case 'party':
        return 'bg-surface-inverse text-on-inverse';
      default:
        return 'bg-info-bg text-info-fg';
    }
  }
}
