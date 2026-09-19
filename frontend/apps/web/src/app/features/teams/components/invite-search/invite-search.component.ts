import { Component, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { EMPTY, catchError, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { InvitableUser } from '../../../../core/models/team.models';
import { TeamService } from '../../../../core/services/team.service';
import { problemDetail } from '../../../../core/utils/problem';
import { AlertComponent, IconComponent } from '../../../../shared/ui';

/**
 * Search people and invite them to one team (feature 052, extracted from
 * `TeamInvitationsComponent`).
 *
 * Two screens render this: the team's invitations screen, and the create wizard's last step. It
 * exists as a component rather than as a block copied twice because of one thing it owns — the
 * `Member` / `Invited` / `Invitable` switch, which decides whether an invite may be offered at
 * all. That is a rule, not decoration, and a second copy of it is the duplication class feature
 * 046 recorded as having already drifted once in this codebase.
 *
 * It owns the search, the debounce, the results and the optimistic flip to `Invited`. It owns
 * nothing else: the shared invite link, the pending-invitation list and navigation all belong to
 * the parent, which learns that something happened from {@link invited}.
 */
@Component({
  selector: 'jh-invite-search',
  imports: [ReactiveFormsModule, AlertComponent, TranslocoPipe, IconComponent],
  templateUrl: './invite-search.component.html',
})
export class InviteSearchComponent {
  private readonly teams = inject(TeamService);

  /** The team people are being invited to. Every result's relation is relative to it. */
  readonly slug = input.required<string>();

  /** An invitation was created. The invitations screen reloads its pending list; the wizard ignores it. */
  readonly invited = output<InvitableUser>();

  protected readonly results = signal<InvitableUser[]>([]);
  protected readonly searching = signal(false);
  /**
   * A search has come back. Distinguishes "matched nobody" from "nothing has been searched yet" —
   * without it the empty state would greet a player who has not typed anything.
   */
  protected readonly searched = signal(false);
  /** The search request itself failed. Distinct from "matched nobody", which is an answer. */
  protected readonly searchFailed = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly searchControl = new FormControl('', { nonNullable: true });

  constructor() {
    this.searchControl.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((q) => {
          const term = q.trim();
          if (!term) {
            this.results.set([]);
            this.searched.set(false);
            this.searchFailed.set(false);
            return EMPTY;
          }
          this.searching.set(true);
          this.searchFailed.set(false);
          // Caught **inside** the switchMap. An error allowed to reach the subscriber would tear
          // the whole subscription down, and no later keystroke would ever be searched again —
          // the same hazard `TeamCreateComponent`'s handle check documents, which the version of
          // this code that lived in `TeamInvitationsComponent` did not guard against.
          return this.teams.searchUsers(this.slug(), term).pipe(
            catchError(() => {
              this.searchFailed.set(true);
              return of(null);
            }),
          );
        }),
        takeUntilDestroyed(),
      )
      .subscribe((page) => {
        this.results.set(page?.items ?? []);
        this.searched.set(page !== null);
        this.searching.set(false);
      });
  }

  /**
   * Invite one person. The row is flipped to `Invited` locally rather than re-searched: the
   * server is the authority on the relation, but a whole search round-trip to learn a fact this
   * call just established would make the list flicker for no gain.
   */
  protected invite(user: InvitableUser): void {
    if (user.relation !== 'Invitable') {
      return;
    }
    this.error.set(null);
    this.teams.createTargetedInvite(this.slug(), user.userId).subscribe({
      next: () => {
        this.results.update((rs) =>
          rs.map((r) => (r.userId === user.userId ? { ...r, relation: 'Invited' } : r)),
        );
        this.invited.emit(user);
      },
      // One failed invitation must not disturb the ones that succeeded (FR-026): only the
      // message is set, and every other row keeps whatever relation it had.
      error: (err) => this.error.set(problemDetail(err)),
    });
  }
}
