import { Component, inject, signal } from '@angular/core';
import { LoadingComponent } from '../../../shared/ui';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TrainingsService } from '../../../core/services/trainings.service';
import { AttendanceEntry } from '../../../core/models/trainings.models';
import { problemDetail } from '../../../core/utils/problem';

/**
 * Admin attendance for one session (feature 018): the full list incl. guests, who count in the headcount
 * and carry a guest tag. The admin can remove a guest — dropping their response without touching team
 * membership. Members and guests share one list; only the tag and the remove control differ.
 */
@Component({
  selector: 'jh-training-attendance',
  imports: [RouterLink, LoadingComponent],
  templateUrl: './attendance.component.html',
  styleUrl: './attendance.component.css',
})
export class AttendanceComponent {
  private readonly trainings = inject(TrainingsService);
  private readonly route = inject(ActivatedRoute);

  protected readonly sessionId = this.route.snapshot.paramMap.get('id') ?? '';
  protected readonly entries = signal<AttendanceEntry[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);

  constructor() {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.trainings.getAttendance(this.sessionId, undefined, 0, 100).subscribe({
      next: (p) => {
        this.entries.set(p.items);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }

  protected removeGuest(entry: AttendanceEntry): void {
    if (this.busy() || !entry.isGuest) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    this.trainings.removeGuest(this.sessionId, entry.userId).subscribe({
      next: () => {
        this.entries.update((list) => list.filter((e) => e.userId !== entry.userId));
        this.busy.set(false);
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }

  protected avatarUrl(handle: string): string {
    return `/api/v1/profiles/${encodeURIComponent(handle)}/avatar`;
  }

  protected answerLabel(a: AttendanceEntry['answer']): string {
    return a === 'Going' ? 'Going' : a === 'Maybe' ? 'Maybe' : "Can't";
  }
}
