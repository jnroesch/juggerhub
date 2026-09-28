import { Component, ElementRef, Injector, OnInit, afterNextRender, computed, inject, signal } from '@angular/core';
import { AlertComponent, ButtonDirective, CardComponent, EmptyStateComponent, IconComponent, LoadingComponent } from '../../../shared/ui';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoDatePipe } from '@jsverse/transloco-locale';
import { FormsModule } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { Observable } from 'rxjs';
import { Party, PartyNews } from '../../../core/models/party.models';
import { PartyService } from '../../../core/services/party.service';
import { NewsPostComponent } from '../../../shared/news-post/news-post.component';
import { NewsPostEditing } from '../../../shared/news-post/news-post-editing';

/**
 * Party news (feature 016 · wireframe 6e). Private to the crew; only party admins compose. Behaves
 * like team news — a newest-first feed with a composer — scoped to this party and gone on disband.
 * Any current party admin may edit or delete any post in place (feature 059).
 */
@Component({
  selector: 'jh-party-news',
  imports: [CardComponent, RouterLink, TranslocoDatePipe, FormsModule, ButtonDirective, LoadingComponent, AlertComponent, EmptyStateComponent, TranslocoPipe, IconComponent, NewsPostComponent],
  // Feature 059 — an open editor and its typed text belong to the page, not to one post's controls.
  providers: [NewsPostEditing],
  templateUrl: './party-news.component.html',
  styleUrl: './party-news.component.css',
})
export class PartyNewsComponent implements OnInit {
  private readonly parties = inject(PartyService);
  private readonly route = inject(ActivatedRoute);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly party = signal<Party | null>(null);
  protected readonly posts = signal<PartyNews[]>([]);
  protected readonly loading = signal(true);
  protected readonly posting = signal(false);
  /** Translation keys, not text, so a language switch re-renders them. */
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected readonly body = signal('');

  protected readonly isAdmin = computed(() => this.party()?.myRole === 'Admin');
  protected id = '';

  protected readonly saveNews = (postId: string, body: string): Observable<PartyNews> =>
    this.parties.editNews(this.id, postId, body);

  protected readonly deleteNews = (postId: string): Observable<void> => this.parties.deleteNews(this.id, postId);

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    this.parties.getParty(this.id).subscribe({ next: (p) => this.party.set(p), error: () => undefined });
    this.load();
  }

  private load(): void {
    this.parties.listNews(this.id).subscribe({
      next: (page) => {
        this.posts.set(page.items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected post(): void {
    const text = this.body().trim();
    if (this.posting() || text.length === 0) {
      return;
    }
    this.posting.set(true);
    this.error.set(null);
    this.parties.postNews(this.id, { body: text }).subscribe({
      next: () => {
        this.body.set('');
        this.posting.set(false);
        this.load();
      },
      error: () => {
        this.posting.set(false);
        // Our own sentence, never the server's English `detail` (GH #179, feature 059).
        this.error.set('parties.news.error');
      },
    });
  }

  protected replaceNews(updated: PartyNews): void {
    this.notice.set(null);
    this.posts.update((list) => list.map((n) => (n.id === updated.id ? updated : n)));
  }

  /** The post is gone — deleted here, or by another admin meanwhile (FR-020), which the notice says. */
  protected dropNews(id: string, gone: boolean): void {
    this.posts.update((list) => list.filter((n) => n.id !== id));
    this.notice.set(gone ? 'news.gone' : null);
    // The button that opened the menu went with the post; land on the page's heading instead.
    afterNextRender(() => this.host.nativeElement.querySelector<HTMLElement>('#party-news-heading')?.focus(), {
      injector: this.injector,
    });
  }
}
