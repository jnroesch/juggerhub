import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { TranslocoDatePipe } from '@jsverse/transloco-locale';
import { AlertComponent, ButtonDirective, CardComponent, LoadingComponent } from '../../../../shared/ui';
import { Placement, TournamentMatch, TournamentResult } from '../../../../core/models/results.models';
import { ResultsService } from '../../../../core/services/results.service';

/** Matches under one stage heading. `stage` null is the knockout rounds. */
interface MatchGroup {
  stage: string | null;
  matches: TournamentMatch[];
}

const MATCH_PAGE = 50;

/**
 * The results of a tournament event (feature 050): the ranking with its winner, where the results
 * came from, the matches of an imported tournament, and — while the tournament is on — a link to
 * follow it live on Tugeny.
 *
 * Renders nothing until there is something to show: no results and no live link means no card.
 * A failed load is not "nothing recorded" — it shows an error with a retry, never the empty look
 * (DESIGN.md "Error vs. empty").
 *
 * Matches load only when asked for: a tournament can carry hundreds, and most visitors come for
 * the ranking.
 */
@Component({
  selector: 'jh-event-results',
  imports: [RouterLink, TranslocoPipe, TranslocoDatePipe, CardComponent, AlertComponent, ButtonDirective, LoadingComponent],
  templateUrl: './event-results.component.html',
  styleUrl: './event-results.component.css',
})
export class EventResultsComponent implements OnInit {
  private readonly api = inject(ResultsService);

  readonly eventId = input.required<string>();
  /** When the event ends — the live link is offered until then. */
  readonly endsAt = input.required<string>();

  protected readonly result = signal<TournamentResult | null>(null);
  protected readonly loadError = signal(false);

  protected readonly matches = signal<TournamentMatch[]>([]);
  protected readonly matchTotal = signal(0);
  protected readonly matchesOpen = signal(false);
  protected readonly matchesLoading = signal(false);
  protected readonly matchesError = signal(false);

  protected readonly placements = computed<Placement[]>(() => this.result()?.placements ?? []);
  protected readonly hasResults = computed(() => this.placements().length > 0);

  /** Everyone at first place — two or more when the admins recorded a shared first place. */
  protected readonly winners = computed(() => this.placements().filter((p) => p.position === 1));

  protected readonly showLive = computed(() => {
    const r = this.result();
    return !!r?.tugeny && new Date(this.endsAt()).getTime() > Date.now();
  });

  protected readonly imported = computed(() => this.result()?.source === 'TugenyImport');

  protected readonly hasMoreMatches = computed(() => this.matches().length < this.matchTotal());

  /** Stages in the order they first appear, with the knockout rounds (no stage) last. */
  protected readonly matchGroups = computed<MatchGroup[]>(() => {
    const groups = new Map<string | null, TournamentMatch[]>();
    for (const match of this.matches()) {
      const list = groups.get(match.stage) ?? [];
      list.push(match);
      groups.set(match.stage, list);
    }
    const staged = [...groups.entries()].filter(([stage]) => stage !== null);
    const knockout = groups.get(null);
    return [
      ...staged.map(([stage, matches]) => ({ stage, matches })),
      ...(knockout ? [{ stage: null, matches: knockout }] : []),
    ];
  });

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loadError.set(false);
    this.api.getResults(this.eventId()).subscribe({
      next: (r) => this.result.set(r),
      error: () => this.loadError.set(true),
    });
  }

  protected openMatches(): void {
    this.matchesOpen.set(true);
    if (this.matches().length === 0) {
      this.loadMoreMatches();
    }
  }

  protected loadMoreMatches(): void {
    this.matchesLoading.set(true);
    this.matchesError.set(false);
    this.api.getMatches(this.eventId(), this.matches().length, MATCH_PAGE).subscribe({
      next: (page) => {
        this.matches.update((current) => [...current, ...page.items]);
        this.matchTotal.set(page.totalCount);
        this.matchesLoading.set(false);
      },
      error: () => {
        this.matchesError.set(true);
        this.matchesLoading.set(false);
      },
    });
  }
}
