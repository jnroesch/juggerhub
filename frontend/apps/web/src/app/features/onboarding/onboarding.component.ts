import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgTemplateOutlet } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { of, switchMap } from 'rxjs';
import { ProfileService } from '../../core/services/profile.service';
import { AuthService } from '../../core/services/auth.service';
import { safeReturnUrl } from '../../core/utils/return-url';
import { PompfeSelectorComponent } from '../profile/components/pompfe-selector/pompfe-selector.component';
import { Pompfe } from '../../shared/pompfen.catalog';
import { ButtonDirective, AlertComponent } from '../../shared/ui';

type Step = 'welcome' | 'name' | 'city' | 'pompfen' | 'team' | 'photo' | 'done';

/** The five core steps that carry the round-knob progress (welcome/done excluded). */
const CORE_STEPS: readonly Step[] = ['name', 'city', 'pompfen', 'team', 'photo'];
/** Full ordered flow for next/back navigation. */
const FLOW: readonly Step[] = ['welcome', 'name', 'city', 'pompfen', 'team', 'photo', 'done'];

/**
 * First-login onboarding wizard (feature 004). One calm question per screen, held
 * in signals so Back/Skip preserve entered values without round-trips. Persistence
 * reuses the feature-003 owner endpoints (updateMine + uploadAvatar); a final
 * completeOnboarding() marks it done. Any terminal exit — finish OR dismiss — marks
 * complete so the flow is shown exactly once.
 */
@Component({
  selector: 'jh-onboarding',
  imports: [FormsModule, NgTemplateOutlet, PompfeSelectorComponent, ButtonDirective, AlertComponent],
  templateUrl: './onboarding.component.html',
  styleUrl: './onboarding.component.css',
})
export class OnboardingComponent implements OnInit {
  private readonly profiles = inject(ProfileService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly step = signal<Step>('welcome');
  protected readonly coreSteps = CORE_STEPS;

  // Collected values — prefilled from the current profile so Skip/Back never blank
  // an existing value (the finish payload re-sends what's here).
  protected readonly handle = signal('');
  protected readonly displayName = signal('');
  protected readonly hometown = signal('');
  protected readonly description = signal('');
  protected readonly selectedPompfen = signal<Pompfe[]>([]);
  protected readonly avatarFile = signal<File | null>(null);
  protected readonly avatarPreview = signal<string | null>(null);

  // Team step is a visual stub — this selection is never persisted (feature 003).
  protected readonly teamStub = signal<string | null>(null);

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  /** Index among the five core steps; -1 on welcome/done (no progress shown). */
  protected readonly coreIndex = computed(() => CORE_STEPS.indexOf(this.step()));
  /** Display name is the only field that can block progress. */
  protected readonly nameEmpty = computed(() => this.displayName().trim().length === 0);

  ngOnInit(): void {
    // Prefill from the existing profile (display name defaults to the handle at
    // registration). Non-fatal if it fails — the required name field still gates.
    this.profiles.getMine().subscribe({
      next: (p) => {
        this.handle.set(p.handle);
        this.displayName.set(p.displayName);
        this.hometown.set(p.hometown ?? '');
        this.description.set(p.description ?? '');
        this.selectedPompfen.set([...p.pompfen]);
      },
      error: () => {
        /* leave defaults; user can still complete the flow */
      },
    });
  }

  protected next(): void {
    const i = FLOW.indexOf(this.step());
    if (i < FLOW.length - 1) {
      this.step.set(FLOW[i + 1]);
    }
  }

  protected back(): void {
    const i = FLOW.indexOf(this.step());
    if (i > 0) {
      this.step.set(FLOW[i - 1]);
    }
  }

  protected onAvatarPicked(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    const previous = this.avatarPreview();
    if (previous) {
      URL.revokeObjectURL(previous);
    }
    this.avatarFile.set(file);
    this.avatarPreview.set(file ? URL.createObjectURL(file) : null);
  }

  /** Welcome "I'll do this later": mark complete, write nothing, leave. */
  protected dismiss(): void {
    if (this.saving()) {
      return;
    }
    this.saving.set(true);
    this.profiles.completeOnboarding().subscribe({
      next: () => this.enterApp(),
      error: () => this.enterApp(), // best-effort; never trap the user in the flow
    });
  }

  /**
   * Persist the collected values via the reused 003 endpoints, mark onboarding
   * complete, then show the Done screen. Skipped optional fields carry their
   * prefilled values, so nothing is destructively blanked.
   */
  protected finish(): void {
    if (this.nameEmpty() || this.saving()) {
      return;
    }
    this.saving.set(true);
    this.error.set(null);

    this.profiles
      .updateMine({
        displayName: this.displayName().trim(),
        hometown: this.blankToNull(this.hometown()),
        description: this.blankToNull(this.description()),
        pompfen: this.selectedPompfen(),
        // First-login flow (feature 026): profiles start private; visibility is opted into later
        // from the profile page, never during onboarding.
        isPublic: false,
      })
      .pipe(
        switchMap(() => {
          const file = this.avatarFile();
          return file ? this.profiles.uploadAvatar(file) : of(void 0);
        }),
        switchMap(() => this.profiles.completeOnboarding()),
      )
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.step.set('done');
        },
        error: () => {
          this.saving.set(false);
          this.error.set('Something went wrong saving your profile. Please try again.');
        },
      });
  }

  /**
   * Refresh the cached session (so the guard sees onboardingCompleted) and enter the
   * app. A returnUrl carried in from sign-in — an action pending since before the user
   * signed up, e.g. an invite — takes precedence over the dashboard so it can resume.
   */
  protected enterApp(): void {
    const target = safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl')) ?? '/';
    this.auth.loadSession().subscribe({
      next: () => this.router.navigateByUrl(target),
      error: () => this.router.navigateByUrl(target),
    });
  }

  private blankToNull(value: string): string | null {
    const trimmed = value.trim();
    return trimmed.length === 0 ? null : trimmed;
  }
}
