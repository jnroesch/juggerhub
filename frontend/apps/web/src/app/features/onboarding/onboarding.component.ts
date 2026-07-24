import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NgTemplateOutlet } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { ProfileService } from '../../core/services/profile.service';
import { AuthService } from '../../core/services/auth.service';
import { SearchService } from '../../core/services/search.service';
import { TeamService } from '../../core/services/team.service';
import { TeamBrowseParams, TeamCard } from '../../core/models/search.models';
import { BrowseList } from '../browse/browse-list';
import { safeReturnUrl } from '../../core/utils/return-url';
import { PompfeSelectorComponent } from '../profile/components/pompfe-selector/pompfe-selector.component';
import { Pompfe } from '../../shared/pompfen.catalog';
import { ButtonDirective, AlertComponent, IconComponent, LoadingComponent } from '../../shared/ui';

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
 *
 * The team step searches real teams and can send a join request (feature 029, replacing
 * 004's placeholder). Note what it deliberately does *not* do: `next()` and `back()` carry
 * no team logic and issue no request, so no state of that step — slow search, failed
 * search, failed join — can hold a player who registered thirty seconds ago inside the
 * wizard. Asking to join is its own press. Keep it that way.
 */
@Component({
  selector: 'jh-onboarding',
  imports: [
    FormsModule,
    NgTemplateOutlet,
    PompfeSelectorComponent,
    ButtonDirective,
    AlertComponent,
    IconComponent,
    LoadingComponent,
  ],
  templateUrl: './onboarding.component.html',
  styleUrl: './onboarding.component.css',
})
export class OnboardingComponent implements OnInit, OnDestroy {
  private readonly profiles = inject(ProfileService);
  private readonly auth = inject(AuthService);
  private readonly search = inject(SearchService);
  private readonly teamApi = inject(TeamService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

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

  // --- Team step (feature 029) ---------------------------------------------
  // None of this reaches the finish payload. The join request is the only thing
  // this step persists, and it is sent from askToJoin() alone — never from next().

  /** Debounced input for the team search; the applied value lands in teamQuery. */
  private readonly teamQueryInput = new Subject<string>();
  /** The *applied* search text (empty = the beginner-friendly opening list). */
  protected readonly teamQuery = signal('');
  /** Single-select: picking another team replaces this one. */
  protected readonly selectedTeam = signal<TeamCard | null>(null);
  /** Slugs already asked in this flow, so the same team can't be asked twice. */
  protected readonly requestedSlugs = signal<ReadonlySet<string>>(new Set<string>());
  /** In-flight guard for the ask action. Never gates Continue. */
  protected readonly askingSlug = signal<string | null>(null);
  protected readonly teamRequestError = signal<string | null>(null);

  protected readonly teams = new BrowseList<TeamCard>((skip, take) =>
    this.search.browseTeams({ ...this.teamParams(), skip, take }),
  );

  /** True once the selected team has been asked — swaps the action for the confirmation. */
  protected readonly selectedRequested = computed(() => {
    const team = this.selectedTeam();
    return team !== null && this.requestedSlugs().has(team.slug);
  });

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  /** Index among the five core steps; -1 on welcome/done (no progress shown). */
  protected readonly coreIndex = computed(() => CORE_STEPS.indexOf(this.step()));
  /** Display name is the only field that can block progress. */
  protected readonly nameEmpty = computed(() => this.displayName().trim().length === 0);

  constructor() {
    // Same 250ms + distinctUntilChanged as BrowseShellComponent, so searching here
    // feels identical to searching on the browse screens.
    this.teamQueryInput
      .pipe(debounceTime(250), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe((value) => {
        this.teamQuery.set(value.trim());
        this.reloadTeams();
      });
  }

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

    // Prefetch the team step's opening list, independent of the prefill above — neither
    // may block the other. Fetching here rather than on arrival costs one list request
    // for players who dismiss at Welcome, and buys everyone else a step that is already
    // populated when they walk to it instead of one that flashes a loading line.
    this.reloadTeams();
  }

  ngOnDestroy(): void {
    this.teams.destroy();
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

  // --- Team step (feature 029) ---------------------------------------------

  /** Template entry point for the search field; the constructor's pipeline debounces it. */
  protected onTeamQuery(value: string): void {
    this.teamQueryInput.next(value);
  }

  protected selectTeam(team: TeamCard): void {
    this.selectedTeam.set(team);
    this.teamRequestError.set(null);
  }

  /**
   * Ask a team to let the player in. This is the only place the team step writes
   * anything, and it is deliberately *not* wired to Continue: keeping the wizard's
   * primary action free of any network call is what makes "this step can never trap a
   * brand-new player" structural rather than something to remember (FR-012, FR-018).
   * It also guarantees the pending-request confirmation is actually seen, since it
   * answers a press the player chose to make.
   *
   * No retry, timeout, or backoff here. `retryInterceptor` time-limits this POST and
   * correctly never repeats it (constitution VII) — a mutation the browser cannot prove
   * was skipped is never retried, even against an endpoint that happens to be idempotent
   * while a request is pending.
   */
  protected askToJoin(): void {
    const team = this.selectedTeam();
    if (!team || this.askingSlug() !== null || this.requestedSlugs().has(team.slug)) {
      return;
    }
    this.askingSlug.set(team.slug);
    this.teamRequestError.set(null);

    this.teamApi.requestToJoin(team.slug).subscribe({
      next: () => {
        this.requestedSlugs.update((slugs) => new Set(slugs).add(team.slug));
        this.askingSlug.set(null);
      },
      error: (response: HttpErrorResponse) => {
        this.askingSlug.set(null);
        // 409 is the server reporting a different *fact* — already a member — not a
        // different failure, so it earns its own sentence. Everything else stays
        // generic; no status code or internal detail reaches the reader (Principle I).
        this.teamRequestError.set(
          response.status === 409
            ? "You're already on that team."
            : "We couldn't send that request just now.",
        );
      },
    });
  }

  /**
   * Only the *opening* list is narrowed to teams that welcome beginners. The moment there
   * is a query, every team is searched — otherwise a player whose team doesn't fly that
   * flag could never find it here (FR-002, FR-003).
   */
  private teamParams(): TeamBrowseParams {
    const q = this.teamQuery();
    return {
      q: q || undefined,
      activeOnly: true,
      beginnersWelcome: q ? undefined : true,
      sort: 'NameAsc',
    };
  }

  private reloadTeams(): void {
    // Decides "no teams match that" vs "nothing here yet". DESIGN.md is explicit that an
    // empty state standing in for something else quietly lies to the reader.
    this.teams.filtered.set(this.teamQuery().length > 0);
    this.teams.reload();
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
