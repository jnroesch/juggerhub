import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { LoadingComponent, EmptyStateComponent } from '../../../shared/ui';
import { HomeService } from '../../../core/services/home.service';
import { HomeNews } from '../../../core/models/home.models';
import { NewsListComponent } from '../modules/news-list.component';

/**
 * "See all" for News (feature 008): the player's full aggregated feed, paginated over
 * GET /home/news, reusing the news-list presentation.
 */
@Component({
  selector: 'jh-news-page',
  imports: [NewsListComponent, LoadingComponent, EmptyStateComponent],
  templateUrl: './news-page.component.html',
  styleUrl: './news-page.component.css',
})
export class NewsPageComponent implements OnInit {
  private readonly home = inject(HomeService);

  protected readonly items = signal<HomeNews[]>([]);
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
    this.home.getNews(this.skip, this.take).subscribe({
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
