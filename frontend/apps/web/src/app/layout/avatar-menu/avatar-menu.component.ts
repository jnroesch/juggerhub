import { Component, ElementRef, HostListener, computed, effect, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { MembershipService } from '../../core/services/membership.service';
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
  private readonly host = inject(ElementRef<HTMLElement>);

  protected readonly open = signal(false);
  protected readonly user = this.auth.currentUser;
  /** Whether to show the (server-enforced) Admin panel entry. UX gating only. */
  protected readonly isAdmin = this.admin.isAdmin;
  /** A single letter for the avatar circle (from the signed-in email). */
  protected readonly initial = computed(() => (this.user()?.email ?? '?').charAt(0).toUpperCase());

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
