import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TeamDetail } from '../../../core/models/team.models';
import { TeamService } from '../../../core/services/team.service';
import { problemDetail } from '../../../core/utils/problem';

/**
 * US5/US6 — team settings. Step down to member (blocked if you're the only admin —
 * the last-admin guard), and the danger-zone delete (admins only, irreversible).
 */
@Component({
  selector: 'jh-team-settings',
  imports: [RouterLink],
  templateUrl: './team-settings.component.html',
  styleUrl: './team-settings.component.css',
})
export class TeamSettingsComponent {
  private readonly teams = inject(TeamService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly slug = signal('');
  protected readonly detail = signal<TeamDetail | null>(null);
  protected readonly soleAdmin = signal(false);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly working = signal(false);
  protected readonly confirmingDelete = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly isAdmin = computed(() => this.detail()?.myRole === 'Admin');

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((pm) => {
      this.slug.set(pm.get('slug') ?? '');
      this.load();
    });
  }

  private load(): void {
    this.loading.set(true);
    this.notFound.set(false);
    this.teams.getDetail(this.slug()).subscribe({
      next: (d) => {
        this.detail.set(d);
        this.loading.set(false);
        if (d.myRole === 'Admin') {
          this.teams.getMembers(this.slug()).subscribe({
            next: (p) => this.soleAdmin.set(p.items.filter((m) => m.role === 'Admin').length <= 1),
          });
        }
      },
      error: () => {
        this.loading.set(false);
        this.notFound.set(true);
      },
    });
  }

  protected stepDown(): void {
    this.working.set(true);
    this.error.set(null);
    this.teams.stepDown(this.slug()).subscribe({
      next: () => this.router.navigate(['/t', this.slug()]),
      error: (err) => {
        this.working.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }

  protected deleteTeam(): void {
    this.working.set(true);
    this.error.set(null);
    this.teams.deleteTeam(this.slug()).subscribe({
      next: () => this.router.navigate(['/']),
      error: (err) => {
        this.working.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }
}
