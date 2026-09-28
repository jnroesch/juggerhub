import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AlertComponent, ButtonDirective, IconComponent, LoadingComponent } from '../../../shared/ui';
import { TeamDetail } from '../../../core/models/team.models';
import { AuthService } from '../../../core/services/auth.service';
import { MembershipService } from '../../../core/services/membership.service';
import { TeamService } from '../../../core/services/team.service';
import { problemDetail } from '../../../core/utils/problem';

/**
 * US5/US6 — "Manage team". An admin manages the team here (logo, recruitment, step down, delete);
 * since GH #361 EVERY member can open it, because it is also where a member manages their own
 * membership — leaving lives here, tucked one page away from the team page, since it happens
 * rarely. Step down and leave share the last-admin guard (the server's, `MutateMembershipAsync`).
 *
 * Feature 051 adds the team logo: upload, replace, remove. Remove is deliberately NOT a
 * danger-zone control — it is reversible by uploading another image, unlike deleting the team.
 */
@Component({
  selector: 'jh-team-settings',
  imports: [RouterLink, ButtonDirective, LoadingComponent, AlertComponent, TranslocoPipe, IconComponent],
  templateUrl: './team-settings.component.html',
  styleUrl: './team-settings.component.css',
})
export class TeamSettingsComponent {
  private readonly teams = inject(TeamService);
  private readonly auth = inject(AuthService);
  private readonly membership = inject(MembershipService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly slug = signal('');
  protected readonly detail = signal<TeamDetail | null>(null);
  protected readonly soleAdmin = signal(false);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly working = signal(false);
  protected readonly confirmingDelete = signal(false);
  /** GH #361 — the inline leave confirmation is open (same shape as the delete confirmation). */
  protected readonly confirmingLeave = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly beginnersWelcome = signal(false);
  protected readonly savingBeginners = signal(false);
  // Feature 051 — one busy flag for both logo writes, plus which one is running, so the buttons
  // disable together and only the acting button changes its label.
  protected readonly uploadingLogo = signal(false);
  protected readonly removingLogo = signal(false);

  protected readonly isAdmin = computed(() => this.detail()?.myRole === 'Admin');

  protected readonly logoBusy = computed(() => this.uploadingLogo() || this.removingLogo());

  /**
   * Reads the service's per-slug revision signal, so the `<img>` re-fetches after a replace
   * instead of keeping the browser's cached copy of the identical URL (feature 051; GH #283 is
   * the same lesson for avatars).
   */
  protected readonly logoUrl = computed(() => this.teams.logoUrl(this.slug()));

  /** The letter shown while a team has no logo — the same fallback every other surface uses. */
  protected initial(name: string): string {
    return name.trim().charAt(0).toUpperCase() || '?';
  }

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
        this.beginnersWelcome.set(d.beginnersWelcome);
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

  protected toggleBeginnersWelcome(): void {
    const next = !this.beginnersWelcome();
    this.beginnersWelcome.set(next);
    this.savingBeginners.set(true);
    this.error.set(null);
    this.teams.updateSettings(this.slug(), next).subscribe({
      next: () => {
        this.savingBeginners.set(false);
        this.detail.update((d) => (d ? { ...d, beginnersWelcome: next } : d));
      },
      error: (err) => {
        this.beginnersWelcome.set(!next); // revert on failure
        this.savingBeginners.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }

  protected onLogoSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || this.logoBusy()) {
      return;
    }

    this.uploadingLogo.set(true);
    this.error.set(null);
    this.teams.uploadLogo(this.slug(), file).subscribe({
      next: () => {
        // The upload bumped this slug's revision, so `logoUrl` now points at a fresh URL.
        this.detail.update((d) => (d ? { ...d, hasLogo: true } : d));
        // The cached membership carries the hasLogo flag the "My team" rows render, so without
        // this the admin would see their new logo here and the old letter tile there.
        this.membership.load();
        this.uploadingLogo.set(false);
        // Clear the picker so choosing the same file again still fires a change event.
        input.value = '';
      },
      error: (err) => {
        this.uploadingLogo.set(false);
        input.value = '';
        this.error.set(problemDetail(err));
      },
    });
  }

  protected removeLogo(): void {
    if (this.logoBusy()) {
      return;
    }

    this.removingLogo.set(true);
    this.error.set(null);
    this.teams.removeLogo(this.slug()).subscribe({
      next: () => {
        this.detail.update((d) => (d ? { ...d, hasLogo: false } : d));
        this.membership.load();
        this.removingLogo.set(false);
      },
      error: (err) => {
        this.removingLogo.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }

  protected stepDown(): void {
    this.working.set(true);
    this.error.set(null);
    this.teams.stepDown(this.slug()).subscribe({
      next: () => {
        // The cached membership carries the role the nav and "My team" render.
        this.membership.load();
        this.router.navigate(['/t', this.slug()]);
      },
      error: (err) => {
        this.working.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }

  /**
   * GH #361 — leave the team. The member removes THEMSELVES through the endpoint an admin removes
   * anyone with (`DELETE /teams/{slug}/members/{userId}`; the server allows self-removal). An
   * admin who is the only admin is refused with 409, which the disabled control + warning above
   * already explain; the error path still shows the server's reason for the raced case.
   */
  protected leaveTeam(): void {
    const userId = this.auth.currentUser()?.id;
    if (this.working() || !userId) {
      return;
    }
    this.working.set(true);
    this.error.set(null);
    this.teams.removeMember(this.slug(), userId).subscribe({
      next: () => {
        // The cached memberships name this team in the nav and the team chooser (023 FR-017).
        this.membership.load();
        this.router.navigate(['/my-team']);
      },
      error: (err) => {
        this.working.set(false);
        this.confirmingLeave.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }

  protected deleteTeam(): void {
    this.working.set(true);
    this.error.set(null);
    this.teams.deleteTeam(this.slug()).subscribe({
      next: () => {
        // The team is gone — drop it from the cache, or the nav keeps deep-linking to a dead /t/:slug.
        this.membership.load();
        this.router.navigate(['/']);
      },
      error: (err) => {
        this.working.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }
}
