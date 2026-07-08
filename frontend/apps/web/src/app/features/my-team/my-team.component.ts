import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MembershipService } from '../../core/services/membership.service';

/**
 * The "My team" chooser (feature 008). Reached from the nav when a player is on more than one
 * team (0/1-team players are routed straight to Browse teams / their single team). Lists the
 * player's teams; also degrades gracefully to a find-a-team prompt if reached with no teams.
 */
@Component({
  selector: 'jh-my-team',
  imports: [RouterLink],
  templateUrl: './my-team.component.html',
  styleUrl: './my-team.component.css',
})
export class MyTeamComponent implements OnInit {
  private readonly membership = inject(MembershipService);

  protected readonly teams = this.membership.teams;
  protected readonly loaded = this.membership.loaded;

  ngOnInit(): void {
    if (!this.membership.loaded()) {
      this.membership.load();
    }
  }
}
