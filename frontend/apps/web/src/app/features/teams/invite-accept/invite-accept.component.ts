import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { InvitePreview } from '../../../core/models/team.models';
import { AuthService } from '../../../core/services/auth.service';
import { TeamService } from '../../../core/services/team.service';
import { problemDetail } from '../../../core/utils/problem';

type PendingAction = 'accept' | 'decline';

/**
 * US4 — the invitee's landing page (full-screen, anonymous). Shows the team's public
 * info + who invited them, with Accept & join / Decline. Expired/invalid links show a
 * terminal state.
 *
 * Accept/decline require a session, so we check it BEFORE calling the API (avoiding a
 * 401 that the interceptor would turn into a context-losing sign-in redirect). A signed-out
 * visitor is sent to sign-in with a returnUrl back here carrying the intended action, which
 * is then resumed automatically after they authenticate.
 */
@Component({
  selector: 'jh-invite-accept',
  imports: [RouterLink],
  templateUrl: './invite-accept.component.html',
  styleUrl: './invite-accept.component.css',
})
export class InviteAcceptComponent {
  private readonly teams = inject(TeamService);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly slug = signal('');
  protected readonly token = signal('');
  protected readonly preview = signal<InvitePreview | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly working = signal(false);
  protected readonly error = signal<string | null>(null);

  private resumed = false;

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((pm) => {
      this.slug.set(pm.get('slug') ?? '');
      this.token.set(pm.get('token') ?? '');
      this.load();
    });
  }

  private load(): void {
    this.loading.set(true);
    this.notFound.set(false);
    this.teams.getInvitePreview(this.token()).subscribe({
      next: (p) => {
        this.preview.set(p);
        this.loading.set(false);
        this.maybeResume();
      },
      error: () => {
        this.loading.set(false);
        this.notFound.set(true);
      },
    });
  }

  /** After returning from sign-in (?action=…), finish the invite automatically if signed in. */
  private maybeResume(): void {
    if (this.resumed) {
      return;
    }
    const action = this.route.snapshot.queryParamMap.get('action');
    if (action !== 'accept' && action !== 'decline') {
      return;
    }
    this.resumed = true;
    this.auth.ensureSession().subscribe((user) => {
      if (!user) {
        return; // still signed out — let them use the buttons
      }
      if (action === 'accept' && this.preview()?.state === 'Usable') {
        this.doAccept();
      } else if (action === 'decline') {
        this.doDecline();
      }
    });
  }

  protected accept(): void {
    this.gated('accept', () => this.doAccept());
  }

  protected decline(): void {
    this.gated('decline', () => this.doDecline());
  }

  /** Run the action if signed in; otherwise bounce to sign-in and return here to resume it. */
  private gated(action: PendingAction, run: () => void): void {
    this.working.set(true);
    this.auth.ensureSession().subscribe((user) => {
      if (user) {
        run();
      } else {
        this.working.set(false);
        this.router.navigate(['/sign-in'], {
          queryParams: { returnUrl: `/join/${this.slug()}/${this.token()}?action=${action}` },
        });
      }
    });
  }

  private doAccept(): void {
    this.working.set(true);
    this.error.set(null);
    this.teams.acceptInvite(this.token()).subscribe({
      next: (r) => this.router.navigateByUrl(`/t/${r.teamSlug}`),
      error: (err) => {
        this.working.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }

  private doDecline(): void {
    this.working.set(true);
    this.teams.declineInvite(this.token()).subscribe({
      next: () => this.router.navigate(['/']),
      error: () => this.router.navigate(['/']),
    });
  }
}
