import { TranslocoDatePipe } from '@jsverse/transloco-locale';
import { Component, ElementRef, Injector, afterNextRender, inject, input, model, output, signal } from '@angular/core';
import { ButtonDirective } from '../../../../shared/ui';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { Observable } from 'rxjs';
import { EventNews } from '../../../../core/models/event.models';
import { EventService } from '../../../../core/services/event.service';
import { NewsPostComponent } from '../../../../shared/news-post/news-post.component';

/**
 * The event's news feed, with an admin-only inline composer. Any current event admin may edit or
 * delete any post in place (feature 059); the list is the page's, bound two-way, so a change made
 * here is the page's too. The page provides the `NewsPostEditing` the post controls need.
 */
@Component({
  selector: 'jh-event-news-feed',
  imports: [TranslocoDatePipe, FormsModule, ButtonDirective, TranslocoPipe, NewsPostComponent],
  templateUrl: './news-feed.component.html',
  styleUrl: './news-feed.component.css',
})
export class EventNewsFeedComponent {
  private readonly events = inject(EventService);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly news = model.required<EventNews[]>();
  readonly eventId = input.required<string>();
  readonly canCompose = input(false);
  /** Feature 059 — the event's admins may edit and delete every post, whoever wrote it. */
  readonly canManage = input(false);
  readonly posting = input(false);
  readonly post = output<string>();

  protected readonly body = signal('');
  /** What happened to a post an admin tried to act on — a translation key, re-rendered on a language switch. */
  protected readonly notice = signal<string | null>(null);

  protected readonly saveNews = (postId: string, body: string): Observable<EventNews> =>
    this.events.editNews(this.eventId(), postId, body);

  protected readonly deleteNews = (postId: string): Observable<void> => this.events.deleteNews(this.eventId(), postId);

  protected submit(): void {
    const value = this.body().trim();
    if (!value || this.posting()) {
      return;
    }
    this.post.emit(value);
    this.body.set('');
  }

  protected replaceNews(updated: EventNews): void {
    this.notice.set(null);
    this.news.update((list) => list.map((n) => (n.id === updated.id ? updated : n)));
  }

  /** The post is gone — deleted here, or by another admin meanwhile (FR-020), which the notice says. */
  protected dropNews(id: string, gone: boolean): void {
    this.news.update((list) => list.filter((n) => n.id !== id));
    this.notice.set(gone ? 'news.gone' : null);
    // The button that opened the menu went with the post; land on the feed's heading instead.
    afterNextRender(() => this.host.nativeElement.querySelector<HTMLElement>('#event-news-heading')?.focus(), {
      injector: this.injector,
    });
  }
}
