import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AlertComponent, ButtonDirective, IconComponent, LowercaseInputDirective } from '../../../shared/ui';
import { EMPTY, catchError, debounceTime, distinctUntilChanged, of, switchMap, tap } from 'rxjs';
import { SlugAvailability, TeamType } from '../../../core/models/team.models';
import { CityOption, toSelection } from '../../../core/models/city.models';
import { IDENTIFIER_MAX_LENGTH, IDENTIFIER_MIN_LENGTH } from '../../../core/models/identifier.models';
import { MembershipService } from '../../../core/services/membership.service';
import { TeamService } from '../../../core/services/team.service';
import { problemDetail } from '../../../core/utils/problem';
import { CityPickerComponent } from '../../../shared/city-picker/city-picker.component';
import { InviteSearchComponent } from '../components/invite-search/invite-search.component';

/**
 * Which screen of the wizard is showing. The first three are answered before anything exists;
 * the last two act on a team that has already been created.
 */
type Step = 'basics' | 'type' | 'review' | 'logo' | 'invite';

const STEPS: readonly Step[] = ['basics', 'type', 'review', 'logo', 'invite'];

/**
 * US1 — create a team, as a guided wizard (feature 052, GH #320), in the same calm
 * one-question-per-screen style as onboarding and event creation: name & team handle (with live
 * availability, like the @handle), then type & city, then a review, then two optional steps —
 * a logo and an invite search. The creator becomes the first admin and lands on the team page.
 *
 * ## The team is created in the middle, and that is the whole design
 *
 * Both optional steps act on capabilities addressed by the team's handle and permitted only to
 * its admins (`PUT /teams/{slug}/logo`, `/teams/{slug}/invitations/*`), so neither can run
 * against a team that does not exist yet. The team is therefore created from the **review**
 * step, and {@link createdSlug} is the latch that records it.
 *
 * Everything about the second half derives from that one signal rather than from
 * `stepIndex >= 3`: the Back control is not rendered once it is set, neither optional step can
 * be entered without it, and both finish by navigating to the team. The team handle is
 * `init`-only on the server — immutable once created — so there is genuinely nothing behind
 * Back to return to, and deriving that from *the team existing* keeps it one fact instead of a
 * convention three places have to agree about.
 *
 * The one arrow that crosses the latch backwards is a **409**: a handle that was available when
 * checked but taken by the time Create was pressed. That is the only case in which the team was
 * *not* created, and the only status the recovery branches on — see {@link create}.
 *
 * ## What this deliberately does not do
 *
 * No draft is persisted. The training and event wizards keep unfinished answers in
 * `sessionStorage` (feature 045); this one does not, by recorded scope decision — two short
 * pre-create steps rather than sixteen or twenty-one answers. See
 * `specs/052-team-creation-wizard/plan.md` D6.
 */
@Component({
  selector: 'jh-team-create',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    ButtonDirective,
    AlertComponent,
    LowercaseInputDirective,
    CityPickerComponent,
    InviteSearchComponent,
    TranslocoPipe,
    IconComponent,
  ],
  templateUrl: './team-create.component.html',
  styleUrl: './team-create.component.css',
})
export class TeamCreateComponent {
  private readonly teams = inject(TeamService);
  private readonly membership = inject(MembershipService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  protected readonly steps = STEPS;
  protected readonly step = signal<Step>('basics');
  protected readonly stepIndex = computed(() => STEPS.indexOf(this.step()));

  /**
   * The created team's handle — **the latch**. Set exactly once, by a successful create, and
   * never cleared. See the class comment: the Back control, the optional steps' reachability and
   * the final navigation all read this rather than the step index.
   */
  protected readonly createdSlug = signal<string | null>(null);

  protected readonly type = signal<TeamType>('CityTeam');
  protected readonly slugStatus = signal<SlugAvailability | null>(null);
  protected readonly checkingSlug = signal(false);
  /** The availability request itself failed (offline, 5xx). Distinct from "unavailable". */
  protected readonly slugCheckFailed = signal(false);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  // Feature 030 — structured city selection (only relevant for a CityTeam).
  protected readonly selectedCity = signal<CityOption | null>(null);

  // --- Logo step (feature 051's capability, offered here) --------------------
  protected readonly uploadingLogo = signal(false);
  protected readonly hasLogo = signal(false);
  /**
   * Reads `TeamService.logoUrl`, which appends this slug's revision counter — the GH #283 fix,
   * since a logo's address does not change when its image does. A `computed` because that
   * counter is a signal, so this recomputes after an upload and the new image actually renders.
   */
  protected readonly logoUrl = computed(() => {
    const slug = this.createdSlug();
    return slug ? this.teams.logoUrl(slug) : '';
  });

  protected readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]],
    // Capitals never reach this control — `jhLowercase` folds them on the way in — so the
    // lowercase-only pattern only fires on input the server would refuse too.
    slug: [
      '',
      [
        Validators.required,
        Validators.pattern(/^[a-z0-9]+(?:-[a-z0-9]+)*$/),
        Validators.maxLength(IDENTIFIER_MAX_LENGTH),
      ],
    ],
  });

  /** Bounds interpolated into the tooShort / tooLong messages. */
  protected readonly reasonParams = { min: IDENTIFIER_MIN_LENGTH, max: IDENTIFIER_MAX_LENGTH };

  /**
   * The catalogue key for the server's refusal code (`Taken` → `teams.create.slugReason.taken`).
   * The server sends a code rather than a sentence because its own prose is English-only.
   */
  protected readonly slugReasonKey = computed(() => {
    const reason = this.slugStatus()?.reason;
    return reason
      ? `teams.create.slugReason.${reason[0].toLowerCase()}${reason.slice(1)}`
      : 'teams.create.slugUnavailable';
  });

  /** The first letter, shown while the team has no logo — the same fallback every surface uses. */
  protected readonly logoInitial = computed(
    () => this.form.getRawValue().name.trim().charAt(0).toUpperCase() || '?',
  );

  constructor() {
    this.form.controls.slug.valueChanges
      .pipe(
        // Ordered deliberately: the last verdict is dropped on the *keystroke*, not after the
        // debounce. Clearing it inside the switchMap would leave the previous handle's "available"
        // standing for 300ms, and submit would ride on a verdict about a handle nobody is asking
        // for any more. `distinctUntilChanged` sits ahead of the debounce so that typing a
        // character and deleting it still re-checks, rather than being swallowed as "unchanged"
        // and leaving the status permanently null.
        distinctUntilChanged(),
        tap(() => {
          this.slugStatus.set(null);
          this.slugCheckFailed.set(false);
        }),
        debounceTime(300),
        switchMap((slug) => {
          if (!slug) {
            // Nothing to ask about — and an in-flight check was just cancelled, so stop saying
            // "Checking…" about a handle that no longer exists.
            this.checkingSlug.set(false);
            return EMPTY;
          }
          this.checkingSlug.set(true);
          // Caught inside the switchMap: an error left to reach the subscriber would tear the
          // whole subscription down, and no later keystroke would ever be checked again — which
          // now matters, because advancing stays blocked until a check succeeds.
          return this.teams.checkSlug(slug).pipe(
            catchError(() => {
              this.slugCheckFailed.set(true);
              return of(null);
            }),
          );
        }),
        takeUntilDestroyed(),
      )
      .subscribe((status) => {
        this.slugStatus.set(status);
        this.checkingSlug.set(false);
      });
  }

  /**
   * Whether the current step's answers are complete enough to move on.
   *
   * A plain method rather than a `computed`, matching `EventCreateComponent`: reactive-form reads
   * are not signal dependencies, so a `computed` would cache the first verdict and never revise
   * it. As a method it re-evaluates each change-detection cycle.
   *
   * The two gates are the two halves of what used to be one `canSubmit` on a single screen:
   * - `basics` — a valid name and handle, and a **completed, positive** availability check. Not
   *   merely the absence of a negative one: while a check is in flight `slugStatus` is null, and
   *   letting that count as "fine" is what allowed a handle to be sent that the check was about
   *   to refuse. The server is still the real uniqueness boundary; this is usability.
   * - `type` — a city, for a city team. A Mixteam has none.
   */
  protected canAdvance(): boolean {
    switch (this.step()) {
      case 'basics':
        return (
          this.form.controls.name.valid &&
          this.form.controls.slug.valid &&
          !this.checkingSlug() &&
          this.slugStatus()?.available === true
        );
      case 'type':
        return this.type() === 'Mixteam' || this.selectedCity() !== null;
      default:
        return true;
    }
  }

  /**
   * The form's submit. **Not a synonym for {@link create}** — the whole wizard lives inside one
   * `<form>`, so pressing Enter in the name field would otherwise create a team from the first
   * screen, before the type, the city or the review had been seen. Enter advances instead, which
   * is what it appears to do, and only the review step creates anything.
   */
  protected onSubmit(): void {
    switch (this.step()) {
      case 'review':
        this.create();
        return;
      case 'logo':
        this.continueFromLogo();
        return;
      case 'invite':
        this.finish();
        return;
      default:
        this.next();
    }
  }

  protected next(): void {
    if (!this.canAdvance()) {
      return;
    }
    const i = this.stepIndex();
    if (i < STEPS.length - 1) {
      this.step.set(STEPS[i + 1]);
    }
  }

  /**
   * Back, for the pre-create steps only. The template does not render the control once the team
   * exists; this guard is the same rule stated where it cannot be bypassed.
   */
  protected back(): void {
    if (this.createdSlug() !== null) {
      return;
    }
    const i = this.stepIndex();
    if (i > 0) {
      this.step.set(STEPS[i - 1]);
    }
  }

  /** The review step's "change this" links. Pre-create only, for the same reason as {@link back}. */
  protected goTo(step: Step): void {
    if (this.createdSlug() !== null) {
      return;
    }
    this.step.set(step);
  }

  protected setType(type: TeamType): void {
    this.type.set(type);
    // A Mixteam has no home city; drop any pending selection when switching to it.
    if (type === 'Mixteam') {
      this.selectedCity.set(null);
    }
  }

  protected onCitySelected(option: CityOption | null): void {
    this.selectedCity.set(option);
  }

  /**
   * Create the team. Sends exactly what the single-screen form this replaces sent — FR-027 asks
   * for a team indistinguishable from one created before the wizard existed, and an identical
   * payload is what makes that structural rather than a thing to remember.
   *
   * **Never retried automatically.** A `POST /teams` replayed after a timeout creates a second
   * team, which is the harm Principle VII's browser-hop rule exists to prevent. The retry the
   * spec asks for (FR-010) is the player pressing again, on a failure they can see.
   */
  protected create(): void {
    if (this.submitting() || this.createdSlug() !== null) {
      return;
    }
    const type = this.type();
    const { name, slug } = this.form.getRawValue();

    this.submitting.set(true);
    this.error.set(null);
    this.teams
      .createTeam({
        name: name.trim(),
        slug: slug.trim(),
        type,
        location: type === 'CityTeam' ? toSelection(this.selectedCity()) : null,
      })
      .subscribe({
        next: (team) => {
          // Creating a team changed this player's memberships — refresh the cache the nav's
          // "My team" target and the /my-team page read, or both keep showing the teamless state
          // until the next full page load.
          this.membership.load();
          this.submitting.set(false);
          this.createdSlug.set(team.slug);
          this.step.set('logo');
        },
        error: (err: HttpErrorResponse) => {
          this.submitting.set(false);
          // FR-009 — the handle was taken between the check and this request. The server makes
          // this structural: `CreateTeamStatus.SlugTaken` is the only 409, everything else is a
          // 400. Branch on the status and never on the message, which is English-only prose —
          // the same reason the availability check ships a machine-readable code instead.
          if (err.status === 409) {
            const taken = slug.trim();
            this.slugStatus.set({ slug: taken, normalized: taken, available: false, reason: 'Taken' });
            this.slugCheckFailed.set(false);
            this.step.set('basics');
            return;
          }
          this.error.set(problemDetail(err));
        },
      });
  }

  /**
   * Upload a logo, applying it immediately — the behaviour team settings has had since feature
   * 051, deliberately not onboarding's hold-the-file-and-upload-at-the-end model, which is right
   * only where nothing exists yet to upload to. By this step the team exists.
   */
  protected onLogoSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    const slug = this.createdSlug();
    if (!file || !slug || this.uploadingLogo()) {
      return;
    }

    this.uploadingLogo.set(true);
    this.error.set(null);
    this.teams.uploadLogo(slug, file).subscribe({
      next: () => {
        // The upload bumped this slug's revision, so `logoUrl` now points at a fresh URL.
        this.hasLogo.set(true);
        // The cached membership carries the hasLogo flag the "My team" rows render, so without
        // this the creator would see their new logo here and the old letter tile there.
        this.membership.load();
        this.uploadingLogo.set(false);
        // Clear the picker so choosing the same file again still fires a change event.
        input.value = '';
      },
      // The team is untouched by a refused upload (FR-018): only a message is shown, and both
      // trying another image and skipping stay available.
      error: (err) => {
        this.uploadingLogo.set(false);
        input.value = '';
        this.error.set(problemDetail(err));
      },
    });
  }

  /** Leave an optional step. Carries no state: skipping and continuing are the same move. */
  protected continueFromLogo(): void {
    this.error.set(null);
    this.step.set('invite');
  }

  /** End the wizard on the team's page (FR-014). Reached from the invite step, skipped or not. */
  protected finish(): void {
    const slug = this.createdSlug();
    if (slug) {
      this.router.navigate(['/t', slug]);
    }
  }
}
