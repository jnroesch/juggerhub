import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  DestroyRef,
  ElementRef,
  HostListener,
  Injector,
  OnInit,
  afterNextRender,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { Observable } from 'rxjs';
import { ButtonDirective, IconComponent } from '../ui';
import { NewsPostEditing } from './news-post-editing';

/** What became of a post the controls removed from view. */
export interface NewsPostRemoval {
  /** True when it was already gone (another admin deleted it meanwhile): the host says so. */
  gone: boolean;
}

/**
 * One news post's edit and delete controls (feature 059): a menu for the post's admins, an editor
 * in place of the post, and a confirmation before a delete. Team news (feature 057, where these
 * were built), event news and party news all use it, so the four places a post can be managed
 * cannot drift apart.
 *
 * The host keeps what differs: its list, each item's frame, and the post's own body and meta line,
 * which it projects in. The component calls no service; the host hands it `save` and `remove` and
 * gets the outcome back through `saved` and `removed`. Every admin sees the controls on every post
 * (the owner's rule for all three kinds of news); the server decides, and `canManage` is only the
 * convenience.
 *
 * Needs a {@link NewsPostEditing} provided by the page.
 */
@Component({
  selector: 'jh-news-post',
  imports: [ButtonDirective, IconComponent, TranslocoPipe],
  templateUrl: './news-post.component.html',
  styleUrl: './news-post.component.css',
  host: { class: 'flex items-start gap-xs' },
})
export class NewsPostComponent<T> implements OnInit {
  private readonly session = inject(NewsPostEditing);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  readonly postId = input.required<string>();
  /** The post's current text, which the editor starts from. */
  readonly body = input.required<string>();
  readonly canManage = input(false);
  /** The posting limit of this kind of news: 1,000 characters for team and party news, 2,000 for events. */
  readonly maxLength = input.required<number>();
  /** Translation key: what saving does, for this kind of news (it never notifies anyone). */
  readonly editHint = input.required<string>();
  /** Translation key: who the post disappears for. */
  readonly deleteBody = input.required<string>();
  /** Saves the trimmed text. Called only when it changed. */
  readonly save = input.required<(postId: string, body: string) => Observable<T>>();
  readonly remove = input.required<(postId: string) => Observable<unknown>>();

  /** The post as the server now has it. */
  readonly saved = output<T>();
  /** The post is gone; the host takes it off its list and moves focus, since this component goes with it. */
  readonly removed = output<NewsPostRemoval>();

  protected readonly menuOpen = signal(false);
  protected readonly editing = computed(() => this.session.editingId() === this.postId());
  protected readonly draft = this.session.draft;
  protected readonly locked = this.session.locked;
  protected readonly saving = signal(false);
  /** Translation keys, not text, so a language switch re-renders them. */
  protected readonly editError = signal<string | null>(null);

  protected readonly confirming = signal(false);
  protected readonly deleting = signal(false);
  protected readonly deleteError = signal<string | null>(null);

  /** Set once the post has left the screen; an answer that arrives later must not reach for its DOM. */
  private destroyed = false;

  constructor() {
    // The editor, if open, is left open here: a page that rebuilds its list finds it again.
    inject(DestroyRef).onDestroy(() => {
      this.destroyed = true;
      this.session.detach(this.postId());
    });
  }

  ngOnInit(): void {
    this.session.attach(this.postId());
  }

  protected toggleMenu(): void {
    this.menuOpen.update((open) => !open);
  }

  protected startEdit(): void {
    this.menuOpen.set(false);
    this.editError.set(null);
    this.session.start(this.postId(), this.body());
    this.focusAfterRender('[data-testid="news-edit-input"]');
  }

  protected cancelEdit(): void {
    this.session.stop(this.postId());
    this.editError.set(null);
    this.focusAfterRender('[data-news-menu-trigger]');
  }

  protected saveEdit(): void {
    const body = this.draft().trim();
    if (body.length === 0 || this.saving()) {
      return;
    }
    if (body === this.body()) {
      // Nothing changed, so there is nothing to send (FR-003).
      this.cancelEdit();
      return;
    }
    this.saving.set(true);
    this.editError.set(null);
    this.save()(this.postId(), body).subscribe({
      next: (updated) => {
        this.saving.set(false);
        this.saved.emit(updated);
        this.cancelEdit();
      },
      error: (err) => {
        this.saving.set(false);
        if (isGone(err)) {
          this.session.stop(this.postId());
          this.removed.emit({ gone: true });
          return;
        }
        // The editor stays open with the typed text (FR-021). Our own sentence, never the server's
        // English `detail` (GH #179).
        this.editError.set('news.saveFailed');
      },
    });
  }

  protected askDelete(): void {
    this.menuOpen.set(false);
    this.deleteError.set(null);
    this.confirming.set(true);
    // The safe answer takes focus, so Enter on arrival keeps the post.
    this.focusAfterRender('[data-testid="news-delete-keep"]');
  }

  protected dismissDelete(): void {
    if (!this.confirming() || this.deleting()) {
      return;
    }
    this.confirming.set(false);
    this.deleteError.set(null);
    this.focusAfterRender('[data-news-menu-trigger]');
  }

  protected confirmDelete(): void {
    if (!this.confirming() || this.deleting()) {
      return;
    }
    this.deleting.set(true);
    this.deleteError.set(null);
    this.remove()(this.postId()).subscribe({
      next: () => {
        this.deleting.set(false);
        this.confirming.set(false);
        // Last: the host drops the post, and this component with it.
        this.removed.emit({ gone: false });
      },
      error: (err) => {
        this.deleting.set(false);
        if (isGone(err)) {
          this.confirming.set(false);
          this.removed.emit({ gone: true });
          return;
        }
        // The dialog stays open; confirming again is the retry (never automatic).
        this.deleteError.set('news.deleteFailed');
      },
    });
  }

  /** Keep Tab inside the open dialog: `aria-modal` promises that the page behind it is inert. */
  protected trapTab(event: Event): void {
    const key = event as KeyboardEvent;
    const buttons = Array.from(
      (key.currentTarget as HTMLElement).querySelectorAll<HTMLElement>('button:not([disabled])'),
    );
    if (buttons.length === 0) {
      return;
    }
    const first = buttons[0];
    const last = buttons[buttons.length - 1];
    if (key.shiftKey && document.activeElement === first) {
      last.focus();
      key.preventDefault();
    } else if (!key.shiftKey && document.activeElement === last) {
      first.focus();
      key.preventDefault();
    }
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    if (this.confirming()) {
      this.dismissDelete();
    }
    if (this.menuOpen()) {
      // Back to the button that opened it, or a keyboard user is left on the page body.
      this.menuOpen.set(false);
      this.focus('[data-news-menu-trigger]');
    }
  }

  /**
   * A click anywhere outside this post's menu closes it — including a click on another post's menu
   * button, which is inside *a* menu but not this one. That is what keeps one menu open at a time.
   */
  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: MouseEvent): void {
    if (!this.menuOpen()) {
      return;
    }
    const menu = this.host.nativeElement.querySelector('[data-news-menu]');
    if (!menu?.contains(event.target as Node | null)) {
      this.menuOpen.set(false);
    }
  }

  /** Zoneless: what to focus exists only after the next render (GH #344's lesson — not an effect). */
  private focusAfterRender(selector: string): void {
    if (!this.destroyed) {
      afterNextRender(() => this.focus(selector), { injector: this.injector });
    }
  }

  private focus(selector: string): void {
    this.host.nativeElement.querySelector<HTMLElement>(selector)?.focus();
  }
}

/**
 * A 404 from an edit or a delete: the post is gone (or the viewer no longer has access). Branch on
 * the status, never the message — the server's text is English in every language.
 */
function isGone(err: unknown): boolean {
  return err instanceof HttpErrorResponse && err.status === 404;
}
