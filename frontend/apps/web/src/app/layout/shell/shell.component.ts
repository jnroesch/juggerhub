import { Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TopNavComponent } from '../top-nav/top-nav.component';
import { BottomNavComponent } from '../bottom-nav/bottom-nav.component';
import { AuthService } from '../../core/services/auth.service';
import { MembershipService } from '../../core/services/membership.service';

/**
 * Application shell (feature 008) — one top bar on desktop, a bottom tab bar on mobile, around
 * the routed content. The old sidebar drawer is gone. Hydrates the session once and, when
 * signed in, loads the player's team memberships so "My team" routes correctly everywhere.
 */
@Component({
  selector: 'jh-shell',
  imports: [RouterOutlet, TopNavComponent, BottomNavComponent],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.css',
})
export class ShellComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly membership = inject(MembershipService);

  ngOnInit(): void {
    this.auth.loadSession().subscribe((user) => {
      if (user) {
        this.membership.load();
      }
    });
  }
}
