import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { TranslocoDatePipe } from '@jsverse/transloco-locale';
import { AlertComponent, ButtonDirective, CardComponent, EmptyStateComponent, LoadingComponent } from '../../../../shared/ui';
import { TeamPlacement } from '../../../../core/models/results.models';
import { ResultsService } from '../../../../core/services/results.service';

const PAGE = 10;

/**
 * A team's tournament placements (feature 050): tournament, date and "place N of M", newest first,
 * each linking to the tournament's results. Visible to every signed-in player, like the team page.
 *
 * Only placements connected to this team appear. A team never claims one: the connection comes
 * from the team's own confirmed sign-up or from a JuggerHub admin's check.
 */
@Component({
  selector: 'jh-team-placements',
  imports: [RouterLink, TranslocoPipe, TranslocoDatePipe, AlertComponent, ButtonDirective, CardComponent, EmptyStateComponent, LoadingComponent],
  templateUrl: './team-placements.component.html',
  styleUrl: './team-placements.component.css',
})
export class TeamPlacementsComponent implements OnInit {
  private readonly api = inject(ResultsService);

  readonly slug = input.required<string>();

  protected readonly items = signal<TeamPlacement[]>([]);
  protected readonly total = signal(0);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);

  protected readonly hasMore = computed(() => this.items().length < this.total());

  ngOnInit(): void {
    this.loadMore();
  }

  protected loadMore(): void {
    this.loading.set(true);
    this.loadError.set(false);
    this.api.getTeamPlacements(this.slug(), this.items().length, PAGE).subscribe({
      next: (page) => {
        this.items.update((current) => [...current, ...page.items]);
        this.total.set(page.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loadError.set(true);
        this.loading.set(false);
      },
    });
  }
}
