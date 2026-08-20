import { Component, computed, inject, input, output, signal } from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ShowcaseImage, ShowcaseOwner } from '../../core/models/showcase.models';
import { ShowcaseService } from '../../core/services/showcase.service';
import { AlertComponent, ButtonDirective, IconComponent } from '../ui';

/** The most pictures a gallery holds, mirroring the server's cap (feature 046). */
export const SHOWCASE_MAX_IMAGES = 5;

/**
 * The management side of a showcase gallery (feature 046 / #99): add, caption, move, remove.
 *
 * Rendered only for someone who may actually change the gallery — the profile's owner, or an
 * admin of the team. A viewer's page never instantiates this component at all, so "an ordinary
 * member is offered nothing" holds structurally rather than through CSS. The server refuses
 * them regardless; this is UX, not the boundary.
 *
 * **Reordering is move-up / move-down, not drag-and-drop.** The repo has no drag-and-drop
 * dependency and this feature adds none; buttons are also keyboard- and touch-operable without
 * a second parallel affordance, which is what the accessibility requirement needs anyway.
 *
 * **Nothing is retried automatically.** Every action here is a mutation on the browser hop; a
 * silent retry of an upload that actually succeeded would spend a second slot in a
 * five-picture gallery (constitution Principle VII).
 */
@Component({
  selector: 'jh-showcase-manager',
  imports: [TranslocoPipe, AlertComponent, ButtonDirective, IconComponent],
  templateUrl: './showcase-manager.component.html',
  styleUrl: './showcase-manager.component.css',
})
export class ShowcaseManagerComponent {
  private readonly showcase = inject(ShowcaseService);
  private readonly transloco = inject(TranslocoService);

  readonly owner = input.required<ShowcaseOwner>();
  readonly images = input.required<readonly ShowcaseImage[]>();

  /** Raised whenever the gallery changed, so the page can refresh what it renders. */
  readonly changed = output<ShowcaseImage[]>();

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly editingId = signal<string | null>(null);
  protected readonly captionDraft = signal('');

  protected readonly maxImages = SHOWCASE_MAX_IMAGES;

  /**
   * The empty-gallery invitation is addressed to the owner, and a team is not a person: "show what
   * playing looks like for you" is wrong copy on a team's gallery.
   */
  protected readonly emptyKey = computed(() =>
    this.owner().kind === 'team' ? 'showcase.emptyTeamOwner' : 'showcase.emptyOwner',
  );
  protected readonly full = computed(() => this.images().length >= SHOWCASE_MAX_IMAGES);
  protected readonly remaining = computed(() => SHOWCASE_MAX_IMAGES - this.images().length);

  protected imageUrl(image: ShowcaseImage): string {
    return this.showcase.imageUrl(this.owner(), image.id);
  }

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    // Clear the input either way, so picking the same file twice still fires a change event.
    input.value = '';

    if (!file || this.busy()) {
      return;
    }

    this.error.set(null);
    this.busy.set(true);
    this.showcase.upload(this.owner(), file).subscribe({
      next: () => this.reload(),
      error: (failure: unknown) => {
        // Each refusal category gets its own sentence: "we couldn't read that picture" and "your
        // gallery is full" are different problems with different fixes (spec FR-016).
        this.error.set(
          this.transloco.translate(
            `showcase.upload.${this.showcase.classifyUploadFailure(failure)}`,
            { max: SHOWCASE_MAX_IMAGES },
          ),
        );
        this.busy.set(false);
      },
    });
  }

  protected startEditing(image: ShowcaseImage): void {
    this.editingId.set(image.id);
    this.captionDraft.set(image.caption ?? '');
  }

  protected cancelEditing(): void {
    this.editingId.set(null);
    this.captionDraft.set('');
  }

  protected saveCaption(image: ShowcaseImage): void {
    const caption = this.captionDraft().trim();
    this.mutate(
      this.showcase.setCaption(this.owner(), image.id, caption.length > 0 ? caption : null),
      () => this.cancelEditing(),
    );
  }

  protected remove(image: ShowcaseImage): void {
    this.mutate(this.showcase.remove(this.owner(), image.id));
  }

  protected move(index: number, delta: number): void {
    const target = index + delta;
    const ids = this.images().map((image) => image.id);
    if (target < 0 || target >= ids.length) {
      return;
    }

    [ids[index], ids[target]] = [ids[target], ids[index]];
    // The whole order goes to the server, which refuses anything that is not a permutation of
    // what it currently holds — that is how a stale page is caught rather than half-applied.
    this.mutate(this.showcase.reorder(this.owner(), ids));
  }

  private mutate(request: ReturnType<ShowcaseService['remove']>, onSuccess?: () => void): void {
    if (this.busy()) {
      return;
    }

    this.error.set(null);
    this.busy.set(true);
    request.subscribe({
      next: () => {
        onSuccess?.();
        this.reload();
      },
      error: (failure: unknown) => {
        const stale = this.showcase.classifyUploadFailure(failure) === 'full';
        this.error.set(
          this.transloco.translate(stale ? 'showcase.staleOrder' : 'showcase.actionFailed'),
        );
        this.busy.set(false);
        // Re-read so the screen shows what the server actually holds rather than the optimistic
        // order the failed action implied.
        this.reload();
      },
    });
  }

  /** Re-read the gallery and hand it to the page. The listing is at most five rows. */
  private reload(): void {
    this.showcase.list(this.owner()).subscribe({
      next: (images) => {
        this.changed.emit(images);
        this.busy.set(false);
      },
      error: () => {
        this.error.set(this.transloco.translate('showcase.actionFailed'));
        this.busy.set(false);
      },
    });
  }
}
