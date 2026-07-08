import { Component, computed, inject } from '@angular/core';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map } from 'rxjs';
import { MembershipService } from '../../core/services/membership.service';
import { AvatarMenuComponent } from '../avatar-menu/avatar-menu.component';
import { NavId, isActiveDestination } from '../nav-model';

/**
 * The single top bar (feature 008). On desktop it carries the brand, the primary destinations
 * (Home · Browse · My team), the notifications bell, and the avatar menu. On mobile it shrinks
 * to a slim strip (wordmark + avatar); the destinations move to the bottom tab bar.
 */
@Component({
  selector: 'jh-top-nav',
  imports: [RouterLink, AvatarMenuComponent],
  templateUrl: './top-nav.component.html',
  styleUrl: './top-nav.component.css',
})
export class TopNavComponent {
  private readonly router = inject(Router);
  private readonly membership = inject(MembershipService);

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map(() => this.router.url),
    ),
    { initialValue: this.router.url },
  );

  /** "My team" resolves 0/1/many memberships to a concrete route. */
  protected readonly myTeamHref = this.membership.myTeamTarget;

  protected active(id: NavId): boolean {
    return isActiveDestination(id, this.url());
  }

  protected readonly homeActive = computed(() => this.active('home'));
  protected readonly browseActive = computed(() => this.active('browse'));
  protected readonly myTeamActive = computed(() => this.active('my-team'));
  protected readonly alertsActive = computed(() => this.active('alerts'));
}
