import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ButtonDirective, LoadingComponent, AlertComponent } from '../../../shared/ui';
import { EMPTY, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { InvitableUser, InviteLink, TeamInvitation } from '../../../core/models/team.models';
import { TeamService } from '../../../core/services/team.service';
import { problemDetail } from '../../../core/utils/problem';

/**
 * US3 — invite people. The single reusable invite link (copy / regenerate / revoke),
 * the pending-invite list, and a user search to invite players directly (emailed).
 * Admin-only; the API returns 403/404 for anyone else.
 */
@Component({
  selector: 'jh-team-invitations',
  imports: [ReactiveFormsModule, RouterLink, ButtonDirective, LoadingComponent, AlertComponent],
  templateUrl: './team-invitations.component.html',
  styleUrl: './team-invitations.component.css',
})
export class TeamInvitationsComponent {
  private readonly teams = inject(TeamService);
  private readonly route = inject(ActivatedRoute);

  protected readonly slug = signal('');
  protected readonly link = signal<InviteLink | null>(null);
  protected readonly pending = signal<TeamInvitation[]>([]);
  protected readonly results = signal<InvitableUser[]>([]);
  protected readonly searching = signal(false);
  protected readonly copied = signal(false);
  protected readonly loading = signal(true);
  protected readonly denied = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly searchControl = new FormControl('', { nonNullable: true });

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((pm) => {
      this.slug.set(pm.get('slug') ?? '');
      this.load();
    });

    this.searchControl.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((q) => {
          const term = q.trim();
          if (!term) {
            this.results.set([]);
            return EMPTY;
          }
          this.searching.set(true);
          return this.teams.searchUsers(this.slug(), term);
        }),
        takeUntilDestroyed(),
      )
      .subscribe({
        next: (p) => {
          this.results.set(p.items);
          this.searching.set(false);
        },
        error: () => this.searching.set(false),
      });
  }

  private load(): void {
    this.loading.set(true);
    this.denied.set(false);
    this.teams.getInvitations(this.slug()).subscribe({
      next: (p) => {
        this.pending.set(p.items);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.denied.set(true);
      },
    });
    this.teams.getInviteLink(this.slug()).subscribe({ next: (l) => this.link.set(l) });
  }

  private reload(): void {
    this.teams.getInvitations(this.slug()).subscribe({ next: (p) => this.pending.set(p.items) });
    this.teams.getInviteLink(this.slug()).subscribe({ next: (l) => this.link.set(l) });
  }

  protected copyLink(): void {
    const l = this.link();
    if (!l) {
      return;
    }
    navigator.clipboard?.writeText(l.url);
    this.copied.set(true);
    setTimeout(() => this.copied.set(false), 1500);
  }

  protected rotate(): void {
    this.error.set(null);
    this.teams.rotateInviteLink(this.slug()).subscribe({
      next: (l) => {
        this.link.set(l);
        this.reload();
      },
      error: (err) => this.error.set(problemDetail(err)),
    });
  }

  protected revoke(id: string): void {
    this.error.set(null);
    this.teams.revokeInvite(this.slug(), id).subscribe({
      next: () => this.reload(),
      error: (err) => this.error.set(problemDetail(err)),
    });
  }

  protected invite(user: InvitableUser): void {
    if (user.relation !== 'Invitable') {
      return;
    }
    this.error.set(null);
    this.teams.createTargetedInvite(this.slug(), user.userId).subscribe({
      next: () => {
        this.results.update((rs) => rs.map((r) => (r.userId === user.userId ? { ...r, relation: 'Invited' } : r)));
        this.reload();
      },
      error: (err) => this.error.set(problemDetail(err)),
    });
  }

  protected expiresIn(iso: string): string {
    const days = Math.ceil((new Date(iso).getTime() - Date.now()) / 86_400_000);
    if (days <= 0) {
      return 'expires today';
    }
    if (days === 1) {
      return 'expires tomorrow';
    }
    return `expires in ${days} days`;
  }
}
