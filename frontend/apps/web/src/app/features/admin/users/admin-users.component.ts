import { Component, computed, inject, signal } from '@angular/core';
import { LoadingComponent, EmptyStateComponent } from '../../../shared/ui';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AccountStatus, AdminUserListItem } from '../../../core/models/admin.models';
import { AdminService } from '../../../core/services/admin.service';
import { problemDetail } from '../../../core/utils/problem';

type StatusFilter = AccountStatus | null;

const PAGE_SIZE = 20;

/**
 * Find anyone in seconds (feature 013 US3, wireframe 1c): search by name, @handle, or
 * team; filter by account status; page through a calm table (desktop) that folds into
 * tappable cards (mobile). Banned players appear here — and only here — so a mistaken
 * ban can be found and undone. `?q=` arrives from the overview's search box.
 */
@Component({
  selector: 'jh-admin-users',
  imports: [RouterLink, FormsModule, LoadingComponent, EmptyStateComponent],
  templateUrl: './admin-users.component.html',
  styleUrl: './admin-users.component.css',
})
export class AdminUsersComponent {
  private readonly api = inject(AdminService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly q = signal(this.route.snapshot.queryParamMap.get('q') ?? '');
  protected readonly status = signal<StatusFilter>(null);
  protected readonly skip = signal(0);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly items = signal<AdminUserListItem[]>([]);
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
      // Keep the query shareable/back-button friendly.
      this.router.navigate([], { queryParams: { q: value.trim() || null }, queryParamsHandling: 'merge', replaceUrl: true });
      this.load();
    });
    this.load();
  }

  protected onType(value: string): void {
    this.typing.next(value);
  }

  protected setStatus(status: StatusFilter): void {
    this.status.set(status);
    this.skip.set(0);
    this.load();
  }

  protected page(direction: 1 | -1): void {
    this.skip.set(Math.max(0, this.skip() + direction * PAGE_SIZE));
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.searchUsers(this.q(), this.status(), this.skip(), PAGE_SIZE).subscribe({
      next: (page) => {
        this.items.set([...page.items]);
        this.total.set(page.totalCount);
        this.loading.set(false);
      },
      error: (e) => {
        this.error.set(problemDetail(e, 'Could not load players.'));
        this.loading.set(false);
      },
    });
  }
}
