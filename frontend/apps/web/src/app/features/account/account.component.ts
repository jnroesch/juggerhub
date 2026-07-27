import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../core/services/auth.service';
import { LanguageSwitcherComponent } from '../settings/language/language-switcher.component';

/**
 * Guarded sample page — reachable only when authenticated (see authGuard). It
 * exists to demonstrate the route guard; unauthenticated visitors are redirected
 * toward sign-in instead of seeing this content.
 */
@Component({
  selector: 'jh-account',
  imports: [RouterLink, TranslocoPipe, LanguageSwitcherComponent],
  templateUrl: './account.component.html',
  styleUrl: './account.component.css',
})
export class AccountComponent {
  private readonly auth = inject(AuthService);

  protected readonly user = this.auth.currentUser;
}
