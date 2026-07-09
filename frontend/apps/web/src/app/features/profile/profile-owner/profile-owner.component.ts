import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Pompfe, pompfeLabel } from '../../../shared/pompfen.catalog';
import { OwnerProfile } from '../../../core/models/profile.models';
import { ProfileService } from '../../../core/services/profile.service';
import { problemDetail } from '../../../core/utils/problem';
import { PompfeSelectorComponent } from '../components/pompfe-selector/pompfe-selector.component';
import { RecognitionDisplayComponent } from '../components/recognition-display/recognition-display.component';

/**
 * US3 — the owner's editable profile. Loads /profiles/me, lets the owner edit the
 * display name, hometown, description, avatar, and pompfen selection, then saves.
 * Teams is a stub section. Badges & achievements (feature 012) and recent activity
 * are real. The handle is shown but never editable — it's immutable.
 */
@Component({
  selector: 'jh-profile-owner',
  imports: [ReactiveFormsModule, RouterLink, PompfeSelectorComponent, RecognitionDisplayComponent],
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
  protected readonly appearInSearch = signal(false);
  private readonly avatarVersion = signal(0);

  protected readonly form = this.fb.nonNullable.group({
    displayName: ['', [Validators.required, Validators.maxLength(50)]],
    hometown: ['', [Validators.maxLength(80)]],
    description: ['', [Validators.maxLength(280)]],
  });

  protected readonly avatarUrl = computed(() => {
    const p = this.profile();
    if (!p?.hasAvatar) {
      return null;
    }
    return `${this.profiles.avatarUrl(p.handle)}?v=${this.avatarVersion()}`;
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
        });
        this.selectedPompfen.set(p.pompfen);
        this.appearInSearch.set(p.appearInSearch);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(problemDetail(err));
        this.loading.set(false);
      },
    });
  }

  protected pompfeName(value: Pompfe): string {
    return pompfeLabel(value)?.de ?? value;
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
      });
      this.selectedPompfen.set(p.pompfen);
      this.appearInSearch.set(p.appearInSearch);
    }
    this.error.set(null);
    this.editing.set(false);
  }

  protected toggleAppearInSearch(): void {
    this.appearInSearch.update((v) => !v);
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
    const { displayName, hometown, description } = this.form.getRawValue();
    this.profiles
      .updateMine({
        displayName: displayName.trim(),
        hometown: hometown.trim() || null,
        description: description.trim() || null,
        pompfen: this.selectedPompfen(),
        appearInSearch: this.appearInSearch(),
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
}
