import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../core/services/auth.service';
import { LanguageSwitcherComponent } from '../settings/language/language-switcher.component';
import { LegalLinksComponent } from '../../shared/ui';
import { DeleteAccountComponent } from './delete-account.component';

/**
 * The signed-in member's own account settings. Reachable only when authenticated (see authGuard).
 * Also the home of the danger zone (feature 037) — deleting your account is an account setting, so
 * it lives with the others rather than being hidden somewhere it has to be hunted for (FR-001).
 */
@Component({
  selector: 'jh-account',
  imports: [
    RouterLink,
    TranslocoPipe,
    LanguageSwitcherComponent,
    LegalLinksComponent,
    DeleteAccountComponent,
  ],
  templateUrl: './account.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './account.component.css',
})
export class AccountComponent {
  private readonly auth = inject(AuthService);

  protected readonly user = this.auth.currentUser;
}
