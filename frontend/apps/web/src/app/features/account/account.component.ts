import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

/**
 * Guarded sample page — reachable only when authenticated (see authGuard). It
 * exists to demonstrate the route guard; unauthenticated visitors are redirected
 * toward sign-in instead of seeing this content.
 */
@Component({
  selector: 'jh-account',
  imports: [RouterLink],
  templateUrl: './account.component.html',
  styleUrl: './account.component.css',
})
export class AccountComponent {
  private readonly auth = inject(AuthService);

  protected readonly user = this.auth.currentUser;
}
