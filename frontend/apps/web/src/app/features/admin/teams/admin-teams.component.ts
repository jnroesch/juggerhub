import { Component, computed, inject, signal } from '@angular/core';
import { LoadingComponent } from '../../../shared/ui';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AdminTeamListItem } from '../../../core/models/admin.models';
import { AdminService } from '../../../core/services/admin.service';
import { problemDetail } from '../../../core/utils/problem';

const PAGE_SIZE = 20;

/**
 * Admin teams browse (feature 014): find a team by name or city, page through a calm table
 * (desktop) that folds into cards (mobile), and open a team to assign awards to it. Parallel to
 * the admin users list; the server `PlatformAdmin` policy is the boundary.
 */
@Component({
  selector: 'jh-admin-teams',
  imports: [RouterLink, FormsModule, LoadingComponent],
  templateUrl: './admin-teams.component.html',
  styleUrl: './admin-teams.component.css',
})
export class AdminTeamsComponent {
  private readonly api = inject(AdminService);

  protected readonly q = signal('');
  protected readonly skip = signal(0);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly items = signal<AdminTeamListItem[]>([]);
  protected readonly total = signal(0);

  protected readonly rangeStart = computed(() => (this.total() === 0 ? 0 : this.skip() + 1));
  protected readonly rangeEnd = computed(() => Math.min(this.skip() + this.items().length, this.total()));
  protected readonly hasPrev = computed(() => this.skip() > 0);
  protected readonly hasNext = computed(() => this.skip() + PAGE_SIZE < this.total());

  private readonly typing = new Subject<string>();

  constructor() {
    this.typing.pipe(debounceTime(250), takeUntilDestroyed()).subscribe((value) => {
      this.q.set(value);
      this.skip.set(0);
      this.load();
    });
    this.load();
  }

  protected onType(value: string): void {
    this.typing.next(value);
  }

  protected page(direction: 1 | -1): void {
    this.skip.set(Math.max(0, this.skip() + direction * PAGE_SIZE));
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.searchTeams(this.q(), this.skip(), PAGE_SIZE).subscribe({
      next: (page) => {
        this.items.set([...page.items]);
        this.total.set(page.totalCount);
        this.loading.set(false);
      },
      error: (e) => {
        this.error.set(problemDetail(e, 'Could not load teams.'));
        this.loading.set(false);
      },
    });
  }
}
