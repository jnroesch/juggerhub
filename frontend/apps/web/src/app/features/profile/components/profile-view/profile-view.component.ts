import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { pompfeLabelKey } from '../../../../shared/pompfen.catalog';
import { ProfileView } from '../../../../core/models/profile.models';
import { RecognitionDisplayComponent } from '../recognition-display/recognition-display.component';
import { CardComponent } from '../../../../shared/ui';

/**
 * Shared, read-only presentation of a player profile (feature 026). Used both for the owner's own
 * profile (view mode) and for viewing another player, so the two are structurally identical and
 * can't drift. Owner-only chrome (Edit, visibility toggle) and the viewer-only quick actions live
 * in the hosting components, not here.
 */
@Component({
  selector: 'jh-profile-view',
  imports: [RouterLink, RecognitionDisplayComponent, TranslocoPipe, CardComponent],
  templateUrl: './profile-view.component.html',
  styleUrl: './profile-view.component.css',
})
export class ProfileViewComponent {
  readonly profile = input.required<ProfileView>();
  protected readonly labelKey = pompfeLabelKey;

  /** First letter for the gradient avatar fallback tile, mirroring the team hero. */
  protected initial(name: string | null | undefined): string {
    return (name?.trim()?.charAt(0) || '?').toUpperCase();
  }
}
