import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnDestroy, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NgTemplateOutlet } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { ProfileService } from '../../core/services/profile.service';
import { AuthService } from '../../core/services/auth.service';
import { InvitationService } from '../../core/services/invitation.service';
import { MembershipService } from '../../core/services/membership.service';
import { SearchService } from '../../core/services/search.service';
import { TeamService } from '../../core/services/team.service';
import { TeamBrowseParams, TeamCard } from '../../core/models/search.models';
import { InvitePreview, MyInvitation } from '../../core/models/team.models';
import { BrowseList } from '../browse/browse-list';
import { InviteRef, inviteFromReturnUrl, invitePagePath } from '../../core/utils/invite-ref';
import { safeReturnUrl } from '../../core/utils/return-url';
import { PompfeSelectorComponent } from '../profile/components/pompfe-selector/pompfe-selector.component';
import { Pompfe } from '../../shared/pompfen.catalog';
import { ButtonDirective, AlertComponent, CardComponent, ChipDirective, IconComponent, LoadingComponent } from '../../shared/ui';
import { CityPickerComponent } from '../../shared/city-picker/city-picker.component';
import { CityOption, Location, toSelection } from '../../core/models/city.models';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';

type Step = 'welcome' | 'name' | 'city' | 'pompfen' | 'team' | 'photo' | 'done';

/**
 * What the wizard knows about the invite the player arrived with (feature 053). `'none'` when no
 * invite was carried; the rest follow the anonymous preview endpoint's answer.
 */
type InvitePreviewState = 'none' | 'loading' | 'expired' | 'invalid' | { kind: 'usable'; preview: InvitePreview };

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
 * 004's placeholder). When the player arrived through a shared invite link, or has invitations
 * addressed to their account, the step leads with those instead (feature 053) — the invite is
 * previewed, offered, and accepted through the endpoints the invite page and the "My team" home
 * already use. Note what the step deliberately does *not* do: `next()` and `back()` carry no
 * team logic and issue no request, so no state of that step — slow search, failed search,
 * failed join, stale invite, failed accept — can hold a player who registered thirty seconds
 * ago inside the wizard. Asking to join is its own press, and so is accepting an invite. Keep
 * it that way.
 */
@Component({
  selector: 'jh-onboarding',
  imports: [
    FormsModule,
    NgTemplateOutlet,
    PompfeSelectorComponent,
    CityPickerComponent,
    ButtonDirective,
    AlertComponent,
    CardComponent,
    ChipDirective,
    IconComponent,
    LoadingComponent,
    TranslocoPipe,
  ],
  templateUrl: './onboarding.component.html',
  styleUrl: './onboarding.component.css',
})
export class OnboardingComponent implements OnInit, OnDestroy {
  private readonly profiles = inject(ProfileService);
  private readonly auth = inject(AuthService);
  private readonly search = inject(SearchService);
  private readonly teamApi = inject(TeamService);
  private readonly invitations = inject(InvitationService);
  private readonly membership = inject(MembershipService);
  private readonly transloco = inject(TranslocoService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly step = signal<Step>('welcome');
  protected readonly coreSteps = CORE_STEPS;

  // Collected values — prefilled from the current profile so Skip/Back never blank
  // an existing value (the finish payload re-sends what's here).
  protected readonly handle = signal('');
  protected readonly displayName = signal('');
  // Feature 030 — structured home city. `initialLocation` prefills the picker's chip from an
  // existing profile city; `selectedCity` holds a fresh pick; `cityTouched` distinguishes "left
  // unchanged" (omit from the payload) from an explicit clear.
  protected readonly initialLocation = signal<Location | null>(null);
  protected readonly selectedCity = signal<CityOption | null>(null);
  protected readonly cityTouched = signal(false);
  /**
   * True once a home city is persisted server-side (either prefilled from the profile or saved when
   * the player left the city step). Gates the team step's proximity ordering (FR-013): the browse
   * derives the home city server-side, so it must exist on the profile before "near you" can work.
   */
  protected readonly homeCityPersisted = signal(false);
  /** The city step's picker; undefined on every other step. */
  private readonly cityPicker = viewChild(CityPickerComponent);
  /**
   * Holds the city step's Continue while suggestions for the typed text are still on their way —
   * otherwise the player moves on before the city they meant could be picked, and it is silently
   * left unset. Skip stays live: that is an explicit "not now", and the step must never trap anyone.
   */
  protected readonly cityPending = computed(() => this.cityPicker()?.pending() ?? false);
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

  // --- Team step: invitations (feature 053) ---------------------------------
  // The invite the player arrived with rides in on the returnUrl sign-in carried here; it is
  // an identity (slug + token), never a path, and a malformed one is simply "no invite".
  // Accepting is its own press — acceptInvite() — never Continue.

  /** The shared invite link the player registered from, if the returnUrl is the invite page. */
  protected readonly carriedInvite: InviteRef | null = inviteFromReturnUrl(
    this.route.snapshot.queryParamMap.get('returnUrl'),
  );
  /** What the preview said about the carried invite. */
  protected readonly invitePreview = signal<InvitePreviewState>('none');
  /** Usable targeted invitations addressed to this account (the "My team" home's list). */
  protected readonly addressedInvites = signal<MyInvitation[]>([]);
  /** Teams joined through this step, in order — the first is where the wizard exits to. */
  protected readonly joinedSlugs = signal<readonly string[]>([]);
  /** In-flight guard for an accept. Never gates Continue. */
  protected readonly acceptingToken = signal<string | null>(null);
  protected readonly inviteError = signal<string | null>(null);
  /** Teams the player already belongs to, so "already on that team" can be told before a press. */
  protected readonly memberSlugs = computed(() => new Set(this.membership.teams().map((t) => t.slug)));
  private membershipsRequested = false;

  /** The carried invite's preview once it is known to be usable. */
  protected readonly usableInvite = computed(() => {
    const state = this.invitePreview();
    return typeof state === 'object' ? state.preview : null;
  });

  /**
   * Addressed invitations minus any for the carried invite's team: a link and a targeted invite
   * to one team are one offer to the player, and the carried one leads (research R8).
   */
  protected readonly visibleAddressed = computed(() => {
    const carriedTeam = this.usableInvite()?.teamSlug;
    return this.addressedInvites().filter((inv) => inv.teamSlug !== carriedTeam);
  });

  /** True when the step is leading with at least one invitation (the search becomes "or…"). */
  protected readonly anyInviteShown = computed(
    () => this.usableInvite() !== null || this.visibleAddressed().length > 0,
  );

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
        this.initialLocation.set(p.location ?? null);
        this.description.set(p.description ?? '');
        this.selectedPompfen.set([...p.pompfen]);
        // A returning player who already has a home city gets proximity ordering from the start —
        // no need to re-pick. Refresh the opening list so it leads with nearby teams.
        if (p.location) {
          this.homeCityPersisted.set(true);
          this.reloadTeams();
        }
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

    // Feature 053 — the invitations the step will lead with, fetched the same way and for the
    // same reason. Neither may block anything: a failed preview is a quiet note, a failed list
    // is an empty list.
    if (this.carriedInvite) {
      this.loadCarriedInvite(this.carriedInvite);
    }
    this.loadAddressedInvites();
  }

  /** Preview the carried invite through the same anonymous endpoint the invite page uses. */
  private loadCarriedInvite(invite: InviteRef): void {
    this.invitePreview.set('loading');
    this.teamApi.getInvitePreview(invite.token).subscribe({
      next: (preview) => {
        if (preview.state === 'Usable') {
          this.invitePreview.set({ kind: 'usable', preview });
          this.ensureMemberships();
        } else {
          this.invitePreview.set(preview.state === 'Expired' ? 'expired' : 'invalid');
        }
      },
      // A 404 (no such invite, or a team that no longer exists) and any other failure read the
      // same to the player: this invite is no longer valid. Nothing here disables the search.
      error: () => this.invitePreview.set('invalid'),
    });
  }

  /** The account's own pending invitations (feature 023's list). A failure is an empty list. */
  private loadAddressedInvites(): void {
    this.invitations.listMine().subscribe({
      next: (page) => {
        this.addressedInvites.set(page.items);
        if (page.items.length > 0) {
          this.ensureMemberships();
        }
      },
      error: () => this.addressedInvites.set([]),
    });
  }

  /**
   * `/onboarding` sits outside the shell, so the shell's `membership.load()` has never run here.
   * The accept endpoint answers 200 for "joined" and "already a member" alike, so the list is the
   * only way to tell a player they are already on a team *before* they press (FR-021). Loaded
   * once, and only when an invitation is actually known — a wizard with none stays as it was.
   */
  private ensureMemberships(): void {
    if (this.membershipsRequested) {
      return;
    }
    this.membershipsRequested = true;
    this.membership.load();
  }

  ngOnDestroy(): void {
    this.teams.destroy();
  }

  /** The city picker's selection changed (a pick or a clear). Recorded so finish() can persist it. */
  protected onCitySelected(option: CityOption | null): void {
    this.selectedCity.set(option);
    this.cityTouched.set(true);
  }

  protected next(): void {
    const current = this.step();
    const i = FLOW.indexOf(current);
    if (i < FLOW.length - 1) {
      this.step.set(FLOW[i + 1]);
    }
    // Feature 030 — leaving the city step, persist a freshly-picked city so the team step (two steps
    // on) can order by proximity. Fire-and-forget: a failure simply leaves proximity off and the team
    // step keeps the 029 default. Navigation is never blocked (the "never trap a new player" rule).
    if (current === 'city') {
      this.persistHomeCityForProximity();
    }
  }

  /**
   * Persist the picked home city on its own (feature 030, FR-013), without writing the rest of the
   * still-unfinished onboarding profile. Only fires for an actual pick this session; a cleared or
   * untouched picker leaves proximity off. On success the team list is refreshed to lead with nearby
   * teams; on failure nothing happens beyond proximity staying off.
   */
  private persistHomeCityForProximity(): void {
    const city = this.selectedCity();
    if (!this.cityTouched() || !city) {
      return;
    }
    this.profiles.setHomeCity({ cityExternalId: city.externalId, name: city.name }).subscribe({
      next: () => {
        this.homeCityPersisted.set(true);
        this.reloadTeams();
      },
      error: () => {
        /* proximity stays off; the team step falls back to the default ordering (never blocks) */
      },
    });
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

  /** Feature 051 — the suggestion row's logo URL. */
  protected logoUrl(slug: string): string {
    return this.teamApi.logoUrl(slug);
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

  // --- Team step: invitations (feature 053) ---------------------------------

  /**
   * Accept an invitation — the carried one or one addressed to the account. Like `askToJoin()`
   * this is its own press and is deliberately *not* wired to Continue (029 FR-012/FR-018 carried
   * over as 053 FR-011/FR-023): the wizard's primary action stays free of any network call.
   *
   * No retry, timeout, or backoff here. `retryInterceptor` time-limits this POST and never
   * repeats it (constitution VII); a failed accept is reported and stays pressable.
   */
  protected acceptInvite(token: string, teamSlug: string): void {
    if (this.acceptingToken() !== null || this.joinedSlugs().includes(teamSlug)) {
      return;
    }
    this.acceptingToken.set(token);
    this.inviteError.set(null);

    this.teamApi.acceptInvite(token).subscribe({
      next: (result) => {
        this.acceptingToken.set(null);
        this.joinedSlugs.update((slugs) => [...slugs, result.teamSlug]);
        // The nav's "My team" cache and the already-member set both read this.
        this.membership.load();
      },
      error: () => {
        this.acceptingToken.set(null);
        // One plain sentence whatever the status: the card's state on reload tells the truth,
        // and no code or internal detail reaches the reader (Principle I, FR-024).
        this.inviteError.set(this.transloco.translate('onboarding.team.invite.acceptError'));
      },
    });
  }

  /** Decline an addressed invitation. Gone from the step either way (023 precedent). */
  protected declineInvite(token: string): void {
    this.teamApi.declineInvite(token).subscribe({
      next: () => this.removeAddressed(token),
      error: () => this.removeAddressed(token),
    });
  }

  private removeAddressed(token: string): void {
    this.addressedInvites.update((list) => list.filter((inv) => inv.token !== token));
  }

  /**
   * The opening list (no query) is narrowed to beginner-friendly teams (FR-002/FR-003) — UNLESS the
   * player has a home city, in which case "near you" is the stronger signal for a newcomer: we drop
   * the beginners filter and order every nearby team by distance (feature 030, FR-013). The moment
   * there is a query, every team is searched. When a home city exists, results are proximity-ordered
   * (server-derived); otherwise the 029 default A–Z applies.
   */
  private teamParams(): TeamBrowseParams {
    const q = this.teamQuery();
    const near = this.homeCityPersisted();
    return {
      q: q || undefined,
      activeOnly: true,
      beginnersWelcome: q || near ? undefined : true,
      sort: near ? 'Proximity' : 'NameAsc',
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
        // Only send a location change if the player touched the picker; otherwise leave it unchanged
        // (feature 030 contract: null location ⇒ no change).
        location: this.cityTouched() ? toSelection(this.selectedCity()) : null,
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
          this.error.set("We couldn't save your profile. Please try again.");
        },
      });
  }

  /**
   * Refresh the cached session (so the guard sees onboardingCompleted) and enter the
   * app at wherever `exitTarget()` says.
   */
  protected enterApp(): void {
    const target = this.exitTarget();
    this.auth.loadSession().subscribe({
      next: () => this.router.navigateByUrl(target),
      error: () => this.router.navigateByUrl(target),
    });
  }

  /**
   * Where the wizard lets the player out (feature 053, research R5):
   *
   * 1. A team joined through the step → that team's page.
   * 2. A carried invite the step *offered* (usable, and the player walked past Welcome) that
   *    they chose not to press → the invite page, WITHOUT the `?action=accept` sign-in carried.
   *    The invite page resumes that action automatically; letting it run here would turn
   *    Continue into an accept by another route (FR-011). Dismissing at Welcome never showed
   *    the card, so there the pre-sign-in intent still stands, exactly as before this feature.
   * 3. A carried invite that turned out stale → the dashboard; the step already said so.
   * 4. Otherwise the returnUrl sign-in carried in — an action pending since before the player
   *    signed up — takes precedence over the dashboard so it can resume, as it always has.
   */
  private exitTarget(): string {
    const joined = this.joinedSlugs()[0];
    if (joined) {
      return `/t/${joined}`;
    }
    const carried = this.carriedInvite;
    if (carried) {
      const state = this.invitePreview();
      if (typeof state === 'object' && this.step() !== 'welcome') {
        return invitePagePath(carried);
      }
      if (state === 'expired' || state === 'invalid') {
        return '/';
      }
    }
    return safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl')) ?? '/';
  }

  private blankToNull(value: string): string | null {
    const trimmed = value.trim();
    return trimmed.length === 0 ? null : trimmed;
  }
}
