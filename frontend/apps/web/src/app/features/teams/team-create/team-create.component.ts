import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ButtonDirective, AlertComponent, LowercaseInputDirective } from '../../../shared/ui';
import { EMPTY, catchError, debounceTime, distinctUntilChanged, of, switchMap, tap } from 'rxjs';
import { SlugAvailability, TeamType } from '../../../core/models/team.models';
import { CityOption, toSelection } from '../../../core/models/city.models';
import { IDENTIFIER_MAX_LENGTH, IDENTIFIER_MIN_LENGTH } from '../../../core/models/identifier.models';
import { MembershipService } from '../../../core/services/membership.service';
import { TeamService } from '../../../core/services/team.service';
import { problemDetail } from '../../../core/utils/problem';
import { CityPickerComponent } from '../../../shared/city-picker/city-picker.component';

/**
 * US1 — create a team. A short form: name, a unique immutable "team handle" (slug,
 * live availability like the @handle), and the type (city team with a city, or a
 * Mixteam with none). The creator becomes the first admin and lands on the team page.
 */
@Component({
  selector: 'jh-team-create',
  imports: [ReactiveFormsModule, RouterLink, ButtonDirective, AlertComponent, LowercaseInputDirective, CityPickerComponent, TranslocoPipe],
  templateUrl: './team-create.component.html',
  styleUrl: './team-create.component.css',
})
export class TeamCreateComponent {
  private readonly teams = inject(TeamService);
  private readonly membership = inject(MembershipService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  protected readonly type = signal<TeamType>('CityTeam');
  protected readonly slugStatus = signal<SlugAvailability | null>(null);
  protected readonly checkingSlug = signal(false);
  /** The availability request itself failed (offline, 5xx). Distinct from "unavailable". */
  protected readonly slugCheckFailed = signal(false);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  // Feature 030 — structured city selection (only relevant for a CityTeam).
  protected readonly selectedCity = signal<CityOption | null>(null);

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
          // now matters, because submit stays blocked until a check succeeds.
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
   * Submit is gated on a *completed, positive* availability check — not merely on the absence of
   * a negative one. While the check is in flight (or still inside the debounce) `slugStatus` is
   * null, and letting that count as "fine" is what allowed a handle to be sent that the check was
   * about to refuse. The server is still the real uniqueness boundary; this is usability.
   */
  protected get canSubmit(): boolean {
    return (
      this.form.valid &&
      !this.submitting() &&
      !this.checkingSlug() &&
      this.slugStatus()?.available === true &&
      (this.type() === 'Mixteam' || this.selectedCity() !== null)
    );
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

  protected submit(): void {
    if (!this.canSubmit) {
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
          this.router.navigate(['/t', team.slug]);
        },
        error: (err) => {
          this.submitting.set(false);
          this.error.set(problemDetail(err));
        },
      });
  }
}
