import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { pompfeLabelKey } from '../../../../shared/pompfen.catalog';
import { ProfileView } from '../../../../core/models/profile.models';
import { ShowcaseImage } from '../../../../core/models/showcase.models';
import { ShowcaseService } from '../../../../core/services/showcase.service';
import { RecognitionDisplayComponent } from '../recognition-display/recognition-display.component';
import { ShowcaseGalleryComponent } from '../../../../shared/showcase';
import { CardComponent } from '../../../../shared/ui';

/**
 * Shared, read-only presentation of a player profile (feature 026). Used both for the owner's own
 * profile (view mode) and for viewing another player, so the two are structurally identical and
 * can't drift. Owner-only chrome (Edit, visibility toggle) and the viewer-only quick actions live
 * in the hosting components, not here.
 */
@Component({
  selector: 'jh-profile-view',
  imports: [
    RouterLink,
    RecognitionDisplayComponent,
    ShowcaseGalleryComponent,
    TranslocoPipe,
    CardComponent,
  ],
  templateUrl: './profile-view.component.html',
  styleUrl: './profile-view.component.css',
})
export class ProfileViewComponent {
  private readonly showcaseClient = inject(ShowcaseService);
  private readonly transloco = inject(TranslocoService);

  readonly profile = input.required<ProfileView>();
  protected readonly labelKey = pompfeLabelKey;

  /**
   * The showcase gallery, when the host already has it. The owner's own profile fetches the
   * list to feed its editing controls, so passing it down here keeps the page at ONE listing
   * request (spec SC-008) instead of two views of the same five rows.
   */
  readonly showcase = input<readonly ShowcaseImage[] | null>(null);

  /**
   * The gallery as fetched by this component (feature 046). Read here rather than folded into
   * the profile payload so a profile with no pictures costs one small request and nothing
   * else, and so the list can refresh without re-reading the whole profile.
   */
  private readonly fetched = signal<ShowcaseImage[]>([]);

  protected readonly showcaseImages = computed<readonly ShowcaseImage[]>(
    () => this.showcase() ?? this.fetched(),
  );
  protected readonly showcaseLoading = signal(false);
  protected readonly showcaseError = signal<string | null>(null);

  protected readonly showcaseOwner = computed(
    () => ({ kind: 'profile', handle: this.profile().handle }) as const,
  );

  /**
   * True when the host projects editing controls into the gallery card (the owner's own profile).
   * They live inside the same card as the pictures, so the page never grows a second "Showcase"
   * heading for the same collection.
   */
  readonly showcaseManageable = input(false);

  /**
   * A viewer who cannot add pictures sees no gallery at all when there are none — an empty frame
   * would be a promise of something that is not there (spec FR-026). Someone who CAN add them
   * always sees the card, because that is where the invitation lives.
   */
  protected readonly showGallery = computed(
    () =>
      this.showcaseManageable() ||
      this.showcaseLoading() ||
      this.showcaseError() !== null ||
      this.showcaseImages().length > 0,
  );

  protected readonly reloadShowcase = (): void => {
    if (this.showcase() !== null) {
      // The host owns the list; it refreshes it after every change it makes.
      return;
    }

    const handle = this.profile().handle;
    this.showcaseLoading.set(true);
    this.showcaseError.set(null);
    this.showcaseClient.list({ kind: 'profile', handle }).subscribe({
      next: (images) => {
        this.fetched.set(images);
        this.showcaseLoading.set(false);
      },
      error: () => {
        // An error state, never an empty one: showing "no pictures" for a failed load would
        // quietly lie to the reader (DESIGN.md).
        this.showcaseError.set(this.transloco.translate('showcase.loadFailed'));
        this.showcaseLoading.set(false);
      },
    });
  };

  constructor() {
    // Re-reads when the component is pointed at a different profile.
    effect(() => {
      this.profile();
      this.reloadShowcase();
    });
  }

  /** First letter for the gradient avatar fallback tile, mirroring the team hero. */
  protected initial(name: string | null | undefined): string {
    return (name?.trim()?.charAt(0) || '?').toUpperCase();
  }
}
