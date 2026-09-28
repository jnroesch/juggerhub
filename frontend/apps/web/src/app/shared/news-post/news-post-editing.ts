import { Injectable, computed, signal } from '@angular/core';

/**
 * Which news post on this page is being edited, and the text typed into it so far (feature 059).
 *
 * The state lives here rather than in each `jh-news-post` because a page can tear its posts down
 * and rebuild them while an admin is typing: the team page shows a spinner in place of everything
 * whenever it reloads, which approving a join request does. Feature 057 kept the editor and its
 * draft on the page for exactly that reason, and so does this; a rebuilt post finds its editor
 * still open here.
 *
 * **Provide it on the page component** (`providers: [NewsPostEditing]`), never in root: it must
 * die with the page, or an edit left open on one page would lock the menus on the next. A page
 * the router reuses for another record (the team page, from one team to the next) calls
 * {@link reset} on the switch.
 */
@Injectable()
export class NewsPostEditing {
  /** The post whose editor is open, if any. */
  readonly editingId = signal<string | null>(null);
  /** What has been typed into that editor. */
  readonly draft = signal('');

  private readonly onScreen = signal<ReadonlySet<string>>(new Set());

  /**
   * While a post that is on screen is being edited, no menu opens: choosing another post must never
   * silently throw away the text being typed (057). A post that has since gone from the list locks
   * nothing.
   */
  readonly locked = computed(() => {
    const id = this.editingId();
    return id !== null && this.onScreen().has(id);
  });

  start(postId: string, text: string): void {
    this.draft.set(text);
    this.editingId.set(postId);
  }

  /** Close the editor, if it is this post's. */
  stop(postId: string): void {
    if (this.editingId() === postId) {
      this.editingId.set(null);
      this.draft.set('');
    }
  }

  reset(): void {
    this.editingId.set(null);
    this.draft.set('');
  }

  /** A post's controls appeared on screen. */
  attach(postId: string): void {
    this.onScreen.update((ids) => new Set(ids).add(postId));
  }

  /** A post's controls left the screen. Its editor, if open, stays open for when it comes back. */
  detach(postId: string): void {
    this.onScreen.update((ids) => {
      const next = new Set(ids);
      next.delete(postId);
      return next;
    });
  }
}
