import { Component, ElementRef, HostListener, computed, effect, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { MembershipService } from '../../core/services/membership.service';
import { ProfileService } from '../../core/services/profile.service';
import { RecognitionAdminService } from '../../core/services/recognition-admin.service';

/**
 * The account menu under the player's avatar (feature 008): Profile · Account · Sign out.
 * Profile lives here, not as a primary nav destination. Keyboard-navigable; closes on
 * outside click or Escape.
 */
@Component({
  selector: 'jh-avatar-menu',
  imports: [RouterLink],
  templateUrl: './avatar-menu.component.html',
  styleUrl: './avatar-menu.component.css',
})
export class AvatarMenuComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly membership = inject(MembershipService);
  private readonly admin = inject(RecognitionAdminService);
  private readonly profiles = inject(ProfileService);
  private readonly host = inject(ElementRef<HTMLElement>);

  protected readonly open = signal(false);
  protected readonly user = this.auth.currentUser;
  /** Whether to show the (server-enforced) Admin panel entry. UX gating only. */
  protected readonly isAdmin = this.admin.isAdmin;
  /** A single letter for the avatar circle (from the signed-in email) — shown when there's no image. */
  protected readonly initial = computed(() => (this.user()?.email ?? '?').charAt(0).toUpperCase());

  /** The last avatar URL that failed to load; the initial stands in for it until the URL changes. */
  private readonly failedAvatarUrl = signal<string | null>(null);

  /**
   * The player's own avatar, the same image their profile shows (GH #283). Cache-busted by the
   * session's upload revision, so a new upload replaces it here without a reload.
   */
  protected readonly avatarUrl = computed(() => {
    const user = this.user();
    if (!user?.hasAvatar) {
      return null;
    }
    const url = this.profiles.ownAvatarUrl(user.handle);
    return url === this.failedAvatarUrl() ? null : url;
  });

  constructor() {
    // Probe admin access once the user is known (authed users only; result is cached).
    effect(() => {
      if (this.auth.currentUser()) {
        this.admin.checkAccess().subscribe();
      }
    });
  }

  toggle(): void {
    this.open.update((o) => !o);
  }

  close(): void {
    this.open.set(false);
  }

  /** An avatar that can't be served (e.g. a storage outage) falls back to the initial, not a broken image. */
  protected onAvatarError(url: string): void {
    this.failedAvatarUrl.set(url);
  }

  signOut(): void {
    this.close();
    this.membership.clear();
    this.auth.logout().subscribe({
      next: () => this.router.navigate(['/sign-in']),
      error: () => this.router.navigate(['/sign-in']),
    });
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) {
      this.close();
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.close();
  }
}
