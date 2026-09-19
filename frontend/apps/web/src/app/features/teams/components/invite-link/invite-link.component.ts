import { Component, effect, inject, input, output, signal } from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { catchError, of } from 'rxjs';
import { InviteLink } from '../../../../core/models/team.models';
import { TeamService } from '../../../../core/services/team.service';
import { problemDetail } from '../../../../core/utils/problem';
import { AlertComponent, ButtonDirective } from '../../../../shared/ui';
import { expiresIn } from '../../invitation-expiry';

/**
 * The team's one shareable invite link: create it, copy it, replace it (feature 052, extracted
 * from `TeamInvitationsComponent`).
 *
 * Two screens render this: the team's invitations screen, and the create wizard's last step,
 * where a link is the only way to reach somebody who has no JuggerHub account yet — the search
 * beside it can only find players who do. It is a component rather than a block copied twice
 * because of the rule it owns: **one live link per team**, so creating and replacing are the same
 * request (`POST .../invitations/link`), and a replacement silently retires the previous link.
 * That is the same duplication class feature 046 recorded as having drifted once already.
 *
 * It owns the link and nothing else. The pending-invitation list belongs to whichever parent has
 * one, and learns that the link changed from {@link changed} — a new link appears there as a
 * `Link` row, and the one it replaced disappears.
 */
@Component({
  selector: 'jh-invite-link',
  imports: [AlertComponent, ButtonDirective, TranslocoPipe],
  templateUrl: './invite-link.component.html',
})
export class InviteLinkComponent {
  private readonly teams = inject(TeamService);
  private readonly t = inject(TranslocoService);

  /** The team the link admits people to. Admin-only server-side, like every other action here. */
  readonly slug = input.required<string>();

  /** A link was created or replaced. The invitations screen re-reads its pending list. */
  readonly changed = output<InviteLink>();

  protected readonly link = signal<InviteLink | null>(null);
  /**
   * The first read has come back. Distinguishes "this team has no link" from "we have not asked
   * yet" — without it the wizard would offer *Create an invite link* for a moment and then
   * replace it with a link, which is the one thing a person is most likely to click too early.
   */
  protected readonly loaded = signal(false);
  protected readonly working = signal(false);
  protected readonly copied = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    // Reads the link for whichever team it is pointed at, and again if that ever changes.
    effect(() => this.reload());
  }

  /**
   * Re-read the team's active link. Public because the link can also be retired from outside
   * this block: the invitations screen lists it among the pending invitations, where it can be
   * revoked — and a block still offering a link nobody can accept is worse than no block at all.
   */
  reload(): void {
    this.loaded.set(false);
    this.link.set(null);
    this.teams
      .getInviteLink(this.slug())
      // A read that fails leaves the block on its create control rather than breaking the screen
      // around it: pressing that is a real attempt, and reports its own refusal.
      .pipe(catchError(() => of(null)))
      .subscribe((l) => {
        this.link.set(l);
        this.loaded.set(true);
      });
  }

  protected expiresIn(iso: string): string {
    return expiresIn(iso, this.t);
  }

  /**
   * Copy the link. `navigator.clipboard` is absent in an insecure context and can be refused by
   * permission, so "Copied!" is said only once the write has actually resolved — claiming it
   * regardless is a promise the person then acts on, and the paste is whatever was there before.
   */
  protected copyLink(): void {
    const l = this.link();
    if (!l) {
      return;
    }
    this.error.set(null);
    const write = navigator.clipboard?.writeText(l.url);
    if (!write) {
      this.error.set(this.t.translate('teams.invitations.copyFailed'));
      return;
    }
    write.then(
      () => {
        this.copied.set(true);
        setTimeout(() => this.copied.set(false), 1500);
      },
      () => this.error.set(this.t.translate('teams.invitations.copyFailed')),
    );
  }

  /**
   * Create the link, or replace the one there is — the same request either way, which is what
   * keeps "one live link per team" a property of the API rather than a convention.
   *
   * **Never retried automatically**: a replayed `POST` would retire the link the first attempt
   * had just handed back, and anything already shared with it. The retry is the person pressing
   * again, on a failure they can see (Principle VII's browser-hop rule).
   */
  protected rotate(): void {
    if (this.working()) {
      return;
    }
    this.working.set(true);
    this.error.set(null);
    this.teams.rotateInviteLink(this.slug()).subscribe({
      next: (l) => {
        this.link.set(l);
        this.loaded.set(true);
        this.working.set(false);
        this.changed.emit(l);
      },
      error: (err) => {
        this.working.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }
}
