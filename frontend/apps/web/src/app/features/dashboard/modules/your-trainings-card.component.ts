import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TrainingsService } from '../../../core/services/trainings.service';
import { AgendaSession, TrainingRsvp } from '../../../core/models/trainings.models';

/**
 * The "Your trainings" dashboard agenda (feature 018): the member's next sessions across every team plus
 * public sessions they joined as a guest, chronological, each with an inline Going/Maybe/Can't so they
 * can answer straight from home. Empty until the member has an upcoming session.
 */
@Component({
  selector: 'jh-your-trainings-card',
  imports: [RouterLink],
  templateUrl: './your-trainings-card.component.html',
  styleUrl: './your-trainings-card.component.css',
})
export class YourTrainingsCardComponent {
  private readonly trainings = inject(TrainingsService);

  protected readonly items = signal<AgendaSession[]>([]);
  protected readonly loading = signal(true);
  protected readonly busyId = signal<string | null>(null);

  constructor() {
    this.trainings.myAgenda(0, 4).subscribe({
      next: (p) => {
        this.items.set(p.items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected respond(item: AgendaSession, answer: TrainingRsvp): void {
    if (this.busyId()) {
      return;
    }
    this.busyId.set(item.sessionId);
    this.trainings.respond(item.sessionId, answer).subscribe({
      next: (row) => {
        this.items.update((list) => list.map((i) => (i.sessionId === item.sessionId ? { ...i, myAnswer: row.myAnswer, goingCount: row.goingCount } : i)));
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }

  protected time(t: string): string {
    return t.slice(0, 5);
  }

  protected shortDate(date: string): string {
    return new Date(`${date}T00:00:00`).toLocaleDateString(undefined, { weekday: 'short', day: 'numeric', month: 'short' });
  }

  protected place(item: AgendaSession): string | null {
    return item.locationKind === 'Virtual' ? 'Online' : item.location;
  }
}
