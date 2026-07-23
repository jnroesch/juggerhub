import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { LoadingComponent } from '../../../shared/ui';
import { AuthService } from '../../../core/services/auth.service';
import { ProfileOwnerComponent } from '../profile-owner/profile-owner.component';
import { ProfilePublicComponent } from '../profile-public/profile-public.component';

type ProfileMode = 'loading' | 'owner' | 'viewer';

/**
 * The single profile page at /u/:handle (feature 026). One URL for every profile:
 * - the owner viewing their own handle gets the editable owner view (edit + visibility toggle);
 * - anyone else gets the read-only view;
 * - a signed-out visitor to a private/unknown profile is redirected to sign-in (handled by the
 *   read-only view once it 404s).
 *
 * Rendered in-shell, so an authenticated viewer keeps full navigation; a signed-out viewer of a
 * public profile sees the shell's slim public bar (see ShellComponent).
 */
@Component({
  selector: 'jh-profile-page',
  imports: [ProfileOwnerComponent, ProfilePublicComponent, LoadingComponent],
  templateUrl: './profile-page.component.html',
  styleUrl: './profile-page.component.css',
})
export class ProfilePageComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly auth = inject(AuthService);

  protected readonly mode = signal<ProfileMode>('loading');

  constructor() {
    const handle = this.route.snapshot.paramMap.get('handle') ?? '';
    // Resolve the session, then choose owner vs viewer. ensureSession is cached, so this is a
    // no-op probe once the shell has hydrated.
    this.auth.ensureSession().subscribe((user) => {
      this.mode.set(user && user.handle === handle ? 'owner' : 'viewer');
    });
  }
}
