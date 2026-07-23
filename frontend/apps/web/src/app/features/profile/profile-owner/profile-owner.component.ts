import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonDirective, LoadingComponent, AlertComponent } from '../../../shared/ui';
import { Pompfe } from '../../../shared/pompfen.catalog';
import { OwnerProfile, ProfileView } from '../../../core/models/profile.models';
import { ProfileService } from '../../../core/services/profile.service';
import { problemDetail } from '../../../core/utils/problem';
import { PompfeSelectorComponent } from '../components/pompfe-selector/pompfe-selector.component';
import { ProfileViewComponent } from '../components/profile-view/profile-view.component';

/**
 * The owner's own profile (feature 026 — hosted by the profile page at /u/:handle for the owner).
 * In view mode it renders through the shared ProfileViewComponent, so it is structurally identical
 * to another player's profile; it adds owner-only chrome (Edit + an instant visibility toggle) and
 * an inline edit form for display name, hometown, description, avatar, and pompfen. The handle is
 * shown but never editable — it's immutable.
 */
@Component({
  selector: 'jh-profile-owner',
  imports: [ReactiveFormsModule, PompfeSelectorComponent, ProfileViewComponent, ButtonDirective, LoadingComponent, AlertComponent],
  templateUrl: './profile-owner.component.html',
  styleUrl: './profile-owner.component.css',
})
export class ProfileOwnerComponent {
  private readonly profiles = inject(ProfileService);
  private readonly fb = inject(FormBuilder);

  protected readonly profile = signal<OwnerProfile | null>(null);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly editing = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly saved = signal(false);
  protected readonly selectedPompfen = signal<Pompfe[]>([]);
  // Feature 026 — the visibility toggle saves on its own (instant), independently of the edit form.
  protected readonly visibilitySaving = signal(false);
  private readonly avatarVersion = signal(0);

  protected readonly form = this.fb.nonNullable.group({
    displayName: ['', [Validators.required, Validators.maxLength(50)]],
    hometown: ['', [Validators.maxLength(80)]],
    description: ['', [Validators.maxLength(280)]],
    // Feature 026 — anonymous visibility (default private). UX only; the server enforces it.
    isPublic: [false],
  });

  protected readonly avatarUrl = computed(() => {
    const p = this.profile();
    if (!p?.hasAvatar) {
      return null;
    }
    return `${this.profiles.avatarUrl(p.handle)}?v=${this.avatarVersion()}`;
  });

  /** Map the owner DTO to the shared, read-only view model — the same one other players render. */
  protected readonly view = computed<ProfileView | null>(() => {
    const p = this.profile();
    if (!p) {
      return null;
    }
    return {
      handle: p.handle,
      displayName: p.displayName,
      hometown: p.hometown,
      description: p.description,
      avatarUrl: this.avatarUrl(),
      pompfen: p.pompfen,
      teams: p.teams,
      recentActivity: p.recentActivity,
      badges: p.badges,
      achievements: p.achievements,
    };
  });

  constructor() {
    this.reload();
  }

  private reload(): void {
    this.loading.set(true);
    this.profiles.getMine().subscribe({
      next: (p) => {
        this.profile.set(p);
        this.form.setValue({
          displayName: p.displayName,
          hometown: p.hometown ?? '',
          description: p.description ?? '',
          isPublic: p.isPublic,
        });
        this.selectedPompfen.set(p.pompfen);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(problemDetail(err));
        this.loading.set(false);
      },
    });
  }

  protected startEdit(): void {
    this.saved.set(false);
    this.editing.set(true);
  }

  protected cancelEdit(): void {
    const p = this.profile();
    if (p) {
      this.form.setValue({
        displayName: p.displayName,
        hometown: p.hometown ?? '',
        description: p.description ?? '',
        isPublic: p.isPublic,
      });
      this.selectedPompfen.set(p.pompfen);
    }
    this.error.set(null);
    this.editing.set(false);
  }

  protected onPompfenChange(next: Pompfe[]): void {
    this.selectedPompfen.set(next);
  }

  protected onAvatarSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }
    this.error.set(null);
    this.profiles.uploadAvatar(file).subscribe({
      next: () => {
        // Force the <img> to re-fetch the new bytes.
        this.profile.update((p) => (p ? { ...p, hasAvatar: true } : p));
        this.avatarVersion.update((v) => v + 1);
        input.value = '';
      },
      error: (err) => this.error.set(problemDetail(err)),
    });
  }

  protected save(): void {
    if (this.form.invalid || this.saving()) {
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    this.saved.set(false);
    const { displayName, hometown, description, isPublic } = this.form.getRawValue();
    this.profiles
      .updateMine({
        displayName: displayName.trim(),
        hometown: hometown.trim() || null,
        description: description.trim() || null,
        pompfen: this.selectedPompfen(),
        isPublic,
      })
      .subscribe({
        next: (p) => {
          this.profile.set(p);
          this.selectedPompfen.set(p.pompfen);
          this.saving.set(false);
          this.saved.set(true);
          this.editing.set(false);
        },
        error: (err) => {
          this.saving.set(false);
          this.error.set(problemDetail(err));
        },
      });
  }

  /**
   * Flip anonymous visibility straight from the profile — no edit-and-save round trip needed
   * (feature 026, SC-005). Reuses the owner update with the current values + the new flag, so the
   * server stays the single authority; the edit form's control is kept in sync for consistency.
   */
  protected toggleVisibility(next: boolean): void {
    const p = this.profile();
    if (!p || this.visibilitySaving()) {
      return;
    }
    this.visibilitySaving.set(true);
    this.error.set(null);
    this.saved.set(false);
    this.profiles
      .updateMine({
        displayName: p.displayName,
        hometown: p.hometown,
        description: p.description,
        pompfen: p.pompfen,
        isPublic: next,
      })
      .subscribe({
        next: (updated) => {
          this.profile.set(updated);
          this.form.controls.isPublic.setValue(updated.isPublic);
          this.visibilitySaving.set(false);
        },
        error: (err) => {
          this.visibilitySaving.set(false);
          this.error.set(problemDetail(err));
        },
      });
  }
}
