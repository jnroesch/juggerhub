import { Component, OnInit, computed, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { TopNavComponent } from '../top-nav/top-nav.component';
import { BottomNavComponent } from '../bottom-nav/bottom-nav.component';
import { AuthService } from '../../core/services/auth.service';
import { MembershipService } from '../../core/services/membership.service';

/**
 * Application shell (feature 008) — one top bar on desktop, a bottom tab bar on mobile, around
 * the routed content. Hydrates the session once and, when signed in, loads the player's team
 * memberships so "My team" routes correctly everywhere.
 *
 * Anonymous visitors can only reach one shell route: a public profile at `/u/:handle` (feature
 * 026). For them the full nav is replaced by a slim "Sign in / Register" bar and the mobile tab
 * bar is hidden, so a shared profile reads as a clean page rather than a wall of links that all
 * bounce to sign-in.
 */
@Component({
  selector: 'jh-shell',
  imports: [RouterOutlet, RouterLink, TopNavComponent, BottomNavComponent],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.css',
})
export class ShellComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly membership = inject(MembershipService);

  /** Known-anonymous (probed and null). Undefined (not yet probed) keeps the full nav to avoid a flash. */
  protected readonly anonymous = computed(() => this.auth.userState() === null);

  ngOnInit(): void {
    this.auth.loadSession().subscribe((user) => {
      if (user) {
        this.membership.load();
      }
    });
  }
}
