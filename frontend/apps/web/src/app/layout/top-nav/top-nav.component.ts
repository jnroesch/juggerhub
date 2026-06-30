import { Component, inject, output } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

/**
 * Top navigation bar. Shows the brand, a mobile menu button, and the auth control:
 * the signed-in user's email + sign-out, or a sign-in link.
 */
@Component({
  selector: 'jh-top-nav',
  imports: [RouterLink],
  templateUrl: './top-nav.component.html',
  styleUrl: './top-nav.component.css',
})
export class TopNavComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly menuToggle = output<void>();
  protected readonly user = this.auth.currentUser;

  signOut(): void {
    this.auth.logout().subscribe({
      next: () => this.router.navigate(['/sign-in']),
      error: () => this.router.navigate(['/sign-in']),
    });
  }
}
