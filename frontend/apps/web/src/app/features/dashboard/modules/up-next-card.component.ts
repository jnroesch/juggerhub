import { Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { EventService } from '../../../core/services/event.service';
import { Signup, SignupStatus } from '../../../core/models/event.models';
import { UpNextItem } from '../../../core/models/home.models';
import { dayOfMonth, shortWeekday, timeHm } from '../../../core/utils/format';

/**
 * One "Up next" / "Open to everyone" item (feature 008). Individuals-mode items carry a one-tap
 * RSVP that toggles to "going" and back (a brief inline confirm guards withdrawal), reusing the
 * existing event sign-up endpoints. Team-mode items are read-only ("your team is going").
 */
@Component({
  selector: 'jh-up-next-card',
  imports: [RouterLink],
  templateUrl: './up-next-card.component.html',
  styleUrl: './up-next-card.component.css',
})
export class UpNextCardComponent {
  private readonly events = inject(EventService);

  readonly item = input.required<UpNextItem>();

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

  protected readonly weekday = computed(() => shortWeekday(this.item().startsAt));
  protected readonly day = computed(() => dayOfMonth(this.item().startsAt));
  protected readonly time = computed(() => timeHm(this.item().startsAt));

  rsvp(): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    this.events.signup(this.item().eventId, null).subscribe({
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
    this.events.withdraw(this.item().eventId, signupId).subscribe({
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
}
