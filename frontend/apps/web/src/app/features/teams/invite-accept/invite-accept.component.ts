import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { InvitePreview } from '../../../core/models/team.models';
import { TeamService } from '../../../core/services/team.service';
import { problemDetail } from '../../../core/utils/problem';

/**
 * US4 — the invitee's landing page (full-screen, anonymous). Shows the team's public
 * info + who invited them, with Accept & join / Decline. Expired/invalid links show a
 * terminal state. Accepting while signed out bounces to sign-in and returns here.
 */
@Component({
  selector: 'jh-invite-accept',
  imports: [RouterLink],
  templateUrl: './invite-accept.component.html',
  styleUrl: './invite-accept.component.css',
})
export class InviteAcceptComponent {
  private readonly teams = inject(TeamService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly token = signal('');
  protected readonly preview = signal<InvitePreview | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly working = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((pm) => {
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
      },
      error: () => {
        this.loading.set(false);
        this.notFound.set(true);
      },
    });
  }

  protected accept(): void {
    this.working.set(true);
    this.error.set(null);
    this.teams.acceptInvite(this.token()).subscribe({
      next: (r) => this.router.navigateByUrl(`/t/${r.teamSlug}`),
      error: (err) => {
        this.working.set(false);
        if (err?.status === 401) {
          this.router.navigate(['/sign-in'], { queryParams: { returnUrl: this.router.url } });
        } else {
          this.error.set(problemDetail(err));
        }
      },
    });
  }

  protected decline(): void {
    this.working.set(true);
    this.teams.declineInvite(this.token()).subscribe({
      next: () => this.router.navigate(['/']),
      error: () => this.router.navigate(['/']),
    });
  }
}
