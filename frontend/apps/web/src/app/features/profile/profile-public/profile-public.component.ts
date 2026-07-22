import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ButtonDirective, LoadingComponent } from '../../../shared/ui';
import { pompfeLabel } from '../../../shared/pompfen.catalog';
import { PublicProfile } from '../../../core/models/profile.models';
import { ProfileService } from '../../../core/services/profile.service';
import { RecognitionDisplayComponent } from '../components/recognition-display/recognition-display.component';
import { ProfileQuickActionsComponent } from '../components/quick-actions/profile-quick-actions.component';

/**
 * US2 — the public, unauthenticated profile at /u/:handle. Renders only the
 * public field set (never email/account data — the server strips it). Shows a
 * friendly not-found for unknown handles and a copy-link affordance.
 */
@Component({
  selector: 'jh-profile-public',
  imports: [RouterLink, RecognitionDisplayComponent, ProfileQuickActionsComponent, ButtonDirective, LoadingComponent],
  templateUrl: './profile-public.component.html',
  styleUrl: './profile-public.component.css',
})
export class ProfilePublicComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly profiles = inject(ProfileService);

  protected readonly profile = signal<PublicProfile | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly copied = signal(false);

  protected readonly labelOf = pompfeLabel;

  constructor() {
    const handle = this.route.snapshot.paramMap.get('handle') ?? '';
    this.profiles.getPublic(handle).subscribe({
      next: (p) => {
        this.profile.set(p);
        this.loading.set(false);
      },
      error: () => {
        this.notFound.set(true);
        this.loading.set(false);
      },
    });
  }

  protected shareUrl(handle: string): string {
    return `${location.origin}/u/${handle}`;
  }

  protected avatarUrl(handle: string): string {
    return this.profiles.avatarUrl(handle);
  }

  protected copyLink(handle: string): void {
    void navigator.clipboard?.writeText(this.shareUrl(handle)).then(() => {
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    });
  }
}
