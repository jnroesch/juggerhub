import { Component, computed, inject, input, signal } from '@angular/core';
import { ButtonDirective } from '../../../shared/ui';
import { RouterLink } from '@angular/router';
import { EventService } from '../../../core/services/event.service';
import { Signup, SignupStatus } from '../../../core/models/event.models';
import { AgendaItem } from '../../../core/models/home.models';
import { TrainingRsvp } from '../../../core/models/trainings.models';
import { TrainingsService } from '../../../core/services/trainings.service';
import { dayOfMonth, shortWeekday, timeHm } from '../../../core/utils/format';

/**
 * One "Up next" / "Open to everyone" agenda item (feature 008, unified by feature 025). An Event item
 * carries a one-tap RSVP that toggles to "going" and back (a brief inline confirm guards withdrawal),
 * reusing the event sign-up endpoints; team-mode events are read-only ("your team is going"). A
 * Training item carries an inline going/maybe/can't answer via the training response endpoint.
 */
@Component({
  selector: 'jh-up-next-card',
  imports: [RouterLink, ButtonDirective],
  templateUrl: './up-next-card.component.html',
  styleUrl: './up-next-card.component.css',
})
export class UpNextCardComponent {
  private readonly events = inject(EventService);
  private readonly trainings = inject(TrainingsService);

  readonly item = input.required<AgendaItem>();

  protected readonly isTraining = computed(() => this.item().kind === 'Training');

  /** Local override of the viewer's sign-up state after an optimistic RSVP/withdraw. */
  private readonly override = signal<{ signupId: string | null; status: SignupStatus | null } | null>(null);
  protected readonly busy = signal(false);
  protected readonly confirming = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly isTeamMode = computed(() => this.item().teamGoing != null);
  private readonly viewer = computed(
    () => this.override() ?? { signupId: this.item().viewerSignupId, status: this.item().viewerStatus },
  );
  protected readonly isGoing = computed(() => this.viewer().signupId != null);
  protected readonly statusLabel = computed(() => {
    switch (this.viewer().status) {
      case 'Waitlisted':
        return 'Waitlisted';
      case 'AwaitingApproval':
        return 'Pending';
      default:
        return 'Going';
    }
  });

  // --- Training answer (local override after an inline response) ---
  private readonly answerOverride = signal<TrainingRsvp | null | undefined>(undefined);
  protected readonly answer = computed(() => {
    const o = this.answerOverride();
    return o === undefined ? this.item().myAnswer : o;
  });

  protected readonly weekday = computed(() => shortWeekday(this.item().startsAt));
  protected readonly day = computed(() => dayOfMonth(this.item().startsAt));
  protected readonly time = computed(() => timeHm(this.item().startsAt));

  /** The route target for the item's title, by kind. */
  protected readonly route = computed<unknown[]>(() =>
    this.isTraining() ? ['/trainings/sessions', this.item().id] : ['/events', this.item().id],
  );

  rsvp(): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    this.events.signup(this.item().id, null).subscribe({
      next: (s: Signup) => {
        this.override.set({ signupId: s.id, status: s.status });
        this.busy.set(false);
      },
      error: () => {
        this.error.set("Couldn't RSVP — try again.");
        this.busy.set(false);
      },
    });
  }

  askWithdraw(): void {
    this.confirming.set(true);
  }

  cancelWithdraw(): void {
    this.confirming.set(false);
  }

  confirmWithdraw(): void {
    const signupId = this.viewer().signupId;
    if (!signupId || this.busy()) {
      return;
    }
    this.busy.set(true);
    this.confirming.set(false);
    this.error.set(null);
    this.events.withdraw(this.item().id, signupId).subscribe({
      next: () => {
        this.override.set({ signupId: null, status: null });
        this.busy.set(false);
      },
      error: () => {
        this.error.set("Couldn't update — try again.");
        this.busy.set(false);
      },
    });
  }

  respond(answer: TrainingRsvp): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    this.trainings.respond(this.item().id, answer).subscribe({
      next: (row) => {
        this.answerOverride.set(row.myAnswer);
        this.busy.set(false);
      },
      error: () => {
        this.error.set("Couldn't update — try again.");
        this.busy.set(false);
      },
    });
  }
}
