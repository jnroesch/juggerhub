import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { HomeService } from '../../../core/services/home.service';
import { UpNextItem } from '../../../core/models/home.models';
import { UpNextCardComponent } from '../modules/up-next-card.component';

/**
 * "See all" for Up next (feature 008): the player's full upcoming-events list, paginated over
 * GET /home/up-next, reusing the interactive up-next card.
 */
@Component({
  selector: 'jh-up-next-list',
  imports: [UpNextCardComponent],
  templateUrl: './up-next-list.component.html',
  styleUrl: './up-next-list.component.css',
})
export class UpNextListComponent implements OnInit {
  private readonly home = inject(HomeService);

  protected readonly items = signal<UpNextItem[]>([]);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  private readonly total = signal(0);
  protected readonly hasMore = computed(() => this.items().length < this.total());

  private skip = 0;
  private readonly take = 20;

  ngOnInit(): void {
    this.loadMore();
  }

  loadMore(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.home.getUpNext(this.skip, this.take).subscribe({
      next: (page) => {
        this.items.update((cur) => [...cur, ...page.items]);
        this.total.set(page.totalCount);
        this.skip += page.items.length;
        this.loading.set(false);
      },
      error: () => {
        this.failed.set(true);
        this.loading.set(false);
      },
    });
  }
}
