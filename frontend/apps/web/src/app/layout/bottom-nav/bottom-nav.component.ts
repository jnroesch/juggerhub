import { Component, inject } from '@angular/core';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map } from 'rxjs';
import { MembershipService } from '../../core/services/membership.service';
import { NavId, isActiveDestination } from '../nav-model';

/**
 * The mobile bottom tab bar (feature 008): Home · Browse · My team · Alerts. Four thumb-reachable
 * fixed tabs (≤5), hidden at desktop width where the top bar carries the destinations.
 */
@Component({
  selector: 'jh-bottom-nav',
  imports: [RouterLink],
  templateUrl: './bottom-nav.component.html',
  styleUrl: './bottom-nav.component.css',
})
export class BottomNavComponent {
  private readonly router = inject(Router);
  private readonly membership = inject(MembershipService);

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map(() => this.router.url),
    ),
    { initialValue: this.router.url },
  );

  protected readonly myTeamHref = this.membership.myTeamTarget;

  protected active(id: NavId): boolean {
    return isActiveDestination(id, this.url());
  }
}
