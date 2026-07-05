import { DatePipe } from '@angular/common';
import { Component, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { EventNews } from '../../../../core/models/event.models';

/** The public news feed, with an admin-only inline composer. */
@Component({
  selector: 'jh-event-news-feed',
  imports: [DatePipe, FormsModule],
  templateUrl: './news-feed.component.html',
  styleUrl: './news-feed.component.css',
})
export class EventNewsFeedComponent {
  readonly news = input.required<EventNews[]>();
  readonly canCompose = input(false);
  readonly posting = input(false);
  readonly post = output<string>();

  protected readonly body = signal('');

  protected submit(): void {
    const value = this.body().trim();
    if (!value || this.posting()) {
      return;
    }
    this.post.emit(value);
    this.body.set('');
  }
}
