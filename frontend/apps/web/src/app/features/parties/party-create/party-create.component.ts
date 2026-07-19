import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { PartyContext, PartyContextTeam } from '../../../core/models/party.models';
import { PartyService } from '../../../core/services/party.service';

/**
 * "Form a party" (feature 016 · wireframe 6a). Launched from a teams-only event by a team admin.
 * Picks which administered team enters (auto-selected when there's only one), shows the event's
 * read-only roster cap, and takes a message. Submitting forms the party (which posts the team
 * request) and navigates to the manage hub — it does NOT yet enter the team on the event.
 */
@Component({
  selector: 'jh-party-create',
  imports: [FormsModule],
  templateUrl: './party-create.component.html',
  styleUrl: './party-create.component.css',
})
export class PartyCreateComponent implements OnInit {
  private readonly parties = inject(PartyService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly context = signal<PartyContext | null>(null);
  protected readonly loading = signal(true);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected selectedTeamId = '';
  protected message = '';

  private eventId = '';

  /** Teams the viewer administers that don't already have a party for this event. */
  protected readonly formableTeams = computed<PartyContextTeam[]>(
    () => this.context()?.teams.filter((t) => t.canForm) ?? [],
  );

  ngOnInit(): void {
    this.eventId = this.route.snapshot.paramMap.get('id') ?? '';
    this.parties.getPartyContext(this.eventId).subscribe({
      next: (ctx) => {
        this.context.set(ctx);
        const formable = ctx.teams.filter((t) => t.canForm);
        if (formable.length === 1) {
          this.selectedTeamId = formable[0].teamId;
        }
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected submit(): void {
    if (this.submitting() || !this.selectedTeamId) {
      return;
    }
    this.submitting.set(true);
    this.error.set(null);
    this.parties.formParty({ eventId: this.eventId, teamId: this.selectedTeamId, message: this.message || null }).subscribe({
      next: (party) => this.router.navigate(['/parties', party.id]),
      error: (err) => {
        this.submitting.set(false);
        this.error.set(err instanceof HttpErrorResponse ? err.error?.detail ?? 'Could not form the party.' : 'Could not form the party.');
      },
    });
  }

  protected cancel(): void {
    this.router.navigate(['/events', this.eventId]);
  }
}
