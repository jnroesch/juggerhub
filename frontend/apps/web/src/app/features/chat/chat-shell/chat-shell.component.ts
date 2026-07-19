import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter, map, startWith } from 'rxjs';
import { ChatInboxComponent } from '../chat-inbox/chat-inbox.component';

/**
 * The chat shell (feature 019, wireframe 9d).
 *
 * At a wide viewport the inbox becomes a persistent left **rail** with the open conversation beside
 * it; below that breakpoint they are separate pushed screens. **Layout only** — there is no behavioural
 * branch here, which is what FR-045 requires and why live delivery, typing and receipts cannot diverge
 * between the two: they are the same component instances either way, and the rail is simply the same
 * inbox that fills the screen on mobile.
 *
 * The open conversation lives in the URL (`/chat/:id`), so it can be linked to and survives a reload
 * (FR-046).
 */
@Component({
  selector: 'jh-chat-shell',
  imports: [RouterOutlet, ChatInboxComponent],
  templateUrl: './chat-shell.component.html',
  styleUrl: './chat-shell.component.css',
})
export class ChatShellComponent {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  /** The current URL, tracked so the layout knows whether a conversation is open. */
  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map((e) => e.urlAfterRedirects),
      startWith(this.router.url),
    ),
    { initialValue: this.router.url },
  );

  /**
   * True when a child route (a conversation, the new-chat sheet, details) is showing.
   *
   * On mobile this replaces the inbox; on desktop it fills the pane beside the rail.
   */
  protected readonly hasChild = computed(() => {
    const path = this.url().split('?')[0].split('#')[0];
    return path !== '/chat' && path.startsWith('/chat');
  });
}
