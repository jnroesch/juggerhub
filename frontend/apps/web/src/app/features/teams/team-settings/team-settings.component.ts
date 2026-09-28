import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AlertComponent, ButtonDirective, IconComponent, LoadingComponent } from '../../../shared/ui';
import { CityPickerComponent } from '../../../shared/city-picker/city-picker.component';
import { CityOption, Location } from '../../../core/models/city.models';
import {
  TeamDetail,
  TeamDetailsErrorCode,
  TeamLink,
  TeamType,
  UpdateTeamDetails,
} from '../../../core/models/team.models';
import { AuthService } from '../../../core/services/auth.service';
import { MembershipService } from '../../../core/services/membership.service';
import { TeamService } from '../../../core/services/team.service';
import { problemDetail } from '../../../core/utils/problem';

// Feature 061 — the form's shape mirrors the server's rules (TeamService, TeamDetailsPolicy). The
// server decides; these only shape the form and catch the cheap mistakes without a round trip.
const NAME_MIN = 2;
const NAME_MAX = 50;
const DESCRIPTION_MAX = 1000;
const MAX_LINKS = 5;
const KNOWN_CODES: readonly TeamDetailsErrorCode[] = [
  'nameInvalid',
  'cityRequired',
  'mixteamHasCity',
  'cityNotFound',
  'descriptionTooLong',
  'tooManyLinks',
  'linkLabelInvalid',
  'linkUrlInvalid',
  'linkDuplicate',
];

function isKnownCode(code: unknown): code is TeamDetailsErrorCode {
  return typeof code === 'string' && (KNOWN_CODES as readonly string[]).includes(code);
}

/**
 * US5/US6 — "Manage team". An admin manages the team here (logo, recruitment, step down, delete);
 * since GH #361 EVERY member can open it, because it is also where a member manages their own
 * membership — leaving lives here, tucked one page away from the team page, since it happens
 * rarely. Step down and leave share the last-admin guard (the server's, `MutateMembershipAsync`).
 *
 * Feature 051 adds the team logo: upload, replace, remove. Remove is deliberately NOT a
 * danger-zone control — it is reversible by uploading another image, unlike deleting the team.
 *
 * Feature 061 adds **Team details**, the page's first section: the name, type and city (create's
 * rules), the description and up to five links, saved together as one whole replacement. The
 * handle is not editable anywhere. A refusal arrives as a `code` this page translates; the server's
 * English `detail` is never shown (GH #179).
 */
@Component({
  selector: 'jh-team-settings',
  imports: [RouterLink, ButtonDirective, LoadingComponent, AlertComponent, TranslocoPipe, IconComponent, CityPickerComponent],
  templateUrl: './team-settings.component.html',
  styleUrl: './team-settings.component.css',
})
export class TeamSettingsComponent {
  private readonly teams = inject(TeamService);
  private readonly auth = inject(AuthService);
  private readonly membership = inject(MembershipService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly slug = signal('');
  protected readonly detail = signal<TeamDetail | null>(null);
  protected readonly soleAdmin = signal(false);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly working = signal(false);
  protected readonly confirmingDelete = signal(false);
  /** GH #361 — the inline leave confirmation is open (same shape as the delete confirmation). */
  protected readonly confirmingLeave = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly beginnersWelcome = signal(false);
  protected readonly savingBeginners = signal(false);
  // Feature 051 — one busy flag for both logo writes, plus which one is running, so the buttons
  // disable together and only the acting button changes its label.
  protected readonly uploadingLogo = signal(false);
  protected readonly removingLogo = signal(false);

  // --- Team details (feature 061) --------------------------------------------------------------
  // Signals throughout: the app is zoneless, and the Save and Add-link buttons decide their
  // enabled state from these.
  protected readonly detailsName = signal('');
  protected readonly detailsType = signal<TeamType>('CityTeam');
  /** The city the save will send — the team's current one until the admin picks or clears one. */
  protected readonly detailsCity = signal<{ externalId: string; name: string } | null>(null);
  /**
   * What the city picker starts from. `jh-city-picker` reads `initial` once, in `ngOnInit`, so this
   * only matters when the picker is created: on load, and when switching back to a City team (by
   * then cleared, which is the spec's "switching back needs a city chosen again").
   */
  protected readonly detailsInitialCity = signal<Location | null>(null);
  protected readonly detailsDescription = signal('');
  protected readonly detailsLinks = signal<TeamLink[]>([]);
  protected readonly savingDetails = signal(false);
  protected readonly detailsSaved = signal(false);
  /** A translation key, never the server's English. */
  protected readonly detailsError = signal<string | null>(null);
  /** The link row the last refusal was about (the server's `link` index). */
  protected readonly invalidLink = signal<number | null>(null);
  /** A page-level notice (translation key) that must outlive the admin-only section. */
  protected readonly pageNotice = signal<string | null>(null);

  protected readonly maxLinks = MAX_LINKS;
  protected readonly descriptionMax = DESCRIPTION_MAX;
  protected readonly canAddLink = computed(() => this.detailsLinks().length < MAX_LINKS);

  protected readonly isAdmin = computed(() => this.detail()?.myRole === 'Admin');

  protected readonly logoBusy = computed(() => this.uploadingLogo() || this.removingLogo());

  /**
   * Reads the service's per-slug revision signal, so the `<img>` re-fetches after a replace
   * instead of keeping the browser's cached copy of the identical URL (feature 051; GH #283 is
   * the same lesson for avatars).
   */
  protected readonly logoUrl = computed(() => this.teams.logoUrl(this.slug()));

  /** The letter shown while a team has no logo — the same fallback every other surface uses. */
  protected initial(name: string): string {
    return name.trim().charAt(0).toUpperCase() || '?';
  }

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((pm) => {
      this.slug.set(pm.get('slug') ?? '');
      this.load();
    });
  }

  private load(): void {
    this.loading.set(true);
    this.notFound.set(false);
    this.teams.getDetail(this.slug()).subscribe({
      next: (d) => {
        this.detail.set(d);
        this.beginnersWelcome.set(d.beginnersWelcome);
        this.seedDetails(d);
        this.loading.set(false);
        if (d.myRole === 'Admin') {
          this.teams.getMembers(this.slug()).subscribe({
            next: (p) => this.soleAdmin.set(p.items.filter((m) => m.role === 'Admin').length <= 1),
          });
        }
      },
      error: () => {
        this.loading.set(false);
        this.notFound.set(true);
      },
    });
  }

  protected toggleBeginnersWelcome(): void {
    const next = !this.beginnersWelcome();
    this.beginnersWelcome.set(next);
    this.savingBeginners.set(true);
    this.error.set(null);
    this.teams.updateSettings(this.slug(), next).subscribe({
      next: () => {
        this.savingBeginners.set(false);
        this.detail.update((d) => (d ? { ...d, beginnersWelcome: next } : d));
      },
      error: (err) => {
        this.beginnersWelcome.set(!next); // revert on failure
        this.savingBeginners.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }

  // --- Team details (feature 061) --------------------------------------------------------------

  private seedDetails(d: TeamDetail): void {
    this.detailsName.set(d.name);
    this.detailsType.set(d.type);
    this.detailsCity.set(d.location ? { externalId: d.location.externalId, name: d.location.name } : null);
    this.detailsInitialCity.set(d.location);
    this.detailsDescription.set(d.description ?? '');
    this.detailsLinks.set(d.links.map((l) => ({ ...l })));
  }

  /** Any edit retires the "Saved" line, which would otherwise describe a form that has moved on. */
  private edited(): void {
    this.detailsSaved.set(false);
  }

  protected setDetailsName(value: string): void {
    this.detailsName.set(value);
    this.edited();
  }

  protected setDetailsType(type: TeamType): void {
    this.detailsType.set(type);
    // A Mixteam has no home city (FR-003). Back to a City team, the picker starts empty.
    if (type === 'Mixteam') {
      this.detailsCity.set(null);
      this.detailsInitialCity.set(null);
    }
    this.edited();
  }

  protected onDetailsCitySelected(option: CityOption | null): void {
    this.detailsCity.set(option ? { externalId: option.externalId, name: option.name } : null);
    this.edited();
  }

  protected setDetailsDescription(value: string): void {
    this.detailsDescription.set(value);
    this.edited();
  }

  protected setLink(index: number, field: keyof TeamLink, value: string): void {
    this.detailsLinks.update((links) => links.map((l, i) => (i === index ? { ...l, [field]: value } : l)));
    if (this.invalidLink() === index) {
      this.invalidLink.set(null);
    }
    this.edited();
  }

  protected addLink(): void {
    if (!this.canAddLink()) {
      return;
    }
    this.detailsLinks.update((links) => [...links, { label: '', url: '' }]);
    this.edited();
  }

  protected removeLink(index: number): void {
    this.detailsLinks.update((links) => links.filter((_, i) => i !== index));
    this.invalidLink.set(null);
    this.edited();
  }

  /**
   * Save the whole section as one replacement (FR-006). A mutation, so a failure is shown and the
   * admin presses again; nothing retries it (Principle VII). The response is the server's own
   * record — links normalised (`instagram.com/x` comes back as `https://instagram.com/x`) — and
   * the form is re-seeded from it.
   */
  protected saveDetails(): void {
    const slug = this.slug();
    if (this.savingDetails() || !slug) {
      return;
    }

    // A row left completely empty is not a link: drop it rather than refuse the save for it. Done
    // on the form itself so the rows on screen and the server's `link` index count the same list.
    this.detailsLinks.update((links) => links.filter((l) => l.label.trim() || l.url.trim()));

    const name = this.detailsName().trim();
    const type = this.detailsType();
    const city = this.detailsCity();
    this.detailsError.set(null);
    this.invalidLink.set(null);
    this.detailsSaved.set(false);

    // The two mistakes worth catching before a round trip; the server checks everything again.
    if (name.length < NAME_MIN || name.length > NAME_MAX) {
      this.detailsError.set('teams.details.errors.nameInvalid');
      return;
    }
    if (type === 'CityTeam' && !city) {
      this.detailsError.set('teams.details.errors.cityRequired');
      return;
    }

    const body: UpdateTeamDetails = {
      name,
      type,
      location: type === 'CityTeam' && city ? { cityExternalId: city.externalId, name: city.name } : null,
      description: this.detailsDescription().trim() || null,
      links: this.detailsLinks().map((l) => ({ label: l.label.trim(), url: l.url.trim() })),
    };

    this.savingDetails.set(true);
    this.teams.updateDetails(slug, body).subscribe({
      next: (d) => {
        this.detail.set(d);
        this.seedDetails(d);
        this.savingDetails.set(false);
        this.detailsSaved.set(true);
        // The nav and "My team" name the team from this cache (FR-008).
        this.membership.load();
      },
      error: (err: HttpErrorResponse) => {
        this.savingDetails.set(false);
        // Branch on the status and the code, never on the server's English `detail` (GH #179).
        if (err.status === 404 || err.status === 403) {
          // No longer on the team, or no longer an admin: the page as it now is says which, and the
          // notice sits outside the admin-only section that is about to disappear (feature 060).
          this.pageNotice.set(err.status === 403 ? 'teams.details.errors.forbidden' : null);
          this.load();
          return;
        }
        const problem = err.error as { code?: unknown; link?: unknown } | null;
        const code = problem?.code;
        const link = problem?.link;
        if (err.status === 400 && isKnownCode(code)) {
          this.detailsError.set(`teams.details.errors.${code}`);
          if (typeof link === 'number') {
            this.invalidLink.set(link);
          }
          return;
        }
        this.detailsError.set('teams.details.errors.generic');
      },
    });
  }

  protected onLogoSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || this.logoBusy()) {
      return;
    }

    this.uploadingLogo.set(true);
    this.error.set(null);
    this.teams.uploadLogo(this.slug(), file).subscribe({
      next: () => {
        // The upload bumped this slug's revision, so `logoUrl` now points at a fresh URL.
        this.detail.update((d) => (d ? { ...d, hasLogo: true } : d));
        // The cached membership carries the hasLogo flag the "My team" rows render, so without
        // this the admin would see their new logo here and the old letter tile there.
        this.membership.load();
        this.uploadingLogo.set(false);
        // Clear the picker so choosing the same file again still fires a change event.
        input.value = '';
      },
      error: (err) => {
        this.uploadingLogo.set(false);
        input.value = '';
        this.error.set(problemDetail(err));
      },
    });
  }

  protected removeLogo(): void {
    if (this.logoBusy()) {
      return;
    }

    this.removingLogo.set(true);
    this.error.set(null);
    this.teams.removeLogo(this.slug()).subscribe({
      next: () => {
        this.detail.update((d) => (d ? { ...d, hasLogo: false } : d));
        this.membership.load();
        this.removingLogo.set(false);
      },
      error: (err) => {
        this.removingLogo.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }

  protected stepDown(): void {
    this.working.set(true);
    this.error.set(null);
    this.teams.stepDown(this.slug()).subscribe({
      next: () => {
        // The cached membership carries the role the nav and "My team" render.
        this.membership.load();
        this.router.navigate(['/t', this.slug()]);
      },
      error: (err) => {
        this.working.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }

  /**
   * GH #361 — leave the team. The member removes THEMSELVES through the endpoint an admin removes
   * anyone with (`DELETE /teams/{slug}/members/{userId}`; the server allows self-removal). An
   * admin who is the only admin is refused with 409, which the disabled control + warning above
   * already explain; the error path still shows the server's reason for the raced case.
   */
  protected leaveTeam(): void {
    const userId = this.auth.currentUser()?.id;
    if (this.working() || !userId) {
      return;
    }
    this.working.set(true);
    this.error.set(null);
    this.teams.removeMember(this.slug(), userId).subscribe({
      next: () => {
        // The cached memberships name this team in the nav and the team chooser (023 FR-017).
        this.membership.load();
        this.router.navigate(['/my-team']);
      },
      error: (err) => {
        this.working.set(false);
        this.confirmingLeave.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }

  protected deleteTeam(): void {
    this.working.set(true);
    this.error.set(null);
    this.teams.deleteTeam(this.slug()).subscribe({
      next: () => {
        // The team is gone — drop it from the cache, or the nav keeps deep-linking to a dead /t/:slug.
        this.membership.load();
        this.router.navigate(['/']);
      },
      error: (err) => {
        this.working.set(false);
        this.error.set(problemDetail(err));
      },
    });
  }
}
