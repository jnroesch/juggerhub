import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LoadingComponent, CardComponent } from '../../../shared/ui';
import { ProfileView, PublicProfile } from '../../../core/models/profile.models';
import { AuthService } from '../../../core/services/auth.service';
import { ProfileService } from '../../../core/services/profile.service';
import { ProfileViewComponent } from '../components/profile-view/profile-view.component';
import { ProfileQuickActionsComponent } from '../components/quick-actions/profile-quick-actions.component';

/**
 * Read-only view of another player's profile, rendered in-shell at /u/:handle (feature 026 —
 * the profile page hosts this for non-owners). Shows only the public field set (the server
 * strips email/account data).
 *
 * On a 404 (missing profile, or — for a signed-out visitor — a private one, which the server
 * makes indistinguishable): a signed-in viewer gets a genuine not-found; a signed-out visitor is
 * redirected to sign-in with a returnUrl back here, so they can view it after logging in if it exists.
 */
@Component({
  selector: 'jh-profile-public',
  imports: [RouterLink, ProfileViewComponent, ProfileQuickActionsComponent, LoadingComponent, CardComponent],
  templateUrl: './profile-public.component.html',
  styleUrl: './profile-public.component.css',
})
export class ProfilePublicComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly profiles = inject(ProfileService);
  private readonly auth = inject(AuthService);

  protected readonly profile = signal<PublicProfile | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);

  /** Map the public DTO to the shared, read-only view model (feature 026). */
  protected readonly view = computed<ProfileView | null>(() => {
    const p = this.profile();
    if (!p) {
      return null;
    }
    return {
      handle: p.handle,
      displayName: p.displayName,
      hometown: p.hometown,
      description: p.description,
      avatarUrl: p.hasAvatar ? this.profiles.avatarUrl(p.handle) : null,
      pompfen: p.selectedPompfen,
      teams: p.teams,
      recentActivity: p.recentActivity,
      badges: p.badges,
      achievements: p.achievements,
    };
  });

  private readonly handle = this.route.snapshot.paramMap.get('handle') ?? '';

  constructor() {
    // Resolve the session first so the 404 branch can tell a signed-in viewer (→ not-found) from a
    // signed-out one (→ sign-in redirect) without racing the initial session probe.
    this.auth.ensureSession().subscribe(() => this.load());
  }

  private load(): void {
    this.profiles.getPublic(this.handle).subscribe({
      next: (p) => {
        this.profile.set(p);
        this.loading.set(false);
      },
      error: () => {
        if (this.auth.isAuthenticated()) {
          this.notFound.set(true);
          this.loading.set(false);
        } else {
          void this.router.navigate(['/sign-in'], { queryParams: { returnUrl: `/u/${this.handle}` } });
        }
      },
    });
  }
}
