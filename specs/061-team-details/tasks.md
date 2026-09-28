# Tasks: Team Details — Editable Name, Type and City, a Description, and Links

**Input**: Design documents from `specs/061-team-details/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/team-details-api.md](./contracts/team-details-api.md), [quickstart.md](./quickstart.md)

**Tests**: included. Each behaviour gets its test before, or alongside, the code that makes it
pass. The rename-rewrite tests must be seen to **fail** before the rewrite exists (T013 before
T016).

**Organization**: by user story. The one endpoint, `PUT /teams/{slug}/details`, carries every
field. It is therefore built, with its whole validation, in the Foundational phase. Each story
then adds its own server behaviour, its tests and its UI.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an unfinished task)
- **[Story]**: US1–US4 from spec.md

---

## Phase 1: Setup

- [X] T001 Record the baseline on the fresh branch. Run `dotnet test backend/tests/JuggerHub.Api.IntegrationTests --filter "FullyQualifiedName~Teams|FullyQualifiedName~TeamBrowse|FullyQualifiedName~Results|FullyQualifiedName~Notifications|FullyQualifiedName~Home"`, and in `frontend/` run `npx nx test web --watch=false --testPathPatterns="team-settings|team-detail|team-create|catalog-"`. Note any pre-existing failure so it is not later mistaken for a regression. *(Done: 292/292 backend, the frontend subset green; no pre-existing failure.)*

---

## Phase 2: Foundational (blocking: the model, the rules, the endpoint)

- [X] T002 [P] Create `backend/Entities/TeamLink.cs`: `sealed class TeamLink : BaseEntity` with `Guid TeamId`, `Team Team`, `string Label`, `string Url`, `int Position`, and XML docs (feature 061; replaced as a whole on every details save; no client-addressable identity). In `backend/Entities/Team.cs` add `string? Description` (doc: plain text ≤1000, null = none) and `ICollection<TeamLink> Links`.
- [X] T003 [P] Create `backend/Services/Teams/TeamDetailsPolicy.cs`, a pure static class (the `TeamSlugPolicy` precedent). It holds:
  - the constants `DescriptionMaxLength = 1000`, `MaxLinks = 5`, `LabelMaxLength = 30`, `UrlMaxLength = 500`;
  - `string? NormalizeDescription(string?)` (trim; blank ⇒ null);
  - a link normaliser that returns either the normalised `(Label, Url)` or a failure code, per research R6:
    - label: trim; 1–30; no `char.IsControl`;
    - address: trim; no `://` ⇒ prefix `https://`; `Uri.TryCreate(Absolute)`; scheme exactly `https`; `HostNameType == Dns` with a `.`; empty `UserInfo`; stored value `uri.AbsoluteUri` ≤ 500;
  - a list validator (count ≤ 5, duplicates by normalised URL; the index reported is the later one);
  - the code enum `TeamDetailsCode { NameInvalid, CityRequired, MixteamHasCity, CityNotFound, DescriptionTooLong, TooManyLinks, LinkLabelInvalid, LinkUrlInvalid, LinkDuplicate }`.
- [X] T004 [P] Create `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamDetailsPolicyTests.cs`, pure unit tests with no factory (the `TugenyLinkParserTests` precedent), covering:
  - `instagram.com/x` ⇒ `https://instagram.com/x`;
  - refused: `http://a.de`, `javascript:alert(1)`, `javascript://x`, `mailto:a@b.de`, `data:text/html,x`, `https://instagram.com@example.net`, `https://localhost`, `ftp://a.de`;
  - an address over 500 characters after normalisation is refused;
  - an IDN host is accepted;
  - labels: empty, 31 characters, and one containing a line break are refused; a 30-character label is accepted;
  - duplicates are detected across case differences in the host only;
  - six links are refused;
  - the description: blank ⇒ null; 1000 accepted; 1001 refused.
- [X] T005 Configure the model in `backend/Data/AppDbContext.cs`:
  - `Team.Description` max length `TeamDetailsPolicy.DescriptionMaxLength`;
  - a `builder.Entity<TeamLink>` block: `Label` (`LabelMaxLength`, required), `Url` (`UrlMaxLength`, required), `HasOne(Team).WithMany(Links).OnDelete(Cascade)`, unique index `(TeamId, Position)`, with a comment on why cascade is safe (no stored object, unlike the logo);
  - `DbSet<TeamLink> TeamLinks`.
- [X] T006 In `backend/Dtos/Teams/TeamDtos.cs`:
  - add `TeamLinkInput(string? Label, string? Url)` and `UpdateTeamDetailsRequest([Required, MaxLength(200)] string Name, [Required] TeamType Type, LocationSelectionDto? Location, [MaxLength(4000)] string? Description, [MaxLength(20)] IReadOnlyList<TeamLinkInput>? Links)`, with a doc comment stating that these attributes are payload guards looser than the rules on purpose (research R8);
  - add `TeamLinkDto(string Label, string Url)`;
  - **append** `string? Description = null, IReadOnlyList<TeamLinkDto>? Links = null` to `TeamDetailDto`;
  - **append** `string? Description, IReadOnlyList<TeamLinkDto> Links` at the END of the positional `TeamPublicDetailDto` (after `Achievements`).
- [X] T007 Build (`dotnet build backend/JuggerHub.slnx`, gate on its exit code: do not pipe through grep). Then generate the migration **with** a build: `dotnet ef migrations add AddTeamDescriptionAndLinks --project backend` (output lands in `backend/Data/Migrations/`). Open it and confirm it adds `Teams.Description varchar(1000) NULL` and creates `TeamLinks` with the FK (cascade) and the unique index. An empty `Up()` means the build step was skipped (056).
- [X] T008 In `backend/Services/Teams/ITeamService.cs` add `TeamDetailsStatus { Updated, Invalid, Forbidden, NotFoundOrNotMember }`, `TeamDetailsResult(TeamDetailsStatus Status, TeamDetailDto? Team, TeamDetailsCode? Code, int? LinkIndex, string? Reason)` with `Ok`/`Fail` factories, and `Task<TeamDetailsResult> UpdateDetailsAsync(string slug, Guid actorUserId, UpdateTeamDetailsRequest request, CancellationToken ct = default)`.
- [X] T009 Implement `UpdateDetailsAsync` in `backend/Services/Teams/TeamService.cs`, following research R2 **without** the rename rewrites (US1 adds them):
  1. The guard: not a member ⇒ `NotFoundOrNotMember`; not an admin ⇒ `Forbidden`.
  2. Validate in the data-model table's order. Extract create's name rule (`2..NameMaxLength` after trim) into one private helper and call it from both `CreateAsync` and here.
  3. Resolve the city per R5: skip when `CityExternalId` equals the team's current city's `ExternalId`; else `ResolveAndUpsertAsync`, catching `CityNotResolvableException` ⇒ `CityNotFound`.
  4. Run `CreateExecutionStrategy().ExecuteAsync`: `ChangeTracker.Clear()` → `BeginTransactionAsync` → `SELECT 1 FROM "Teams" WHERE "Id" = {id} FOR UPDATE` → read the current `Name` → `ExecuteUpdate` of `Name`, `Type`, `CityId`, `Description` and **`ModifiedDate`** → `ExecuteDelete` of the team's `TeamLinks` → `AddRange` of new `TeamLink` rows (Position = index) → `SaveChangesAsync` → `CommitAsync`. Leave a clearly marked spot after the name read for the US1 rewrites, and return whether the name changed.
  5. Re-read and return the `TeamDetailDto`.

  Also extend the projections in `GetDetailAsync`, `GetPublicDetailAsync` and the `CreateAsync` return with `t.Description` and `t.Links.OrderBy(l => l.Position).Select(l => new TeamLinkDto(l.Label, l.Url)).ToList()`, in the query that already runs (no second round trip).
- [X] T010 Add `[HttpPut("{slug}/details")] UpdateDetails` in `backend/Controllers/TeamsController.cs`, mapping the result:
  - `Updated` ⇒ `Ok(dto)`;
  - `Forbidden` ⇒ `Forbidden("Only admins can change the team's details.")`;
  - `NotFoundOrNotMember` ⇒ `TeamNotFound()`;
  - `Invalid` ⇒ 400 ProblemDetails `title: "Invalid team details"`, `detail: reason`, with `Extensions["code"]` = the camelCase code name and `Extensions["link"]` for the three link codes. Follow `EventResultsController`'s `row` extension.
- [X] T011 [P] Frontend model and service:
  - in `frontend/apps/web/src/app/core/models/team.models.ts`, add `TeamLink`, `UpdateTeamDetails` and `TeamDetailsErrorCode` per the contract, and add `description: string | null; links: TeamLink[]` to `TeamDetail` and `TeamPublicDetail`;
  - in `frontend/apps/web/src/app/core/services/team.service.ts`, add `updateDetails(slug, body): Observable<TeamDetail>` (`PUT …/details`);
  - fix every fixture that builds a `TeamDetail`/`TeamPublicDetail` (run `npx tsc -p apps/web/tsconfig.app.json --noEmit` and the spec compile to list them), giving each `description: null, links: []`.

**Checkpoint**: backend builds, T004 green, frontend compiles. Commit
`feat(061): team details endpoint, description and links model (#359, #321)`.

---

## Phase 3: User Story 1 — An admin changes the team's name, type or city (P1) 🎯 MVP

**Goal**: name, type and city are editable in Manage team, and a rename reaches every copy of the
name (FR-001–FR-012).

**Independent test**: rename, change the city, then flip the type. After each save the team
page shows the new values. After the rename, an alert and a placement from before it show the
new name, and the address is unchanged.

### Tests

- [X] T012 [P] [US1] Create `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamDetailsTests.cs` (`[Collection("Teams")]`, helpers mirroring `TeamNewsEditDeleteTests`: `Player`, `NewUserAsync`, `CreateTeamAsync` with `TEST:berlin`, `JoinAsync`). Facts:
  - (a) an admin renames the team: 200 with the new name, the new name in `GET /teams/{slug}` and `/public`, the slug unchanged, and team browse (`/api/v1/teams?q=`) finds the team by the new name;
  - (b) a name of 1 or 51 characters ⇒ 400 `code == "nameInvalid"`, with the name unchanged in the DB;
  - (c) the city changes to `TEST:hamburg`: `location.externalId` matches, and browse's city filter finds the team under Hamburg;
  - (d) CityTeam → Mixteam with `location: null` ⇒ `CityId` null in the DB;
  - (e) Mixteam → CityTeam with no city ⇒ `cityRequired`, and a Mixteam with a city ⇒ `mixteamHasCity`, nothing changed either time;
  - (f) an unknown external id ⇒ `cityNotFound`;
  - (g) R5: resending the current city (only the description changed) keeps the same `CityId`;
  - (h) a plain member ⇒ 403; a signed-in non-member ⇒ 404 with the same `title` as `PUT /teams/doesnotexist/details`; anonymous ⇒ 401;
  - (i) independence: `beginnersWelcome` set by PATCH survives a details save, `hasLogo` survives, and a later PATCH leaves name, description and links unchanged;
  - (j) atomicity: a valid new name together with an invalid link ⇒ 400, and the name is still the old one;
  - (k) `Teams.ModifiedDate` moved (Gate 2).
- [X] T013 [P] [US1] Create `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamRenameRewriteTests.cs` (`[Collection("Teams")]`).

  Arrange team *Rheinfuer*, with alerts produced through the **real** flows:
  - `TeamInvite`: a targeted invite to X;
  - `TeamRoleChanged`: promote B;
  - `TeamNews`: a post;
  - `TeamJoinRequest`: J requests, so the admin receives it;
  - `TeamJoinRequestAnswered`: decline J.

  Then insert rows directly for `PartyRequest` (one with a dedupe key, one **null** like the nudge), `PartyNews` and `MarketInvite`, with payloads shaped exactly as `PartyService.cs:153`, `PartyRosterService.cs:286`, `PartyNewsService.cs:261` and `MarketRequestService.cs:265` build them (read them first; include `eventName`). Let C (a member with a news alert) **leave**. Create a second team with the same display name but another slug, with its own alert.

  Facts after renaming to *Rheinfeuer*:
  - (a) every one of the nine kinds, C's included, has `teamName == "Rheinfeuer"`, and every other payload key is unchanged (`eventName` too);
  - (b) `IsRead`, `CreatedDate`, `DedupeKey` and `Type` are unchanged row by row, and `ModifiedDate` moved;
  - (c) the other team's row still says *Rheinfuer*;
  - (d) nothing was sent: the total notification count is unchanged, no email reached `TestEmailSender`, `_factory.PushDispatcher` got no recipients, and `_factory.NotificationRealtime` recorded nothing;
  - (e) B's Home activity (`/api/v1/home`) "RoleChanged" entry has `teamName == "Rheinfeuer"`;
  - (f) saving the details again with the **same** name moves no notification's `ModifiedDate` (FR-011).

  Run it and **confirm (a) fails** before T016.
- [X] T014 [P] [US1] Create `backend/tests/JuggerHub.Api.IntegrationTests/Results/TeamRenamePlacementTests.cs` (`[Collection("Results")]`, derive from `ResultsTestSupport` like `TeamPlacementHistoryTests`). Arrange a past tournament whose ranking has the team connected, plus an unconnected placement whose `SourceName` equals the team's old name, plus another team's connected placement, with matches linked to the connected placement if the support allows (else set `FirstPlacementId` directly). Rename the team. Facts:
  - (a) the connected placement's ranking row and the linked match side show the new name, and `SourceName` is unchanged;
  - (b) the unconnected placement and the other team's placement are unchanged;
  - (c) the result's last-changed date (whatever field the results DTO exposes) is unchanged;
  - (d) the same-name save touches no placement.

### Implementation

- [X] T015 [US1] Add `Task<int> ReplaceTeamNameAsync(string teamSlug, string teamName, CancellationToken ct = default)` to `backend/Services/Notifications/INotificationService.cs`, with a doc: realtime-free, joins the ambient transaction, silent (FR-009), found by slug never roster.
- [X] T016 [US1] Implement `ReplaceTeamNameAsync` in `backend/Services/Notifications/NotificationService.cs` beside `ReplacePayloadAsync`: one `ExecuteSqlInterpolatedAsync` per research R3 (`"Payload" = "Payload" || jsonb_build_object('teamName', {name}::text)`, `"ModifiedDate" = {now}`, `WHERE "Payload" ->> 'teamSlug' = {slug} AND jsonb_typeof("Payload" -> 'teamName') = 'string' AND "Payload" ->> 'teamName' IS DISTINCT FROM {name}`). Add a comment that the two keys are the camelCase of the payload records' `TeamSlug`/`TeamName` (`PayloadJson`), and why there is no type list. Fix any existing test fakes implementing `INotificationService`.
- [X] T017 [US1] In `UpdateDetailsAsync` (`backend/Services/Teams/TeamService.cs`), at the spot left in T009 and **only when the name changed**: call `_notifications.ReplaceTeamNameAsync(slug, newName, ct)`, then `_db.TournamentPlacements.Where(p => p.TeamId == teamId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Name, newName).SetProperty(p => p.ModifiedDate, now), ct)`. Both go inside the strategy delegate, before `CommitAsync`. Run T012–T014 green.
- [X] T018 [US1] In `frontend/apps/web/src/app/features/teams/team-settings/team-settings.component.{ts,html}`, add the **Team details** section as the FIRST section, admins only:
  - a name input (`maxlength=50`, required);
  - the type as the wizard's two-button segmented control (copy its markup from `team-create.component.html:69-81`; switching to Mixteam clears the pending city);
  - `jh-city-picker [initial]="t.location"` for a City team (rendered only once `detail` is loaded; it reads `initial` in `ngOnInit`);
  - **Save** (`jhButton`, the page's one primary), with `Saving…` while busy and a `role="status"` *Saved* line on success.

  State: signals for `detailsType`, `detailsCity`, `savingDetails`, `detailsSaved`, `detailsError` (translation key) and `invalidLinkIndex`, plus a reactive group for name/description. The request body is `{ name, type, location: type === 'CityTeam' ? (picked ? toSelection(picked) : { cityExternalId: t.location.externalId, name: t.location.name }) : null, description, links }`.

  On success: `detail.set(response)`, re-seed the form, `membership.load()`. On error: map `err.error?.code` to `teams.details.errors.<code>`, 403 to `errors.forbidden`, 404 to `errors.notFound` + `load()`, anything else to `errors.generic`. **Never** `problemDetail(err)` here (#179). Never auto-retry.
- [X] T019 [US1] Extend `frontend/apps/web/src/app/features/teams/team-settings/team-settings.component.spec.ts`:
  - the section renders for an admin with the current values and not for a member;
  - Save sends the expected body, keeping the current city's external id when untouched and sending `location: null` after switching to Mixteam;
  - success updates the page and calls `membership.load`;
  - a 400 `{code:'cityRequired'}` shows the translated key, not the server `detail`;
  - a 404 triggers a reload.

  Stub `updateDetails` in every existing TestBed setup.
- [X] T020 [US1] Add the `teams.details.*` keys for the section (title, name, type, city, save, saving, saved, and `errors.{nameInvalid,cityRequired,mixteamHasCity,cityNotFound,forbidden,notFound,generic}`) to **all three** `frontend/apps/web/public/i18n/{en,de,es}.json` in one change. German *Teamdetails*; the dashes rule applies.
- [X] T021 [US1] Add an amendment note to `specs/050-tournament-results/data-model.md` beside rule 3 ("Connecting sets `Name = Team.Name`"): *Amended by 061: renaming the connected team sets `Name = Team.Name` too (the spec's "a team is renamed later: its placements show its current name").*

**Checkpoint**: backend suites + `team-settings` spec green. Commit
`feat(061): admins change a team's name, type and city; a rename reaches alerts and results (#359)`.

---

## Phase 4: User Story 2 — A team says who it is (P2)

**Goal**: a description of up to 1000 characters, edited in Team details and shown on the team
page to every signed-in viewer (FR-013, FR-014).

**Independent test**: save a description with a line break, open the page as a non-member, see
it verbatim, clear it, and see the card disappear.

- [ ] T022 [P] [US2] Add facts to `TeamDetailsTests.cs`:
  - a description with a line break round-trips, and appears in `/public` for a signed-in **non-member** and in the members' detail;
  - `"   \n  "` ⇒ `description == null`;
  - 1001 characters ⇒ 400 `descriptionTooLong`, nothing changed;
  - emptying clears it.
- [ ] T023 [US2] In the settings Team details section, add a description textarea (`rows=5`, `maxlength=1000`, a live character counter in `caption`/`text-muted`, and the hint "plain text, line breaks kept"). Map `descriptionTooLong`. Extend the settings spec: the body carries the trimmed text, and blank sends `null`.
- [ ] T024 [US2] In `frontend/apps/web/src/app/features/teams/team-detail/team-detail.component.html`, add the **About** `jh-card` (`data-testid="about"`, `h2` `teams.detail.about`). It goes at the top of the main column, after the join-queue block and before the roster, and renders only when `team.description || team.links.length`. The description is a `<p class="whitespace-pre-line break-words text-body-md text-body">` with interpolation only. Extend `team-detail.component.spec.ts`:
  - the card shows for a non-member;
  - text containing `<b>x</b>` and `https://x.de` renders as literal text, with no `b` element and no `a` element inside the paragraph;
  - there is no card when both the description and the links are empty.
- [ ] T025 [US2] Add the keys `teams.detail.about` and `teams.details.{description,descriptionHint,descriptionCount}` and `teams.details.errors.descriptionTooLong` to all three catalogues. German *Über das Team*, Spanish *Sobre el equipo*.

**Checkpoint**: commit `feat(061): teams have a description (#321)`.

---

## Phase 5: User Story 3 — A team points at its website, Instagram and Discord (P2)

**Goal**: up to five labelled https links, edited in Team details and shown in the About card
with their real host (FR-015–FR-019).

**Independent test**: add three links, one scheme-less; each shows its label and host and opens in
a new tab. `http`, `javascript`, a sixth link and a user-info disguise are each refused with a
reason.

- [ ] T026 [P] [US3] Add facts to `TeamDetailsTests.cs`:
  - three links are saved in order, and `instagram.com/x` comes back as `https://instagram.com/x`;
  - each of `http://…`, `javascript:alert(1)`, `mailto:…`, `https://instagram.com@example.net`, a duplicate, an empty label, a 31-character label and six links ⇒ 400 with the right `code` and `link` index, and nothing stored;
  - a second save replaces the list as a whole (fewer links, new order);
  - `/public` carries the links for a non-member;
  - `DELETE /teams/{slug}` leaves no `TeamLinks` rows (cascade).
- [ ] T027 [P] [US3] Create `frontend/apps/web/src/app/core/utils/link-host.ts`: `linkHost(url: string): string` returns `new URL(url).host` with a leading `www.` removed, or `''` when it cannot parse. Add `link-host.spec.ts`:
  - `https://www.instagram.com/x` ⇒ `instagram.com`;
  - an IDN host (Cyrillic `і`) ⇒ the `xn--` form;
  - `https://a.de:8443/x` keeps the port;
  - garbage ⇒ `''`.
- [ ] T028 [US3] Add the links editor to the settings Team details section:
  - a `FormArray` (or a signal list) of `{label, url}` rows, each a label input (`maxlength=30`) and an address input (`type="url"`, `inputmode="url"`, placeholder `https://…`), **stacked on narrow screens and side by side from `sm`**, with a remove icon button (`jh-icon x`, with an `aria-label`);
  - an **Add link** secondary button, disabled at five (the count is a signal);
  - rows sent in order;
  - the row named by the error's `link` index gets `aria-invalid="true"` and the error text beside it;
  - maps `tooManyLinks`/`linkLabelInvalid`/`linkUrlInvalid`/`linkDuplicate`.

  Extend the settings spec: add/remove rows, the disabled state at five, the body order, and the row highlighting from `link: 1`.
- [ ] T029 [US3] In the About card, render the links as a list below the description. Each row is `<a [href]="l.url" target="_blank" rel="noopener noreferrer nofollow ugc">` with the label `underline break-words`, `jh-icon name="external-link" size="sm"`, an `sr-only` *(opens in a new tab)*, and `linkHost(l.url)` in `text-caption text-muted break-all`. Extend `team-detail.component.spec.ts`:
  - the exact `rel`/`target` values;
  - the host text;
  - the order;
  - a card with links but no description renders without an empty paragraph.
- [ ] T030 [US3] Add the keys `teams.details.{links,linksHint,linkLabel,linkUrl,addLink,removeLink,linksMax}`, `teams.details.errors.{tooManyLinks,linkLabelInvalid,linkUrlInvalid,linkDuplicate}` and `teams.detail.{links,opensInNewTab}` to all three catalogues. German *Links*, *Link hinzufügen*, *Bezeichnung*, *Adresse*.

**Checkpoint**: commit `feat(061): teams list up to five links (#359)`.

---

## Phase 6: User Story 4 — A new team writes its description while being created (P3)

**Goal**: an optional `about` step after the logo step (FR-020–FR-022).

**Independent test**: create a team with a description (the page shows it). Create one skipping
the step (no `PUT` sent, no description).

- [ ] T031 [P] [US4] Extend `frontend/apps/web/src/app/features/teams/team-create/team-create.component.spec.ts`:
  - after the logo step comes `about`; its button reads Skip while blank and Continue once text is typed;
  - Skip goes to `invite` with **no** `updateDetails` call;
  - Continue with text calls `updateDetails(slug, { name, type, location, description, links: [] })` built from the create response (a City team sends the created city's external id);
  - a failure keeps the text, shows the translated error and offers a secondary Skip;
  - the step count is 6.
- [ ] T032 [US4] Implement it in `frontend/apps/web/src/app/features/teams/team-create/team-create.component.{ts,html}`:
  - `STEPS` gains `'about'` between `'logo'` and `'invite'`;
  - `continueFromLogo()` goes to `'about'`;
  - a `createdTeam` signal is set beside the `createdSlug` latch;
  - signals `aboutText`, `savingAbout`, `aboutFailed`;
  - a `continueFromAbout()` (blank ⇒ `invite`; else `PUT`, success ⇒ `invite`, error ⇒ `aboutFailed` + keep the text; never retried);
  - an `onSubmit` case;
  - the step markup: `h1` + subtitle, the textarea (`maxlength=1000`, counter), the primary button (`skip`/`next`/`saving` labels), and a secondary Skip when `aboutFailed()`.

  Update the class comment's step list.
- [ ] T033 [US4] Add the keys `teams.create.{aboutTitle,aboutSubtitle,aboutPlaceholder,aboutSaving,aboutFailed}` to all three catalogues.
- [ ] T034 [US4] Add an amendment callout to `specs/052-team-creation-wizard/spec.md`: *Amended by 061: a sixth, optional step (the team's description) sits between the logo and invite steps.*

**Checkpoint**: commit `feat(061): the creation wizard asks for a description (#321)`.

---

## Phase 7: Polish & verification

- [ ] T035 Run everything:
  - `dotnet test backend/JuggerHub.slnx`;
  - in `frontend/`: `npx nx test web --watch=false`, `npx nx lint web`, `npx nx build web`.

  Fix any failure. Attribute any failure that T001 already had.
- [ ] T036 Copy `.specify/templates/ui-review-checklist-template.md` to `specs/061-team-details/checklists/ui-review.md` and verify each item against the diff (DESIGN.md wins). Pay particular attention to:
  - one primary per view (Save in settings, Continue in the wizard);
  - `body-md` for the description;
  - underlined links;
  - `h2` levels;
  - 44px targets on the remove and add controls;
  - no emoji or text glyphs as icons.
- [ ] T037 Browser walk (owner rule). Rebuild both images with `docker compose up -d --build backend frontend` and drive quickstart scenarios 1–12 with a Playwright script inside `frontend/`, using one context per actor and `locale: 'de-DE'`, at **375px and desktop**. Screenshot:
  - the Team details section with five link rows and an error;
  - the About card with a long label and host;
  - the wizard step (blank, typed, failed).

  Read the driver's output and **look** at every screenshot. Record the results in `checklists/ui-review.md`. Delete the script afterwards.
- [ ] T038 [P] File the follow-up issue "Player profiles have no links" (owner decision: teams only for now), referencing #359, and link it from the spec's Out of scope.
- [ ] T039 Mark the tasks done, commit `docs(061): UI review and walk results (#359, #321)`, push, and open the PR (`Closes #359`, `Closes #321`) with a summary, the verification run and the screenshots.

---

## Dependencies

- **T002–T011** (Foundational) block every story.
- **US1** (T012–T021) is independent of US2/US3/US4. Within US1, the tests (T012–T014) come before T015–T017, and T013 must fail before T016.
- **US2** and **US3** both extend the settings section (T018) and the About card. Run US2 → US3 in that order to avoid edit conflicts in the same two templates. Their backend tests are independent.
- **US4** depends only on Foundational (T011's `updateDetails`). It can run in parallel with US2/US3.
- **Polish** comes after all stories.

## Parallel opportunities

- Foundational: T002, T003 and T004 (entity, policy, policy tests) in parallel; T011 in parallel with T005–T010.
- US1: T012, T013 and T014 are three test files, in parallel.
- US3: T026 (backend tests) and T027 (util) in parallel.
- US4: T031 in parallel with any US2/US3 work.

## Implementation strategy

MVP = Foundational + **US1**. That fixes #359's core complaint (the frozen name/type/city) and
the two defects-in-waiting. US2 and US3 each add one self-contained capability. US4 is a
convenience. Commit at each checkpoint, so a failing later phase never blocks the earlier value.
