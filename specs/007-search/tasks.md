---
description: "Task list for feature 007 — Search / Browse"
---

# Tasks: Search / Browse

**Input**: Design documents from `/specs/007-search/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/openapi.yaml, quickstart.md

**Tests**: Included — the plan's Testing section explicitly requests backend integration tests (xUnit + Testcontainers) and frontend Jest unit + Playwright e2e.

**Organization**: Grouped by user story. US1 (Teams) and US2 (Events) are P1; US3 (Players) and US4 (shell/states) are P2. The shared browse shell + data-model changes are foundational (all stories depend on them).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1=Teams, US2=Events, US3=Players, US4=Shell/states
- Paths are repo-relative: backend `backend/`, frontend `frontend/apps/web/src/app/`, e2e `frontend/apps/web-e2e/src/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Folders, config, and scaffolding shared by all stories

- [X] T001 [P] Create backend folders `backend/Services/Search/` and `backend/Dtos/Search/` (empty, ready for slice files)
- [X] T002 [P] Add `backend/Common/SearchOptions.cs` (`ActiveTeamWindowMonths = 12`, `MinQueryLength = 1`; safe defaults) and bind it in `backend/Program.cs`
- [X] T003 [P] Create frontend folder `frontend/apps/web/src/app/features/browse/` and add `core/models/search.models.ts` (card DTO interfaces, browse-query params, sort/filter types, reuse of the existing Pompfe position catalog)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Data-model changes, shared backend search helper + seeding, and the shared frontend browse shell — everything all three pages need

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Backend data model

- [X] T004 Add `BeginnersWelcome` (bool, default false) to `backend/Entities/Team.cs` and `AppearInSearch` (bool, default false) to `backend/Entities/PlayerProfile.cs`
- [X] T005 Configure the two new columns (DB defaults) and add indexes in `backend/Data/AppDbContext.cs`: partial `PlayerProfiles(AppearInSearch) WHERE AppearInSearch`, `Events(StartsAt)`, `Events(Status)`, and `EventParticipations(TeamId)` if not already present
- [X] T006 Create EF migration `AddDiscoveryFields` in `backend/Data/Migrations/` — adds both columns, `CREATE EXTENSION IF NOT EXISTS unaccent`, and the indexes; verify it auto-applies on startup
- [X] T007 [P] Add shared `backend/Services/Search/SearchQuery.cs` — query normalization (trim, whitespace-only ⇒ none, min-length), `EF.Functions.Unaccent`/`ILike` substring helper, and the sort enums (`TeamSort`, `EventSort`, `PlayerSort`) each applying a `.ThenBy(x => x.Id)` tiebreaker

### Backend seeding

- [X] T008 Extend `backend/Data/DevDataSeeder.cs` (Dev/local only) — active vs dormant teams (recent/old/no `EventParticipation`), some `BeginnersWelcome`, several cities incl. a Mixteam; past/upcoming/cancelled events of mixed type & city; opted-in vs not-opted-in players with varied names/hometowns (incl. accented) and pompfen; enough rows to exceed one page

### Frontend shared shell

- [X] T009 [P] Add `core/services/search.service.ts` — `browseTeams`/`browseEvents`/`browsePlayers` returning `PagedResult<T>` (signals), plus a `take=0` count helper for the live "Show N"
- [X] T010 [P] Build `features/browse/browse-shell/` (`.ts/.html/.css`) — shared shell: header + "Browse" caption, search input (RxJS ~250 ms debounce), Filters button + active-count badge, Sort control, active-filter chip row (+ "Clear all"), result-count line, results list with infinite scroll, and a host slot for the filter panel + a per-entity row `ng-template`; first-page reset on query/filter change. Styled per DESIGN.md
- [X] T011 [P] Build `features/browse/filter-panel/` (`.ts/.html/.css`) — responsive bottom-sheet (mobile) / slide-over drawer (desktop) with Reset + primary "Show N" action bound to the live count, and the locked non-interactive "Near me — coming soon" item
- [X] T012 Add browse routes in `frontend/apps/web/src/app/app.routes.ts` — in-shell, **no** authGuard, lazy-loaded: `browse` (redirect → `browse/teams`), `browse/teams`, `browse/events`, `browse/players`
- [X] T013 Add a "Browse" entry point (Teams/Events/Players) to the shell navigation in `frontend/apps/web/src/app/layout/`

**Checkpoint**: Data model migrated, shared search helper + seeder ready, shared shell + routes + nav in place — story pages can now be built

---

## Phase 3: User Story 1 — Browse & search Teams (Priority: P1) 🎯 MVP

**Goal**: A fully usable Teams browse page (default list, live name/city search, active + beginners + city filters, chips, links to team pages) plus the admin-managed Beginners-welcome flag it filters on

**Independent Test**: Load `/browse/teams`; default list shows without typing (active-only), `q=koln` narrows live and accent-insensitively, the filter sheet/drawer toggles active/beginners/city with a live count and chips, and a row links to `/t/:slug`

### Tests for User Story 1 ⚠️

- [X] T014 [P] [US1] Backend integration tests `backend/tests/JuggerHub.Api.IntegrationTests/Search/TeamBrowseTests.cs` — active derivation (12-mo boundary), `activeOnly=false` includes dormant, beginners filter, city filter, accent/case-insensitive name+city search, pagination stability, anonymous access
- [X] T015 [P] [US1] Backend integration tests `.../Search/TeamSettingsTests.cs` — `PATCH /teams/{slug}` sets BeginnersWelcome as admin (204), non-admin 403, unknown slug 404
- [X] T016 [P] [US1] Playwright `frontend/apps/web-e2e/src/browse.spec.ts` (Teams section) — defaults visible, live search, filter sheet(mobile)/drawer(desktop), chips + count, row navigation

### Backend implementation for US1

- [X] T017 [P] [US1] Add `backend/Dtos/Search/TeamCardDto.cs` (slug, name, city?, playerCount, beginnersWelcome, logoInitial) and `backend/Dtos/Search/TeamBrowseQuery.cs` (q?, activeOnly=true, beginnersWelcome?, city?, sort, + PaginationRequest)
- [X] T018 [US1] Add `backend/Services/Search/ITeamSearchService.cs` + `TeamSearchService.cs` — projected `AsNoTracking` query: name/city `unaccent` search, active `EXISTS` over `EventParticipations`+`Events.StartsAt`, beginners + city filters, A–Z sort with Id tiebreaker, `PagedResult<TeamCardDto>`
- [X] T019 [US1] Add `[HttpGet] ""` (anonymous) browse action to `backend/Controllers/TeamsController.cs` binding `TeamBrowseQuery`; register `ITeamSearchService` in `backend/Program.cs`
- [X] T020 [P] [US1] Add `backend/Dtos/Teams/UpdateTeamSettingsRequest.cs` ({ bool beginnersWelcome })
- [X] T021 [US1] Add `UpdateSettingsAsync(slug, beginnersWelcome)` to `ITeamService`/`TeamService` (team-admin guard, tracked save) and `[HttpPatch("{slug}")]` (auth) to `TeamsController` mapping non-admin→403, unknown→404

### Frontend implementation for US1

- [X] T022 [US1] Build `features/browse/browse-teams/` (`.ts/.html/.css`) — Teams config (labels, filter schema: active/beginners/city + locked near-me, sort A–Z, fetch via `SearchService.browseTeams`) and the team row template (logo initial, name, city, player count, Beginners chip) rendered through `browse-shell`
- [X] T023 [US1] Wire a "Beginners welcome" toggle into `features/teams/team-settings/` (`.ts/.html`) calling `PATCH /teams/{slug}` (admin-only UI, server-enforced)

**Checkpoint**: Teams browse is fully functional and testable end-to-end (MVP)

---

## Phase 4: User Story 2 — Browse & search Events (Priority: P1)

**Goal**: Events browse page — upcoming-only by default (soonest first), cancelled always excluded, live name search, date-range + type + city filters, links to `/events/:id`

**Independent Test**: Load `/browse/events`; only non-ended, non-cancelled events show soonest-first; turning off "Hide past" reveals past (still no cancelled); date range + type narrow with chips; a row opens the event page

### Tests for User Story 2 ⚠️

- [X] T024 [P] [US2] Backend integration tests `.../Search/EventBrowseTests.cs` — hide-past default, cancelled always excluded (both toggle states), date-range, type, city, name accent search, soonest-first sort with tiebreaker, pagination, anonymous
- [X] T025 [P] [US2] Playwright `browse.spec.ts` (Events section) — defaults hide past, filters (hide-past toggle, date range, type) + chips, row navigation

### Backend implementation for US2

- [X] T026 [P] [US2] Add `backend/Dtos/Search/EventCardDto.cs` (id, name, type, customTypeLabel?, startsAt, endsAt, locationKind, city?, locationLabel) and `EventBrowseQuery.cs` (q?, hidePast=true, from?, to?, type?, city?, sort, + PaginationRequest)
- [X] T027 [US2] Add `backend/Services/Search/IEventSearchService.cs` + `EventSearchService.cs` — projected `AsNoTracking`: exclude `Status==Cancelled` always, hide-past (EndsAt<now) default, date-range on StartsAt, type + city filters, name `unaccent` search, StartsAt-asc sort with Id tiebreaker, `PagedResult<EventCardDto>`
- [X] T028 [US2] Add `[HttpGet] ""` (anonymous) browse action to `backend/Controllers/EventsController.cs` binding `EventBrowseQuery`; register `IEventSearchService` in `backend/Program.cs`

### Frontend implementation for US2

- [X] T029 [US2] Build `features/browse/browse-events/` (`.ts/.html/.css`) — Events config (filter schema: hide-past/date-range/type/city + locked near-me, sort soonest, fetch via `SearchService.browseEvents`) and the event row template (name, date, city/location, type)

**Checkpoint**: Teams AND Events browse both work independently

---

## Phase 5: User Story 3 — Browse & search Players, opt-in only (Priority: P2)

**Goal**: Players browse page gated by the opt-in privacy invariant — only `AppearInSearch=true` players ever appear; live name search, position (from pompfen) + city filters; the opt-in note; plus the self-service "Appear in search" profile toggle

**Independent Test**: Load `/browse/players`; only opted-in players show with the note; a non-opted-in player is unreachable by any query/filter/sort/auth; toggling opt-in on/off adds/removes them; position filter narrows by declared pompfen; a row opens `/u/:handle`

### Tests for User Story 3 ⚠️

- [X] T030 [P] [US3] Backend integration test `.../Search/PlayerOptInInvariantTests.cs` — **the security test**: a `AppearInSearch=false` player is absent across no-query, exact-name query, every position/city/sort combination, and both anonymous + authenticated callers
- [X] T031 [P] [US3] Backend integration tests `.../Search/PlayerBrowseTests.cs` — position filter (ANY-of over pompfen), city filter, accent/case name search, pagination, anonymous; and `PUT /profiles/me` persists `appearInSearch` (owner-only)
- [X] T032 [P] [US3] Playwright `browse.spec.ts` (Players section) — only opted-in listed, opt-in note visible, position filter + chips, row navigation; profile toggle flips visibility

### Backend implementation for US3

- [X] T033 [P] [US3] Add `backend/Dtos/Search/PlayerCardDto.cs` (handle, displayName, hometown?, positions[], hasAvatar) and `PlayerBrowseQuery.cs` (q?, positions[]?, city?, sort, + PaginationRequest)
- [X] T034 [US3] Add `backend/Services/Search/IPlayerSearchService.cs` + `PlayerSearchService.cs` — **opt-in `WHERE AppearInSearch` applied first, unconditionally**; positions derived from `ProfilePompfe` (ANY-of), city on Hometown, name `unaccent` search, A–Z sort with Id tiebreaker, `PagedResult<PlayerCardDto>`
- [X] T035 [US3] Add `[HttpGet] ""` (anonymous) browse action to `backend/Controllers/ProfilesController.cs` binding `PlayerBrowseQuery`; register `IPlayerSearchService` in `backend/Program.cs`
- [X] T036 [US3] Extend `backend/Dtos/Profile/UpdateProfileRequest.cs` + the `OwnerProfileDto` with `AppearInSearch`, and map it in `ProfileService.UpdateMine` (existing `PUT /profiles/me`, owner-only)

### Frontend implementation for US3

- [X] T037 [US3] Build `features/browse/browse-players/` (`.ts/.html/.css`) — Players config (filter schema: position multi-select from the Pompfe catalog + city + locked near-me, sort A–Z, fetch via `SearchService.browsePlayers`), player row (avatar via `/profiles/{handle}/avatar`, name, city, position), and the "only opted-in players appear" note
- [X] T038 [US3] Wire an "Appear in search" toggle into `features/profile/profile-owner/` (`.ts/.html`) via `PUT /profiles/me`, reflecting persisted state on reload

**Checkpoint**: All three browse pages are independently functional; opt-in privacy invariant verified

---

## Phase 6: User Story 4 — Consistent shell, empty & no-results states (Priority: P2)

**Goal**: Finalize the four list states and prove the three pages are behaviourally identical

**Independent Test**: On each page a nonsense query shows a no-results state with a working clear action; an empty dataset shows the distinct empty state; a failed request shows error+retry; the three pages share the same shell controls

- [X] T039 [US4] Finalize the four list states in `browse-shell` (`.ts/.html/.css`) — distinct **empty** (no data), **no-results** (query/filters matched nothing, with clear-query/reset-filters action), **loading** indication, and **error + retry**; wire the live "Show N" pending count in the filter panel via the `take=0` helper
- [X] T040 [P] [US4] Jest unit tests for the shell state machine `features/browse/browse-shell/browse-shell.component.spec.ts` — debounce, chip add/remove + Clear all, pending-vs-applied filter state, live count, first-page reset on query/filter change, the four states
- [X] T041 [US4] Extend `browse.spec.ts` — no-results + clear, empty state, and a cross-page consistency assertion (search, Filters+badge, Sort, chips, count present on all three), desktop + mobile

**Checkpoint**: Shell states complete; SC-004 (identical behaviour) and FR-012 (states) verified

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T042 [P] DESIGN.md conformance + responsive pass on all browse pages/sheet/drawer at mobile + desktop breakpoints (sand/coral tokens, sentence case, no emoji)
- [X] T043 [P] Accessibility pass on the shell — search/label semantics, filter panel focus trap + escape, chip remove buttons, locked Near-me `aria-disabled`
- [X] T044 Run `specs/007-search/quickstart.md` Scenarios A–H end-to-end and record results
- [X] T045 [P] Security re-review of the opt-in invariant (no bypass path; card DTOs leak no private fields) and confirm all browse endpoints are `[AllowAnonymous]` yet still opt-in gated
- [X] T046 [P] Verify DB indexes are used (EXPLAIN on the browse queries); add the deferred `pg_trgm` name/city GIN index only if profiling warrants (note in research)

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies
- **Foundational (Phase 2)**: depends on Setup — **blocks all user stories** (migration + shared shell)
- **US1 Teams (Phase 3)**: after Foundational — MVP, no dependency on other stories
- **US2 Events (Phase 4)**: after Foundational — independent of US1
- **US3 Players (Phase 5)**: after Foundational — independent of US1/US2
- **US4 Shell/states (Phase 6)**: after Foundational; best validated once ≥1 story page exists (uses a real page to exercise states)
- **Polish (Phase 7)**: after the desired stories are complete

### Within a story

- Tests (marked ⚠️) written first and expected to fail before implementation
- DTOs before services; services before controller endpoints; backend endpoint before its frontend page
- Program.cs DI registration accompanies each service

### Parallel opportunities

- Setup: T001, T002, T003 in parallel
- Foundational: T007 (search helper) ∥ T009/T010/T011 (frontend shell) after T004–T006; T008 seeder ∥ frontend
- Once Foundational is done, **US1, US2, US3 can proceed in parallel** (different controllers/services/pages)
- Within a story, the `[P]` test tasks and DTO tasks run in parallel

---

## Parallel Example: User Story 1

```bash
# Tests first (parallel):
Task: "TeamBrowseTests.cs — filters, active derivation, accent search, pagination"
Task: "TeamSettingsTests.cs — PATCH beginners admin/non-admin"
Task: "browse.spec.ts Teams section — defaults, live search, sheet/drawer, chips"

# Then DTOs (parallel):
Task: "TeamCardDto.cs + TeamBrowseQuery.cs"
Task: "UpdateTeamSettingsRequest.cs"
```

---

## Implementation Strategy

### MVP first (US1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational (migration + shared shell) → 3. Phase 3 US1 Teams → **STOP & validate** `/browse/teams` end-to-end → demo.

### Incremental delivery

Foundation → US1 Teams (MVP) → US2 Events → US3 Players (+ opt-in) → US4 states/consistency → Polish. Each story is an independently testable, deployable increment.

### Notes

- [P] = different files, no incomplete dependency; [Story] label maps to spec user stories
- Commit after each task or logical group; verify tests fail before implementing
- The player opt-in invariant (T030) is the feature's key security gate — do not weaken it
