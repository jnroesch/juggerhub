---

description: "Task list for feature 050 — Tournament results"
---

# Tasks: Tournament Results

**Input**: Design documents from `/specs/050-tournament-results/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/results-api.md](./contracts/results-api.md), [contracts/tugeny.md](./contracts/tugeny.md), [quickstart.md](./quickstart.md)

**Tests**: Included. The plan and [quickstart.md](./quickstart.md) name the coverage every area must have, and every feature in this repo ships integration tests. Within each story, tests are written first and must fail before the implementation.

**Organization**: One phase per user story, in spec priority order.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: the user story the task serves (US1–US6)
- Backend paths are under `backend/`. Frontend paths are under `frontend/apps/web/src/app/` unless they start with `frontend/`.

## Ground rules that apply to every task

- **The repository is public.** Specs, code, comments, commits and PR text describe working *with* Tugeny. They never position JuggerHub against any other community platform.
- **Never read turniere.jugger.org** (FR-019). Nothing in this feature learns from it, fetches it, or links it.
- **Terminology**: a **signed-up team** is a team with an `EventSignup` for that event where `Status == Joined` (research R4). It is the only kind of team an event admin may connect a placement to. Every other connection is a platform admin's, made one placement at a time (FR-025).
- **Never write `EventParticipation`** (R3).
- **No test calls the real tugeny.org.** Tugeny is faked with a scripted primary handler serving the committed samples.
- **i18n keys** are added to `frontend/apps/web/public/i18n/en.json`, `de.json` and `es.json` in the **same** task, or `core/i18n/catalog-parity.spec.ts` goes red.
- **Frontend is zoneless.** State the template reads must be signals; a `computed()` over plain fields never recomputes (042 lesson). Keep `.html`/`.css`/`.ts` separate.

---

## Phase 1: Setup

**Purpose**: Fixtures and shared enums.

- [X] T001 Copy the seven files from `specs/050-tournament-results/contracts/tugeny-samples/` into `backend/tests/JuggerHub.Api.IntegrationTests/Results/Fixtures/`, keeping the same file names. Add `<None Include="Results\Fixtures\*.json" CopyToOutputDirectory="PreserveNewest" />` to `backend/tests/JuggerHub.Api.IntegrationTests/JuggerHub.Api.IntegrationTests.csproj`, so tests can read them from `AppContext.BaseDirectory` (the `TemplateParityTests` precedent)
- [X] T002 [P] Create `backend/Entities/ResultEnums.cs` with `ResultSource { None = 0, Manual = 1, TugenyImport = 2 }` and `MatchWinner { First = 0, Second = 1, Draw = 2 }` (data-model.md), each with an XML doc line

---

## Phase 2: Foundational (blocking prerequisites)

**Purpose**: The schema, DTOs and test base every story builds on.

**⚠️ CRITICAL**: No user story starts before this phase is complete.

- [X] T003 [P] Create `backend/Entities/TournamentResult.cs` (`: BaseEntity`) with every field and navigation in data-model.md § TournamentResult, and an XML summary stating the lifecycle: created by the first link, save or import; never deleted; clear keeps the row
- [X] T004 [P] Create `backend/Entities/TournamentPlacement.cs` (`: BaseEntity`) per data-model.md § TournamentPlacement. The XML doc must state the four invariants, and that `TugenyTeamId` is per-commit and never read across results (FR-025)
- [X] T005 [P] Create `backend/Entities/TournamentMatch.cs` (`: BaseEntity`) per data-model.md § TournamentMatch, with `FirstScores`/`SecondScores` as `int[]`
- [X] T006 Configure the three entities in `backend/Data/AppDbContext.cs`: `DbSet`s plus a `// ---- Feature 050: Tournament results ----` block in `OnModelCreating`:
  - **Unique** `EventId`, FK to `Event` with `Cascade`
  - `Placements` and `Matches` with `Cascade`
  - `TournamentPlacement.TeamId`: `SetNull` (comment: the EventParticipation precedent, history survives team delete)
  - `ConnectedByUserId` and `TournamentResult.LastChangedByUserId`: `Restrict` (comment: the award `GrantedByUserId` precedent; survives 037 erasure because the `User` row is never deleted)
  - `FirstPlacementId`/`SecondPlacementId`: `SetNull`
  - Max lengths from data-model.md; enums as ints
  - Indexes: `(TournamentResultId, Position, SortIndex)`, unique `(TournamentResultId, TeamId)` filtered `"TeamId" IS NOT NULL`, `(TeamId)`, `(TugenyTournamentId)`, `(TournamentResultId, SortIndex)` on matches

  (depends on T002–T005)
- [X] T007 Generate the migration with `dotnet ef migrations add AddTournamentResults`, run from `backend/`. Read the generated `Up`/`Down`: it must contain **only** `CreateTable` × 3, `CreateIndex` and `AddForeignKey`, and must **not** touch any existing table. Apply it with `dotnet ef database update` (depends on T006)
- [X] T008 [P] Create `backend/Dtos/Results/ResultDtos.cs` with every event and team DTO and request record in `contracts/results-api.md`:
  - `TournamentResultDto`, `PlacementDto`, `ResultViewerDto`, `TugenyLinkDto`
  - `TournamentMatchDto`, `MatchSideDto`
  - `ResultEditorDto`, `EditorPlacementDto`, `SignedUpTeamDto`
  - `SaveRankingRequest`/`RankingRowRequest` (validation attributes on **constructor parameters** for positional records)
  - `LinkTugenyRequest`, `TugenyImportPreviewDto`, `ImportCommitRequest`/`ImportConnectionRequest`, `TeamPlacementDto`

  Do **not** put an XML doc comment on any `PagedResult<T>` usage type; it crashes the OpenAPI generator (`Common/Pagination.cs` note)
- [X] T009 [P] Create `backend/tests/JuggerHub.Api.IntegrationTests/Results/ResultsTestSupport.cs`:
  - `[CollectionDefinition("Results")] ResultsCollection : ICollectionFixture<JuggerHubApiFactory>`
  - an abstract `ResultsTestSupport : PartyTestSupport` (from `Parties/PartyTestSupport.cs`)
  - `CreateTournamentAsync(client, participantMode)`, which creates a Teams/Individuals `Tournament` via `POST /api/v1/events`
  - `SetEventDatesAsync(eventId, startsAt, endsAt)` and `CancelEventAsync(client, eventId)`
  - `SeedSignupAsync(eventId, teamId, SignupStatus)`, which inserts an `EventSignup` row directly through `AppDbContext`. The signed-up rule reads only that row, so no party dance is needed.
  - `PlatformAdminClientAsync()`, wrapping `Admin/AdminAreaTestSupport.AdminClientAsync`
  - `LoadFixture(name)`, which reads `Results/Fixtures/{name}` from `AppContext.BaseDirectory`
- [X] T010 [P] Create `core/models/results.models.ts` with TypeScript interfaces mirroring every DTO in `contracts/results-api.md`, including `signedUpTeams`, `winner: 'First' | 'Second' | 'Draw'` and `source: 'None' | 'Manual' | 'TugenyImport'`. Reuse the existing `PagedResult<T>` from `core/models/home.models.ts`; don't add a third copy
- [X] T011 [P] Create `core/services/results.service.ts` (`providedIn: 'root'`, `HttpClient`, base `/api/v1/events`) with `getResults(eventId)` and `getMatches(eventId, skip = 0, take = 50)`. Later stories add their own methods

**Checkpoint**: The schema is migrated and the shared contracts compile. Stories can start.

---

## Phase 3: User Story 1 - Record a tournament's final ranking (Priority: P1) 🎯 MVP

**Goal**: An event admin records placements (ties allowed), picking signed-up teams or typing plain names. Everyone who can see the event sees the ranking with the winner called out.

**Independent Test**: [quickstart.md](./quickstart.md) scenario 1. The results card and "1st of 3" on the team page belong to US5, but everything on the event page works without it.

### Tests for User Story 1

- [X] T012 [P] [US1] Write `backend/tests/JuggerHub.Api.IntegrationTests/Results/TournamentRankingTests.cs`: pure xUnit facts for `TournamentRanking.Normalize`:
  - submitted `1,2,3,3,4` → stored `1,2,3,3,5`
  - all tied → all `1`
  - gaps collapse (`1,5,9` → `1,2,3`)
  - order within a tie keeps submission order (`SortIndex`)
  - one row → `1`
- [X] T013 [P] [US1] Write `backend/tests/JuggerHub.Api.IntegrationTests/Results/RankingTests.cs` (`[Collection("Results")]`). It must cover:
  - **FR-001**: 409 when not started, cancelled, or the event is a Workshop
  - 403 for a non-admin, 404 for an unknown event
  - **400** for: an empty name, 81 characters, 129 rows, position 0 or 1000, the same team twice
  - **422** for a `teamId` whose signup is `AwaitingApproval` or `Waitlisted`, or who has none; 200 for `Joined`
  - ties are stored normalised
  - `GET …/results` works for any signed-in user and returns ordered placements, `rankedCount`, `resultsChangedAt`, and `source = Manual`
  - `GET …/results` on an event with nothing recorded returns 200 with `source = None` and no placements
  - `DELETE …/ranking` clears placements, keeps the row, and sets `source = None`
  - an Individuals-mode tournament accepts plain names and has an empty `signedUpTeams`
  - `GET …/editor` gives 403 for non-admins and lists only `Joined` teams in `signedUpTeams`
- [X] T014 [P] [US1] Write `backend/tests/JuggerHub.Api.IntegrationTests/Results/ConnectionPreservationTests.cs` (research R8), after seeding through `AppDbContext` a placement connected by a platform admin to a team with **no** signup:
  - an event admin re-saving the ranking with the same row `id` and the same `teamId` → 200, connection and `ConnectedByUserId` unchanged
  - the same `teamId` on a **different** row → 422
  - the row omitted → deleted
  - the `teamId` changed to a signed-up team → attribution becomes the event admin

### Implementation for User Story 1

- [X] T015 [US1] Create `backend/Services/Results/TournamentRanking.cs`: a pure `static` normaliser to standard competition ranking over `(submittedPosition, submissionIndex)` → `(Position, SortIndex)` (research R8). Make T012 pass
- [X] T016 [US1] Create `backend/Services/Results/ITournamentResultService.cs` and `TournamentResultService.cs`, with outcome enums following `EventService`'s result pattern. Methods:
  - **`GetAsync(eventId, viewerId)`**: an `AsNoTracking` projection. It computes `viewer.canEdit` as event admin ∧ Tournament ∧ ¬Cancelled ∧ `StartsAt <= UtcNow`, and `teamSlug` via the team navigation.
  - **`GetMatchesAsync(eventId, PaginationRequest)`**: ordered by `SortIndex`.
  - **`GetEditorAsync(eventId, userId)`**: includes `signedUpTeams` (`Joined` only), `sourceName`, and connection attribution. The display name uses `MemberPlaceholder.For(culture)` when the profile is gone, following `TeamNewsService.cs:55-66`.
  - **`ReplaceRankingAsync(eventId, userId, rows)`**, implementing research R8 exactly. It uses a guard via `EventAdminGuard.ResolveAsync`, the FR-001 gates, the 128 cap, and the signed-up-or-unchanged-connection rule. It keeps attribution only for unchanged connections, sets `Name = team.Name` when connected (otherwise `SourceName`), and creates the result row on first save. `Source`, `EditedSinceImport`, `ResultsChangedAt` and `LastChangedByUserId` follow the data-model state diagram. It does one tracked load and one `SaveChanges`.
  - **`ClearAsync`**.
- [X] T017 [US1] Create `backend/Controllers/EventResultsController.cs` (`[ApiController]`, `[ApiVersion("1.0")]`, route `api/v{version:apiVersion}/events/{eventId:guid}/results`, class-level `[Authorize]` as `EventsController`). Add `GET ""`, `GET matches`, `GET editor`, `PUT ranking` and `DELETE ranking`, mapping outcomes to the statuses in `contracts/results-api.md` with generic problem details. Register the service in `backend/Program.cs` next to the other `AddScoped` service registrations. Make T013 and T014 pass
- [X] T018 [P] [US1] Create `features/events/event-detail/components/event-results.component.{ts,html,css}` (selector `jh-event-results`, input `eventId`):
  - loads `getResults`
  - renders nothing when there are no placements
  - a winner callout that handles a shared first place ("Shared first place: A and B")
  - the ordered list with ordinals in the mono face (DESIGN.md "Numbers & scores") and team names linking `/t/{slug}`
  - "N teams ranked" and "last changed"
  - failure → `jh-alert tone="danger"` with a "Try again" secondary button, never an empty state (DESIGN.md "Error vs. empty")
  - loading via `jh-loading`
- [X] T019 [US1] Edit `features/events/event-detail/event-detail.component.{html,ts}`:
  - render `<jh-event-results>` first in the main column when `type === 'Tournament'`
  - in the admin manage menu (the `manage-menu-panel` block), add a "Results" link to `['/events', d.id, 'results']` for tournaments only, with `data-testid="manage-results"`
- [X] T020 [US1] Add `getEditor(eventId)`, `saveRanking(eventId, rows)` and `clearRanking(eventId)` to `core/services/results.service.ts`
- [X] T021 [US1] Create the results page `features/events/event-results/event-results.component.{ts,html,css}` and its lazy route `events/:id/results` with `canActivate: [authGuard]` in `app.routes.ts`, next to the other `events/:id/*` routes.

  **Page states**:
  - a back link `‹ {event name}` to the event (DESIGN.md "Navigation: back links": up, never `Location.back()`)
  - a non-admin sees a friendly refusal (the server is the boundary)
  - not started / cancelled / not a tournament → an explanatory `jh-empty-state`

  **Ranking editor** (signals only):
  - rows: a position number input; a team control that is either a select of `signedUpTeams` or a free-text name (max 80)
  - add and remove row
  - rows connected by a platform admin are shown read-only with their team name and "connected by an admin", and are saved back with their `id` (R8)

  **Actions**:
  - **Save ranking** is the single coral primary (DESIGN.md "one coral CTA per view")
  - **Clear results** is secondary, with a confirm step
  - inline `jh-alert` for 400/422 problem details, naming the row
- [X] T022 [P] [US1] Add `events.results.*` keys for the card and editor (heading, winner, sharedFirst, teamsRanked, lastChanged, notStarted, cancelled, notATournament, notAdmin, addRow, removeRow, teamSelect, freeName, connectedByAdmin, save, saved, clear, clearConfirm, loadError, retry, emptyEditor, validation messages) and `events.detail.manageResults` to en/de/es in one task. Keep the voice warm and in sentence case (DESIGN.md "Voice & content")
- [ ] T023 [P] [US1] Write `event-results.component.spec.ts`:
  - winner, shared first, ties render as `1,2,2,4`
  - hidden when empty
  - error shows `jh-alert` and retry, not empty

  Write `event-results/event-results.component.spec.ts`:
  - editor hidden for non-admin
  - only `signedUpTeams` selectable
  - admin-connected rows read-only and saved back with their `id`
  - one primary button

**Checkpoint**: US1 works end to end (quickstart scenario 1, minus the team page).

---

## Phase 4: User Story 2 - Fill in the ranking from Tugeny's export (Priority: P2)

**Goal**: An admin pastes Tugeny's *Export Ranking for JTR* text, gets an unconnected draft, connects signed-up teams, and saves.

**Independent Test**: [quickstart.md](./quickstart.md) scenario 2.

### Tests for User Story 2

- [X] T024 [P] [US2] Write `features/events/event-results/tugeny-export.parser.spec.ts` against `contracts/tugeny.md` §2.

  **Accepted**:
  - compact `[{"name":"A","position":1}]`
  - Qt-style indented with sorted keys
  - `"position":"3"` (string digits)
  - ties
  - unknown keys ignored
  - names trimmed

  **Rejected, each with the one friendly error**:
  - `hello`, `{}`, `[]`, 129 rows
  - a missing `name` or `position`, an empty name, 81 characters
  - position `0` / `-1` / `1.5` / `"x"`
  - duplicate names (case-insensitive, trimmed)

  **Output**: rows sorted by `(position, source order)`, all unconnected

### Implementation for User Story 2

- [X] T025 [US2] Create `features/events/event-results/tugeny-export.parser.ts`: a pure `parseTugenyRankingExport(text): { rows } | { error }` per `contracts/tugeny.md` §2. It does no network and no DOM access. Make T024 pass
- [X] T026 [US2] Add a "Paste from Tugeny" dialog to `features/events/event-results/event-results.component.{ts,html}`:
  - a textarea, and Read → parse
  - on success, replace the editor's draft rows with **unconnected** rows (FR-011: nothing preselected)
  - on failure, show an inline message and leave the draft and the saved ranking untouched (FR-010)
  - show a replace warning when a saved ranking exists before the draft can be saved (FR-012)
  - it saves through the existing `saveRanking` (the server re-validates)
  - the button is secondary
- [X] T027 [P] [US2] Add `events.results.paste.*` keys (open, title, hint pointing to Tugeny's *Export → Export Ranking for JTR*, read, cancel, unreadable, replaceWarning, draftLoaded) to en/de/es in one task
- [ ] T028 [US2] **Manual, owner or anyone with Windows**: in Tugeny 2.4 for Windows, open the bundled `WCC_2020+_finished.tur` and choose *Export → Export Ranking for JTR*. Commit the text as `features/events/event-results/fixtures/tugeny-export.wcc2020.json`, and add a parser spec case asserting it parses. If the real shape differs from research R1, update the parser, `contracts/tugeny.md` §2 and research R1 in the same change

**Checkpoint**: US1 + US2 work. A paste fills the editor, and the save follows US1's rules.

---

## Phase 5: User Story 3 - Hand the team list to Tugeny (Priority: P2)

**Goal**: An admin copies the confirmed team list in a form Tugeny's *Import Team Names* accepts.

**Independent Test**: [quickstart.md](./quickstart.md) scenario 3.

### Tests for User Story 3

- [X] T029 [P] [US3] Write `features/events/event-results/tugeny-team-list.spec.ts`:
  - joined teams only, in `joinedAt` order
  - one name per line, no trailing newline
  - duplicates detected case-insensitively after trimming, and listed
  - rows without `teamName` (individual signups) ignored
  - empty input → empty result

### Implementation for User Story 3

- [X] T030 [US3] Create `features/events/event-results/tugeny-team-list.ts`: a pure `buildTugenyTeamList(signups: Signup[]): { text, duplicates: string[] }` per `contracts/tugeny.md` §3. Make T029 pass
- [X] T031 [US3] Add a "Team list for Tugeny" card to `features/events/event-results/event-results.component.{ts,html}`:
  - it loads **every** page of `EventService.getParticipants(id, 'joined', skip, take)` (`core/services/event.service.ts`)
  - it shows a read-only textarea with the text, and a Copy button using `navigator.clipboard.writeText`. The textarea stays selectable as the fallback where the Clipboard API is blocked.
  - duplicates are listed with a warning that Tugeny refuses them
  - an empty state when no team is confirmed
  - not rendered for Individuals-mode events
  - it is available **before** the start (FR-001 gates only the ranking)
- [X] T032 [P] [US3] Add `events.results.teamList.*` keys (title, hint naming Tugeny's *Import Team Names*, copy, copied, duplicatesWarning, empty) to en/de/es in one task

**Checkpoint**: US1–US3 work without any outbound call.

---

## Phase 6: User Story 4 - Import results from a finalized Tugeny tournament (Priority: P3)

**Goal**: Link an event to its Tugeny tournament, show its live view, and import the ranking plus every match once it's finalized.

**Independent Test**: [quickstart.md](./quickstart.md) scenarios 6–7 (manual, real Tugeny). Automated: T034–T036 against fixtures.

### Tests for User Story 4

- [X] T033 [P] [US4] Write `backend/tests/JuggerHub.Api.IntegrationTests/Results/TugenyLinkParserTests.cs` (pure, research R6).

  **Accepted** (all → `25-deutsche-meisterschaft`):
  - `https://tugeny.org/tournaments/25-deutsche-meisterschaft`
  - the same with `/all-teams`, `/live-view`, `/tournament-tree`
  - `http://` and `www.`
  - a trailing slash
  - the bare slug

  **Rejected**:
  - another host (`evil.example/tournaments/x`)
  - `tugeny.org/api/...`
  - `javascript:`
  - slugs with `_`, `%`, uppercase, a leading or trailing `-`, or longer than 150 characters
  - empty
- [X] T034 [P] [US4] Write `backend/tests/JuggerHub.Api.IntegrationTests/Results/TugenyLinkTests.cs`. Tugeny is faked with `Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddHttpClient("Tugeny").ConfigurePrimaryHttpMessageHandler(() => scripted)))`, where `scripted` serves the fixtures by path. It must cover:
  - link OK → stores id, slug, name and date; the response carries them
  - `tournament-by-slug.unknown.json` (`null`) → 404
  - 500 / timeout → 503, nothing stored
  - a bad address → 400 with **no** transport call
  - linking before the start is allowed; cancelled → 409
  - the same Tugeny id already linked on another event → `linkedElsewhere: true`, and saved anyway
  - `DELETE` link → the four `Tugeny*` columns are null and results untouched
  - 403 for a non-admin
- [X] T035 [P] [US4] Write `backend/tests/JuggerHub.Api.IntegrationTests/Results/TugenyImportTests.cs`.

  **Preview** (finalized fixtures): 20 placements, `matchCount` 78, nothing saved, every row unconnected.

  **Not finalized**: `rankings.not-finalized.json` → 422 with the `not-finalized` type.

  **Commit**:
  - makes 2 transport calls (it re-fetches)
  - stores 20 placements and 78 matches
  - 9 matches have `Winner = Draw`
  - stages are `Group 1..5` plus null
  - `SortIndex` follows `timestamp`
  - `"5:1 - 2:5 - 0:5"` is parsed to `[5,2,0]` / `[1,5,5]`
  - connections to signed-up teams are applied with attribution
  - a connection to a team without a `Joined` signup → 422
  - two Tugeny teams → one team → 422
  - `source = TugenyImport`, with `importedAt` set

  **Failure cases**:
  - a 503 on commit leaves the previous ranking byte-for-byte intact (SC-006)
  - a 4 MiB + 1 byte body → 503 after **exactly 1** transport call (not retried, research R5)
  - a malformed JSON body → 503
  - a later hand save → `editedSinceImport = true`

  **Also**: `GET matches?take=50` pages.
- [X] T036 [P] [US4] Write `backend/tests/JuggerHub.Api.IntegrationTests/Resilience/TugenyResilienceTests.cs`. Mirror `CircuitBreakerTests.cs`, `OutboundEmailResilienceTests.cs` and `MediaStoreTests.cs:143-161`, using a harness that registers the named `"Tugeny"` client with `AddJuggerHubResilience(configuration, "Tugeny")` and a `ScriptedHandler` that can return a JSON body. It must cover:
  - `client.Timeout == Timeout.InfiniteTimeSpan`, and the base address comes from `Tugeny:BaseUrl`
  - 500/502/503/504/408/429 are retried (3 transport calls); 400/404 are not (1)
  - with R5's values the breaker opens at 4 failed attempts
  - `TugenyResponseTooLargeException` is not retried
  - no log line contains a response body

### Implementation for User Story 4

- [X] T037 [P] [US4] Create `backend/Common/TugenyOptions.cs` (`SectionName = "Tugeny"`, `BaseUrl = "https://tugeny.org/"`, `const ResilienceName = "Tugeny"`, `MaxResponseBytes = 4 * 1024 * 1024`). Add a `Normalize()` that falls back to the safe defaults, never to "unlimited" (Principle VII)
- [X] T038 [P] [US4] Create `backend/Resilience/ResponseSizeLimitHandler.cs` (`DelegatingHandler`, constructor `maxBytes`) and `TugenyResponseTooLargeException : Exception`, **not** `HttpRequestException`. It:
  - rejects when `Content-Length > maxBytes`
  - otherwise reads at most `maxBytes + 1` bytes of the body inside `SendAsync`, so within the attempt timeout
  - throws on overflow
  - swaps in a `ByteArrayContent` carrying the original content headers

  The XML doc explains why it exists: `MaxResponseContentBufferSize` throws `HttpRequestException`, which the shared retry treats as transient (research R5)
- [X] T039 [US4] Wire the client in `backend/Program.cs`, next to the `MediaStore` named client:
  - bind `TugenyOptions`
  - `builder.Services.AddHttpClient(TugenyOptions.ResilienceName, (sp, c) => c.BaseAddress = new Uri(options.BaseUrl)).AddJuggerHubResilience(builder.Configuration, TugenyOptions.ResilienceName).AddHttpMessageHandler(() => new ResponseSizeLimitHandler(options.MaxResponseBytes))`

  Add a comment stating: GET-only, so idempotent; Tugeny's 429 is retried (the provider throttling us), while our own `tugeny` policy's 429 is never retried by the browser. Register `ITugenyClient` and `ITugenyImportService` as scoped (depends on T037, T038)
- [X] T040 [P] [US4] Add configuration parity (Principle V) with the R5 values in `backend/appsettings.json`, `.env.sample`, `docker-compose.yml` and `JuggerHubApiFactory.cs`:
  - `backend/appsettings.json`: `"Tugeny": { "BaseUrl": "https://tugeny.org/" }` and `Resilience:Outbound:Tugeny` (all 8 keys)
  - `.env.sample`: a `RESILIENCE_TUGENY_*` block (the same 4 keys as Resend/MediaStore) and `TUGENY_BASE_URL`
  - `docker-compose.yml`: `Resilience__Outbound__Tugeny__*=${RESILIENCE_TUGENY_*:-default}` and `Tugeny__BaseUrl=${TUGENY_BASE_URL:-https://tugeny.org/}`
  - `backend/tests/JuggerHub.Api.IntegrationTests/JuggerHubApiFactory.cs`: fast test values next to the MediaStore ones
- [X] T041 [US4] Create `backend/Services/Results/TugenyLinkParser.cs`: a pure `static bool TryParseSlug(string input, out string slug)` per research R6. Make T033 pass
- [X] T042 [US4] Create `backend/Services/Results/ITugenyClient.cs` and `TugenyClient.cs`, the only code that talks to Tugeny. It uses `IHttpClientFactory.CreateClient(TugenyOptions.ResilienceName)`, and every slug or id is path-encoded. Methods:
  - `GetTournamentBySlugAsync`: a literal `null` body → NotFound
  - `GetRankingAsync`: `rankings` as an object `"1".."N"`, or `[]` → not finalized
  - `GetMatchesAsync`

  Parse with `JsonDocument`, tolerant per `contracts/tugeny.md` §1:
  - unknown keys are ignored
  - a missing required field → Unusable
  - an unparseable `score_total` → empty arrays

  **Errors**: exceptions, non-success status and the size exception become a typed `Unavailable`/`Unusable` result. Log the endpoint name, status and length at Warning, **never** the body
- [X] T043 [US4] Create `backend/Services/Results/ITugenyImportService.cs` and `TugenyImportService.cs`. Every write gets the same guard and the FR-001 gates as `TournamentResultService`; linking before the start is allowed. Methods:
  - **`LinkAsync`**: parse, fetch, store the link, compute `linkedElsewhere`.
  - **`UnlinkAsync`**.
  - **`PreviewAsync`**: returns `signedUpTeams` and `replacesExisting`.
  - **`CommitAsync`** (research R7):
    - re-fetch
    - validate each connection's `teamId` is a signed-up team, and that no two connections target one team
    - build placements (Position from the key, `SourceName`/`Name` from `team_name`, `TugenyTeamId`)
    - build matches: sort by `(timestamp, response index)` → `SortIndex`; sides resolved through `TugenyTeamId` to the new placements; `Winner` from `victorious_team_id`, null → Draw
    - replace the old placements and matches in **one** `SaveChanges`
    - set `Source = TugenyImport`, `ImportedAt`, `EditedSinceImport = false`, `ResultsChangedAt`, `LastChangedByUserId`
- [X] T044 [US4] Add a `Tugeny = "tugeny"` policy (10 per minute, `PartitionByUser`) to `backend/Security/RateLimitPolicies.cs`. Its comment names the two meanings of 429 (constitution Principle VII)
- [X] T045 [US4] Add `PUT tugeny-link`, `DELETE tugeny-link`, `GET tugeny-import` and `POST tugeny-import` to `backend/Controllers/EventResultsController.cs`:
  - `[EnableRateLimiting(RateLimitPolicies.Tugeny)]` on the three that cause outbound calls
  - statuses per `contracts/results-api.md`
  - 503 with the problem detail "Tugeny can't be reached right now — try again in a few minutes."
  - a 422 problem `type` ending in `not-finalized`

  Make T034 and T035 pass
- [X] T046 [US4] Add `linkTugeny`, `unlinkTugeny`, `previewTugenyImport` and `commitTugenyImport` to `core/services/results.service.ts`
- [X] T047 [US4] Add a "Tugeny" card to `features/events/event-results/event-results.component.{ts,html}`.

  **Linking**:
  - an address input and Link (secondary)
  - once linked: the Tugeny name and start date for confirmation, a `linkedElsewhere` warning, and Remove link

  **Import results** → a preview dialog:
  - counts
  - per Tugeny team, a select of `signedUpTeams`, **empty by default** (FR-011)
  - a replace warning
  - a confirm step, then commit

  **Errors**:
  - not finalized → a message pointing to paste and hand entry
  - 503 → an inline `jh-alert` with retry
  - 429 from our own limiter → a "please wait a moment" message, never retried
- [ ] T048 [US4] Extend `features/events/event-detail/components/event-results.component.{ts,html,css}` with:
  - **Matches**: grouped by `stage` in order, with null under one "Knockout" heading
  - each row shows both sides (linked when connected), scores in the mono face as `5 : 3` per set, and the winner emphasised; draws labelled
  - load more through `getMatches`, 50 per page (the `news-page.component.ts` pattern)
  - **Provenance**: "Results from Tugeny · {date}" linking `tugeny.tournamentUrl`, plus "edited since" when `editedSinceImport`
  - **Live link**: in `event-detail.component.html`, a "Live on Tugeny" secondary link to `tugeny.liveUrl` while `endsAt` is in the future and a link exists
- [X] T049 [P] [US4] Add `events.results.tugeny.*` and `events.results.matches.*` keys (link, address, linked, linkedElsewhere, removeLink, import, preview, connectPrompt, replaceWarning, notFinalized, unreachable, slowDown, provenance, editedSince, liveView, knockout, draw, loadMore) to en/de/es in one task
- [ ] T050 [P] [US4] Write component specs in the two `event-results.component.spec.ts` files:
  - in `features/events/event-results/event-results.component.spec.ts`: Tugeny card states (unlinked, linked, linked elsewhere, not finalized, unreachable), and the import preview has **no** preselected team
  - in `features/events/event-detail/components/event-results.component.spec.ts`: matches group by stage with Knockout last, a draw shows the draw label, and the provenance line appears for imports

**Checkpoint**: US1–US4 work. The only outbound dependency is live, resilient and bounded.

---

## Phase 7: User Story 5 - A team's placement history (Priority: P3)

**Goal**: The team page lists the tournaments the team placed in, newest first, paged.

**Independent Test**: [quickstart.md](./quickstart.md) scenario 1, step 4 (the team page), and scenario 4, step 3.

### Tests for User Story 5

- [X] T051 [P] [US5] Write `backend/tests/JuggerHub.Api.IntegrationTests/Results/TeamPlacementHistoryTests.cs`:
  - only connected placements
  - newest tournament first
  - `rankedCount` counts every placement in that ranking
  - `skip`/`take` paging with `totalCount`
  - visible to any signed-in user; 404 for an unknown slug; 401 signed out
  - a disconnected placement disappears from history
  - deleting the team via `DELETE /api/v1/teams/{slug}` leaves the placement on the event with the team's name, unlinked (`SetNull`)

### Implementation for User Story 5

- [X] T052 [US5] Create `backend/Services/Results/ITeamPlacementService.cs` and `TeamPlacementService.cs`: `GetForTeamAsync(slug, PaginationRequest)`. It is an `AsNoTracking` projection, `CountAsync`, then order by `Event.StartsAt` descending then `EventId`, then `Skip`/`Take`, returning `PagedResult<TeamPlacementDto>` (`TeamActivityService.GetForTeamAsync` pattern). Register it in `Program.cs`
- [X] T053 [US5] Add `GET {slug}/placements` to `backend/Controllers/TeamsController.cs` (`[FromQuery] PaginationRequest`, 404 for an unknown team). Make T051 pass
- [ ] T054 [US5] Add `getTeamPlacements(slug, skip, take)` to `core/services/results.service.ts`, and create `features/teams/team-detail/placements/team-placements.component.{ts,html,css}` (selector `jh-team-placements`), rendered in `features/teams/team-detail/team-detail.component.html` right **after** the "Recent events" `data-testid="activity"` card. It shows:
  - `<jh-card data-testid="placements">`
  - rows with the event name linking `/events/{id}`, the date via `translocoDate`, and "{position}. of {rankedCount}" with numbers in the mono face
  - load more (the `news-page.component.ts` pattern)
  - `jh-empty-state` (inline) when empty; `jh-alert` plus retry on error
- [ ] T055 [P] [US5] Add `teams.placements.*` keys (title, place with `{{position}}`/`{{count}}` params, empty, loadMore, loadError) to en/de/es in one task
- [ ] T056 [P] [US5] Write `features/teams/team-detail/placements/team-placements.component.spec.ts`: rows, empty vs error, load more appends

**Checkpoint**: Recorded results show on team pages.

---

## Phase 8: User Story 6 - Connect results to JuggerHub teams (Priority: P3), plus past-dated tournaments (FR-023)

**Goal**: Platform admins work through unconnected placements and connect or disconnect them one at a time. Past-dated tournaments stop offering sign-up actions that the server refuses.

**Independent Test**: [quickstart.md](./quickstart.md) scenario 4, plus scenario 5's admin checks.

### Tests for User Story 6

- [X] T057 [P] [US6] Write `backend/tests/JuggerHub.Api.IntegrationTests/Results/AdminPlacementTests.cs`:
  - **Access**: 403 for a normal user and for a team admin
  - **Queue**: defaults to unconnected, newest tournament first, `q` matches `sourceName` and `name` accent-insensitively, `connected=true` lists connected rows
  - **Connect**: sets `TeamId`, `Name = team name`, `ConnectedByUserId` and `ConnectedAt`; **exactly one row changes** even when another event has a placement with the same `sourceName` (SC-007, FR-025)
  - **Errors**: 409 when the team is already placed in that ranking; 404 for an unknown placement or team
  - **Disconnect**: restores `Name = sourceName` and clears attribution; repeating it is idempotent
  - **Erased connector**: after erasing the connecting admin's account (`AccountDeletion/AccountDeletionTestSupport.cs`), `connectedBy` shows the placeholder
- [X] T058 [P] [US6] Add a test in `backend/tests/JuggerHub.Api.IntegrationTests/Parties/` to the party-context tests: on an event whose `EndsAt` has passed, `GET /api/v1/events/{id}/party-context` reports `canForm: false` for a team admin (research R12)

### Implementation for User Story 6

- [X] T059 [P] [US6] Create `backend/Dtos/Admin/AdminResultDtos.cs` with `AdminPlacementDto` and `ConnectTeamRequest(string TeamSlug)` per `contracts/results-api.md`
- [X] T060 [US6] Create `backend/Services/Admin/IAdminPlacementService.cs` and `AdminPlacementService.cs`, and register them in `Program.cs`. Methods:
  - **`ListAsync(connected, q, PaginationRequest)`**, following the `AdminTeamService.SearchAsync` pattern: `SearchQuery.Normalize`, `ILike` + `AppDbContext.Unaccent`, count, then order by `Event.StartsAt` descending then `Position`, paged
  - **`ConnectAsync(placementId, teamSlug, actorId)`**: one tracked row; the FR-005 check within the same result; sets `Name`, `ConnectedByUserId`, `ConnectedAt` and the result's `ResultsChangedAt`
  - **`DisconnectAsync(placementId)`**
- [X] T061 [US6] Create `backend/Controllers/Admin/AdminResultsController.cs`: route `api/v{version:apiVersion}/admin/results`, `[Authorize(Policy = PlatformAdminPolicy.Name)]`, derived from `AdminControllerBase`, with `GET placements`, `PUT placements/{id:guid}/team` and `DELETE placements/{id:guid}/team`. Make T057 pass
- [X] T062 [US6] In `backend/Services/Parties/PartyService.cs`, change the party-context `CanForm` (currently `t.IsAdmin && t.Party is null`) to also require that the event is open, using the same rule as `PartyAccess.IsEventOpen`. Make T058 pass
- [ ] T063 [US6] In `features/events/event-detail/components/join-actions.component.ts` (and its `.html`), hide the join and enter-party actions once `endsAt` has passed. The server stays the boundary. Update `join-actions.component.spec.ts` with an ended-event case
- [ ] T064 [US6] Add `listPlacements(connected, q, skip, take)`, `connectPlacement(id, teamSlug)` and `disconnectPlacement(id)` to `core/services/admin.service.ts`
- [ ] T065 [US6] Create `features/admin/shared/team-picker.component.{ts,html,css}` (selector `jh-admin-team-picker`, output `picked: {slug, name}`, `closed`). Build it from `features/admin/teams/admin-teams.component.ts` (250 ms debounced `AdminService.searchTeams`) and the modal behaviour of `features/admin/shared/assign-picker.component.ts` (Esc closes, errors via `problemDetail`)
- [ ] T066 [US6] Create `features/admin/results/admin-results.component.{ts,html,css}`:
  - a work-queue list (unconnected by default, with a connected toggle and search)
  - each row shows the event name and date, the position of `rankedCount`, `sourceName`, the connected team, and connected-by/at
  - Connect opens `jh-admin-team-picker`; Disconnect has a confirm step
  - load more
  - the responsive list follows the existing admin pages (e2e memory: desktop and mobile variants must not duplicate testids in a way that breaks strict mode)
- [ ] T067 [US6] Add the admin navigation in `app.routes.ts` and `features/admin/shell/admin-shell.component.{ts,html}`:
  - an `admin` child route `results` in `app.routes.ts`
  - a `resultsActive` computed in `features/admin/shell/admin-shell.component.ts`
  - a "Results" link in **both** the desktop sidebar and the `data-testid="admin-bottom-nav"` bar in `admin-shell.component.html`, with `data-testid="admin-nav-results"`
  - fix the stale doc comment in the shell's `.ts`

  If five tabs do not fit at 375 px in German, DESIGN.md wins: move the queue under the Teams section as a sub-view instead, and record the decision in `checklists/ui-review.md` (research R15)
- [ ] T068 [P] [US6] Add `admin.nav.results` and `admin.results.*` keys (title, intro, unconnectedOnly, showConnected, search, sourceName, connectedTo, connectedBy, connect, disconnect, disconnectConfirm, pickTeam, alreadyPlaced, empty, loadMore, loadError) to en/de/es in one task
- [ ] T069 [P] [US6] Write `features/admin/results/admin-results.component.spec.ts` (queue rows; connect calls the service once with one placement id; disconnect confirms) and `features/admin/shared/team-picker.component.spec.ts` (debounce, pick, Esc)

**Checkpoint**: All six stories are functional.

---

## Phase 9: Polish & Cross-Cutting Concerns

- [ ] T070 [P] In `infra/modules/app/network-policy.tf`, extend the backend egress comment ("The backend calls Resend and Azure Blob by hostname") with tugeny.org. This is a comment only; no rule changes
- [ ] T071 Run the full verification (`backend/tests/JuggerHub.Api.IntegrationTests`, `frontend/`) and fix anything red:
  - `dotnet test backend/tests/JuggerHub.Api.IntegrationTests`, including `ApiDocsTests` and every `Results`, `Resilience`, `Parties` and `Events` test
  - in `frontend/`: `npx nx test web --watch=false`, including `catalog-parity.spec.ts`
  - `npm run lint`
  - `npm run build`

  If `node_modules` looks stale, run `npm ci` first (memory: stale node_modules faked a broken main)
- [ ] T072 Gate 7: copy `.specify/templates/ui-review-checklist-template.md` to `specs/050-tournament-results/checklists/ui-review.md` and verify every item against the diff (DESIGN.md wins). Include research R15: one coral CTA per view, mono scores and ordinals, error vs empty, sentence case, touch targets ≥ 44 px, the fifth admin tab
- [ ] T073 Browser walk (owner rule). Use a real browser, **German**, at **375 px and desktop**, and take the screenshots listed in [quickstart.md](./quickstart.md) § Browser walk. Read the driver's output; a shared Playwright context and a stale backend image have each produced a false pass before
- [ ] T074 Run [quickstart.md](./quickstart.md) manual scenarios 1–7. Scenario 6 needs internet access to tugeny.org and uses `25-deutsche-meisterschaft` and `26-deutsche-meisterschaft`. Record the results and any failures in `checklists/ui-review.md` or the PR description
- [ ] T075 Post a progress comment on GH #295 with a `--body-file`, never an inline PowerShell here-string (memory). List the spec drift recorded in `plan.md` § Spec drift, and whether T028 (the real export fixture) is still open

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)** → **Foundational (Phase 2)** → user stories.
- **US1 (Phase 3)** is the base the other event-side stories extend. It creates `TournamentResultService`, `EventResultsController`, the results page and the results card.
- **US2 and US3** each need only US1's results page (T021). They can run in parallel with each other.
- **US4** extends US1's controller, results page and card. It is independent of US2 and US3.
- **US5** needs only Foundational plus data. It can be built right after Phase 2, in parallel with US1, but it can only be demonstrated once US1, US4 or US6 has produced connected placements.
- **US6** needs only Foundational. Its admin half can run in parallel with US1–US5. T062 and T063 (FR-023) are independent of everything else in this feature.
- **Polish** comes after all the stories wanted in this release.

### Within each story

Tests first (they must fail), then pure helpers, services, controller, frontend service, components, and i18n. The i18n task can run in parallel with component work but must land in the same commit as the templates that use the keys.

### Parallel opportunities

- Phase 2: T003–T005, T008, T009, T010 and T011 are all different files.
- US1: T012, T013 and T014 (tests), and later T018, T022 and T023.
- US4: T033–T036 (tests), T037 and T038, T040, T049, T050.
- US6: T057, T058, T059, T068 and T069. The backend (T060–T062) and frontend (T063–T067) halves run in parallel.
- Across stories: once US1 lands, US2, US3, US4 and US6's frontend can proceed in parallel. They touch different sections of `event-results.component.*`, so merge them carefully.

---

## Parallel Example: User Story 4

```text
# Tests together:
Task: "T033 TugenyLinkParserTests.cs (pure)"
Task: "T034 TugenyLinkTests.cs (scripted handler)"
Task: "T035 TugenyImportTests.cs (fixtures)"
Task: "T036 Resilience/TugenyResilienceTests.cs"

# Infrastructure together:
Task: "T037 Common/TugenyOptions.cs"
Task: "T038 Resilience/ResponseSizeLimitHandler.cs"
Task: "T040 appsettings.json / .env.sample / docker-compose.yml / JuggerHubApiFactory.cs"
```

---

## Implementation Strategy

### MVP (US1 only)

Phases 1 → 2 → 3. Hand entry of a ranking on any tournament that has started, shown on the event page. It needs no outbound call and no admin tooling. It is useful on its own for every tournament run in JuggerHub, and it is the path every other story either feeds or reads.

### Incremental delivery

1. **MVP**: US1.
2. **US2 + US3** (the Tugeny hand-offs, still with no outbound call).
3. **US4** (the import, and the first outbound integration since 028/035).
4. **US5 + US6** (history on team pages, plus the platform-admin queue that makes past tournaments count).

Each step is releasable. Commit per task or logical group, with messages referencing `#295`.

---

## Notes

- **Suggested commit groups**: Phase 1+2 (schema), then each story.
- **Stop at every checkpoint** and run that story's tests.
- **If a task produces a change to an existing table in the migration, stop.** The plan adds three tables and touches nothing else.
- **T028 is the only task an agent cannot do alone** (it needs the Tugeny Windows GUI). Everything else is automatable.
