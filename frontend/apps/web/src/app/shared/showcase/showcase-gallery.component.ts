import { Component, ElementRef, computed, effect, inject, input, signal, viewChild } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { ShowcaseImage, ShowcaseOwner } from '../../core/models/showcase.models';
import { ShowcaseService } from '../../core/services/showcase.service';
import { AlertComponent, ButtonDirective, IconComponent, LoadingComponent } from '../ui';

/**
 * The read side of a showcase gallery (feature 046 / #99): a thumbnail row, and an enlarged
 * view for looking at one picture properly.
 *
 * Used on three surfaces — the public profile, the owner's own profile, and the team page —
 * which is what earns it a place in `shared/`. It never uploads, reorders, or deletes: those
 * live in `jh-showcase-manager`, so a viewer's bundle carries no editing code and "a
 * non-admin is offered nothing" is structural rather than a set of `@if`s.
 *
 * The images are fed in by the parent rather than fetched here, so a page that already knows
 * its gallery (the owner's profile, which is editing it) does not issue a second request.
 */
@Component({
  selector: 'jh-showcase-gallery',
  imports: [TranslocoPipe, AlertComponent, ButtonDirective, IconComponent, LoadingComponent],
  templateUrl: './showcase-gallery.component.html',
  styleUrl: './showcase-gallery.component.css',
})
export class ShowcaseGalleryComponent {
  private readonly showcase = inject(ShowcaseService);

  /** Whose gallery this is — selects the image addresses. */
  readonly owner = input.required<ShowcaseOwner>();

  /** The pictures, in the owner's order. */
  readonly images = input.required<readonly ShowcaseImage[]>();

  /** Name used in the fallback text alternative of a picture with no caption. */
  readonly ownerName = input<string>('');

  readonly loading = input(false);

  /** Non-null when the listing could not be loaded — an error state, never an empty one. */
  readonly error = input<string | null>(null);

  /** Emitted by the "Try again" control in the error state. */
  readonly retry = input<(() => void) | null>(null);

  /** Index of the enlarged picture, or null when the overlay is closed. */
  private readonly openIndex = signal<number | null>(null);

  protected readonly opened = computed(() => {
    const index = this.openIndex();
    return index === null ? null : (this.images()[index] ?? null);
  });

  protected readonly hasPrevious = computed(() => (this.openIndex() ?? 0) > 0);

  protected readonly hasNext = computed(() => {
    const index = this.openIndex();
    return index !== null && index < this.images().length - 1;
  });

  /** 1-based position of the enlarged picture, for its text alternative. */
  protected readonly openedNumber = computed(() => (this.openIndex() ?? 0) + 1);

  /** The element that opened the overlay, so focus can go back where it came from. */
  private opener: HTMLElement | null = null;

  private readonly viewer = viewChild<ElementRef<HTMLElement>>('viewer');

  constructor() {
    // Move focus into the overlay when it opens, so the arrow and Escape keys reach it without
    // the viewer having to tab into it first (spec FR-027). Focus goes back to the thumbnail in
    // close(), not here — an effect cannot tell "closed" from "never opened".
    effect(() => {
      if (this.openIndex() !== null) {
        setTimeout(() => this.viewer()?.nativeElement.focus(), 0);
      }
    });
  }

  protected imageUrl(image: ShowcaseImage): string {
    return this.showcase.imageUrl(this.owner(), image.id);
  }

  /**
   * The accessible text alternative. A caption is the best description there is; without one,
   * a generic alternative naming the owner still tells a screen-reader user what they are
   * looking at (spec FR-028).
   */
  protected altKeyFor(image: ShowcaseImage): string | null {
    return image.caption?.trim() ? image.caption : null;
  }

  protected open(index: number, event: Event): void {
    this.opener = event.currentTarget as HTMLElement | null;
    this.openIndex.set(index);
  }

  protected close(): void {
    this.openIndex.set(null);
    // Focus returns to the thumbnail the viewer opened, not to the top of the page.
    this.opener?.focus();
    this.opener = null;
  }

  protected previous(): void {
    if (this.hasPrevious()) {
      this.openIndex.update((index) => (index ?? 0) - 1);
    }
  }

  protected next(): void {
    if (this.hasNext()) {
      this.openIndex.update((index) => (index ?? 0) + 1);
    }
  }

  /** Keyboard handling for the overlay: arrows page, Escape closes (spec FR-027, SC-007). */
  protected onOverlayKeydown(event: KeyboardEvent): void {
    switch (event.key) {
      case 'Escape':
        event.preventDefault();
        this.close();
        break;
      case 'ArrowLeft':
        event.preventDefault();
        this.previous();
        break;
      case 'ArrowRight':
        event.preventDefault();
        this.next();
        break;
      default:
        break;
    }
  }

  protected onRetry(): void {
    this.retry()?.();
  }
}
