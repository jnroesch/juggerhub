---
description: "Task list for feature 008 — Home dashboard & top-level navigation"
---

# Tasks: Home dashboard & top-level navigation

**Input**: Design documents from `/specs/008-home-dashboard-nav/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: INCLUDED — plan.md requests backend integration tests (xUnit + Testcontainers), frontend Jest unit, and a Playwright e2e. Write each story's tests before/alongside its implementation and ensure they fail first.

**Organization**: Tasks are grouped by user story (US1–US5) so each is independently implementable and testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: US1–US5 (Setup/Foundational/Polish carry no story label)

## Path conventions

- Backend: `backend/…` (.NET solution, namespace `JuggerHub`)
- Frontend: `frontend/apps/web/src/app/…`; e2e: `frontend/apps/web-e2e/src/…`
- Angular components keep separate `.ts` / `.html` / `.css` (constitution VI)

---

## Phase 1: Setup (shared scaffolding)

**Purpose**: Create the empty slices, DTOs, options, and index/seed plumbing every story builds on. No behavior yet.

- [ ] T001 [P] Create Home DTO records in `backend/Dtos/Home/HomeDtos.cs` — `HomeDto`, `ViewerSummaryDto`, `MyTeamDto`, `UpNextItemDto`, `TeamGoingDto`, `TeamActivityDto`, `HomeNewsDto`, `TournamentCardDto`, `TeamSnapshotDto`, `NextFixtureDto` (shapes per data-model.md; enums serialize as names)
- [ ] T002 [P] Create `backend/Common/HomeOptions.cs` — `UpNextCap=5, NewsCap=5, ActivityCap=6, TournamentCap=3, TeamsCap=12, NewsWindow=50` (safe defaults)
- [ ] T003 [P] Create `backend/Services/Home/IHomeService.cs` — `GetHomeAsync`, `ListUpNextAsync`, `ListNewsAsync`, `ListMyTeamsAsync` signatures (return `HomeDto` / `PagedResult<…>`)
- [ ] T004 Create `backend/Services/Home/HomeService.cs` skeleton implementing `IHomeService` (methods throw `NotImplementedException` for now; ctor injects `AppDbContext`, `IOptions<HomeOptions>`) — depends on T001–T003
- [ ] T005 [P] Create `backend/Services/Home/HomeProjections.cs` and `HomeNewsMerge.cs` as empty helper stubs (static `Expression<>` holders + merge signature) — parallel with T004 (different files)
- [ ] T006 Register `IHomeService` and bind `HomeOptions` in `backend/Program.cs` (mirror existing Search/Events registrations) — depends on T004
- [ ] T007 [P] Add read indexes in `backend/Data/AppDbContext.cs` — `EventSignups(UserId)`, `EventSignups(TeamId)`, `TeamMemberships(UserId)`, `TeamNewsPosts(TeamId, CreatedDate)`, `EventNewsPosts(EventId, CreatedDate)` — **first verify** which already exist (feature 007) and add only the missing
- [ ] T008 Generate the index-only EF migration `AddHomeIndexes` (`dotnet ef migrations add AddHomeIndexes` in `backend/`) and confirm it contains only `CreateIndex` ops — depends on T007
- [ ] T009 [P] Create frontend `frontend/apps/web/src/app/core/models/home.models.ts` — TypeScript mirrors of the Home DTOs (per contracts/openapi.yaml)
- [ ] T010 [P] Create `frontend/apps/web/src/app/layout/nav-model.ts` — shared destination list (Home/Browse/My team/Alerts), `isActive(route)` matcher, and the "My team" target resolver (0 → find-a-team, 1 → `/t/{slug}`, many → `/my-team`)

**Checkpoint**: Slices compile; no endpoints wired yet.

---

## Phase 2: Foundational (blocking prerequisites)

**Purpose**: The composite endpoint skeleton, the entitlement seam, `me/teams`, the shared frontend services, and a minimal Home host — everything all stories depend on.

**⚠️ No user-story work starts until this phase is done.**

- [ ] T011 Implement the `myTeams` entitlement helper + `ViewerSummaryDto` load in `backend/Services/Home/HomeService.cs` (caller's team ids from `TeamMemberships`; display name/handle/avatar-presence from `PlayerProfile`) — depends on T004
- [ ] T012 Implement `GetHomeAsync` composite skeleton in `HomeService.cs` returning the real `viewer` + `teams` + **empty** `upNext`/`teamsActivity`/`news`/`tournaments`/`snapshots` (stories fill each array) — depends on T011
- [ ] T013 Implement `ListMyTeamsAsync` in `HomeService.cs` (`PagedResult<MyTeamDto>` over `TeamMemberships.Where(UserId==me)`, projected + `AsNoTracking`) — depends on T011
- [ ] T014 Create `backend/Controllers/HomeController.cs` — `[Authorize]`, `[Route("api/v{version:apiVersion}/home")]`, `GET ""` → `GetHomeAsync`; `GET "up-next"` and `GET "news"` thin actions delegating to the service (caller id via the established `TryGetUserId` helper) — depends on T012
- [ ] T015 Extend `backend/Controllers/ProfilesController.cs` — `GET "me/teams"` → `ListMyTeamsAsync` (thin, `[Authorize]`, `PaginationRequest`) — depends on T013
- [ ] T016 [P] Create `frontend/apps/web/src/app/core/services/home.service.ts` — `getHome()`, `getUpNext(skip,take)`, `getNews(skip,take)` (typed over home.models) — depends on T009
- [ ] T017 [P] Create `frontend/apps/web/src/app/core/services/membership.service.ts` — signal of the viewer's teams via `GET /profiles/me/teams`; exposes `hasTeam` + `myTeamTarget()` (used by nav-model) — depends on T009
- [ ] T018 Replace the health-check stub with a minimal Home host: rewrite `frontend/apps/web/src/app/features/dashboard/dashboard.component.{ts,html,css}` to load `getHome()`, expose loading/error signals, pick the team-member vs new-player **variant** (from `hasTeam`), and render empty module slots + the desktop main/right-rail grid — depends on T016, T017
- [ ] T019 Extend `DevDataSeeder` (`backend/Data/DevDataSeeder.cs`) — a team-member demo player (≥1 team; upcoming individuals sign-up + a team-entered event; team + event news; an upcoming tournament) and a no-team demo player — depends on T001

**Checkpoint**: `/home` returns viewer + teams (modules empty); `me/teams` works; the shell can render a minimal Home. Foundation ready.

---

## Phase 3: User Story 1 — Navigate by task, not by data type (Priority: P1) 🎯 MVP

**Goal**: Replace the sidebar with a desktop top bar + mobile bottom tab bar + avatar menu, all from one nav model, with correct active state and "My team" routing.

**Independent Test**: Sign in → top bar (desktop) / bottom tab bar (mobile) expose Home/Browse/My team + Alerts + avatar; each navigates and marks the active destination; My team routes by membership (0/1/many); the old sidebar is gone.

### Tests for User Story 1

- [ ] T020 [P] [US1] Jest unit for `nav-model.ts` — active-match per route and "My team" target resolver (0/1/many teams) in `frontend/apps/web/src/app/layout/nav-model.spec.ts`
- [ ] T021 [P] [US1] Jest unit for the reworked shell/top-nav active-destination + avatar-menu items in `frontend/apps/web/src/app/layout/top-nav/top-nav.component.spec.ts`

### Implementation for User Story 1

- [ ] T022 [US1] Rework `frontend/apps/web/src/app/layout/top-nav/top-nav.component.{ts,html,css}` — desktop bar (brand · Home · Browse · My team · bell/Alerts · avatar), mobile slim top strip (wordmark + avatar); `routerLinkActive` + `aria-current`; DESIGN.md tokens — depends on T010, T017
- [ ] T023 [P] [US1] Create `frontend/apps/web/src/app/layout/bottom-nav/bottom-nav.component.{ts,html,css}` — fixed mobile bottom tab bar (Home · Browse · My team · Alerts), active state, safe-area inset, ≥44px targets — depends on T010, T017
- [ ] T024 [P] [US1] Create `frontend/apps/web/src/app/layout/avatar-menu/avatar-menu.component.{ts,html,css}` — Profile · Account · Sign out; keyboard-navigable menu with focus trap + Escape (moves sign-out from the old top-nav) — depends on T010
- [ ] T025 [US1] Update `frontend/apps/web/src/app/layout/shell/shell.component.{ts,html,css}` — compose top-nav + bottom-nav (responsive), hydrate session + memberships on init, drop the sidebar wiring — depends on T022, T023, T024
- [ ] T026 [US1] Remove `frontend/apps/web/src/app/layout/sidebar/` and all references (shell imports, toggle output) — depends on T025
- [ ] T027 [US1] Add the `/my-team` route + a lightweight team-chooser page (or redirect for the 0/1 cases) in `app.routes.ts` + `features/…` consistent with the nav-model resolver — depends on T010
- [ ] T028 [US1] Backend integration test `backend/tests/JuggerHub.Api.IntegrationTests/Home/MyTeamsTests.cs` — `GET /profiles/me/teams` returns only the caller's memberships (0/1/many), 401 unauth — depends on T015

**Checkpoint**: The new shell works at both viewports and routes "My team" correctly, independent of dashboard content.

---

## Phase 4: User Story 2 — See and act on what's next (Priority: P1) 🎯 MVP

**Goal**: Home leads with Up next (own + team upcoming events, soonest-first) and a one-tap RSVP/withdraw toggle; team-mode shows read-only "your team is going".

**Independent Test**: A player signed up to (or on a team signed up to) upcoming events sees them soonest-first with working RSVP/withdraw; team-mode is read-only; empty prompt when nothing upcoming; "see all" paginates.

### Tests for User Story 2

- [ ] T029 [P] [US2] Backend integration test `backend/tests/JuggerHub.Api.IntegrationTests/Home/UpNextTests.cs` — union of own individuals sign-ups + teams' team-mode entries, soonest-first, **past/cancelled excluded**, dedupe on multi-team same event, individuals item carries `viewerSignupId`+`viewerStatus`, team item carries `teamGoing` + null signup id; pagination on `/home/up-next` (skip/take clamp, stable tie order) — depends on T014
- [ ] T030 [P] [US2] Backend entitlement test in the same folder — caller B never sees caller A's private sign-ups via `/home` or `/home/up-next` — depends on T014
- [ ] T031 [P] [US2] Jest unit for the up-next module state machine (loading/empty/error; individuals toggle vs team read-only) in `frontend/apps/web/src/app/features/dashboard/modules/up-next-card.component.spec.ts`
- [ ] T032 [P] [US2] Playwright `frontend/apps/web-e2e/src/dashboard.spec.ts` (US2 slice) — sign in → Up next lists soonest-first → RSVP flips to "going" → tap "going" → confirm → withdrawn; desktop + mobile

### Implementation for User Story 2

- [ ] T033 [US2] Implement the up-next projection in `HomeService.cs` + `HomeProjections.cs` — the two-source union (personal individuals + team-mode), soonest-first, dedupe, spots-remaining via `EventCapacity`, mapping to `UpNextItemDto` — depends on T012
- [ ] T034 [US2] Populate `HomeDto.upNext` (capped `UpNextCap`) in `GetHomeAsync` and implement `ListUpNextAsync` (paginated `PagedResult<UpNextItemDto>`) — depends on T033
- [ ] T035 [P] [US2] Create the up-next module `frontend/apps/web/src/app/features/dashboard/modules/up-next-card.component.{ts,html,css}` — date chip, title, place, time, spots; RSVP button (individuals, not going), "going ✓" toggle with confirm (individuals, going), read-only "your team is going" (team-mode); "see all" — depends on T018, T016
- [ ] T036 [US2] Wire RSVP/withdraw from the up-next module through `EventService.signup(id, null)` / `EventService.withdraw(id, viewerSignupId)` and update the item in place; surface going/waitlisted/full outcomes — depends on T035
- [ ] T037 [US2] Mount the up-next module in `dashboard.component` (team-member variant, first module) + a low-pressure empty state — depends on T035, T018

**Checkpoint**: Home shows Up next and RSVP works end-to-end. Combined with US1 this is the MVP.

---

## Phase 5: User Story 3 — Catch up on teams, news, and tournaments (Priority: P2)

**Goal**: Your teams activity (aggregated, tagged), News (team+event, tagged, newest-first), Tournaments, and the desktop right rail (per-team snapshot + tournament).

**Independent Test**: A player on team(s) with news/events + an upcoming tournament sees aggregated activity, source-tagged news newest-first, the tournament card, and (desktop) per-team snapshots; each module degrades to a friendly empty state.

### Tests for User Story 3

- [ ] T038 [P] [US3] Backend integration test `backend/tests/JuggerHub.Api.IntegrationTests/Home/NewsTests.cs` — team + event news merged newest-first, each tagged by source; "connected events" predicate; pagination on `/home/news`; entitlement (no non-member team news, no unconnected event news) — depends on T014
- [ ] T039 [P] [US3] Backend integration test `backend/tests/JuggerHub.Api.IntegrationTests/Home/TeamsModulesTests.cs` — aggregated team activity across all teams; tournaments = upcoming published Tournament events soonest-first; snapshots one-per-team with next fixture (no record) — depends on T014
- [ ] T040 [P] [US3] Jest unit for news-list + tournament-card + team-snapshot module states in `frontend/apps/web/src/app/features/dashboard/modules/news-list.component.spec.ts`

### Implementation for User Story 3

- [ ] T041 [US3] Implement `HomeNewsMerge.cs` — bounded-window read of `TeamNewsPost` (member teams) + `EventNewsPost` (connected events), tag by source, merge newest-first — depends on T011
- [ ] T042 [US3] Populate `HomeDto.news` (capped) + implement `ListNewsAsync` (paginated within window) in `HomeService.cs` — depends on T041
- [ ] T043 [P] [US3] Implement team-activity aggregation (across all `myTeams`, newest-first) → `HomeDto.teamsActivity` in `HomeService.cs`/`HomeProjections.cs` — depends on T012
- [ ] T044 [P] [US3] Implement tournaments query (upcoming published Tournament events, soonest-first) → `HomeDto.tournaments` — depends on T012
- [ ] T045 [P] [US3] Implement per-team snapshots (name + next fixture, no record) → `HomeDto.snapshots` — depends on T012
- [ ] T046 [P] [US3] Create `frontend/apps/web/src/app/features/dashboard/modules/team-activity.component.{ts,html,css}` — aggregated activity, tagged by team, "open team" link — depends on T018
- [ ] T047 [P] [US3] Create `frontend/apps/web/src/app/features/dashboard/modules/news-list.component.{ts,html,css}` — source-tagged items, relative timestamps, "see all" — depends on T018
- [ ] T048 [P] [US3] Create `frontend/apps/web/src/app/features/dashboard/modules/tournament-card.component.{ts,html,css}` — name/place/date/spots + "view"; "see all" → `/browse/events?type=Tournament` — depends on T018
- [ ] T049 [P] [US3] Create `frontend/apps/web/src/app/features/dashboard/modules/team-snapshot.component.{ts,html,css}` — per-team card (name + next fixture, "view team") — depends on T018
- [ ] T050 [US3] Compose the modules + desktop right rail (main column: activity, news, tournaments; rail: snapshots + tournament) into `dashboard.component`, each with empty states — depends on T046, T047, T048, T049

**Checkpoint**: Home is a full home base (agenda + catch-up + right rail).

---

## Phase 6: User Story 4 — A warm first run for players without a team (Priority: P2)

**Goal**: The new-player Home variant — welcome greeting, find-a-team prompts, "Open to everyone" module, News still present.

**Independent Test**: A no-team player sees the welcome variant (prompts instead of Up next/Your team, an open-events module, News present); joining a team flips to the standard variant.

### Tests for User Story 4

- [ ] T051 [P] [US4] Backend integration test `backend/tests/JuggerHub.Api.IntegrationTests/Home/NewPlayerTests.cs` — no-team viewer: `teams` empty, team-scoped modules empty, open-to-everyone events present, news present — depends on T014
- [ ] T052 [P] [US4] Jest unit for the dashboard variant selector (team-member vs new-player) + new-player prompts in `frontend/apps/web/src/app/features/dashboard/dashboard.component.spec.ts`
- [ ] T053 [P] [US4] Playwright (US4 slice) in `dashboard.spec.ts` — no-team player sees find-a-team + open-to-everyone + News; desktop + mobile

### Implementation for User Story 4

- [ ] T054 [US4] Add the "open to everyone" source to `HomeService.GetHomeAsync` for no-team viewers (upcoming open individuals-mode events, reusing the `UpNextItemDto` shape with null viewer signup → RSVP button) — depends on T033
- [ ] T055 [P] [US4] Create `frontend/apps/web/src/app/features/dashboard/modules/new-player-prompts.component.{ts,html,css}` — "you're not on a team yet"; primary "find a team near you" (→ Browse teams), secondary "browse open trainings" (→ open events) — depends on T018
- [ ] T056 [P] [US4] Create `frontend/apps/web/src/app/features/dashboard/modules/open-to-everyone.component.{ts,html,css}` — reuses the up-next card with RSVP — depends on T035
- [ ] T057 [US4] Wire the new-player variant in `dashboard.component` — swap Up next/Your team for prompts, mount open-to-everyone, keep News; welcome greeting copy — depends on T055, T056

**Checkpoint**: Both Home variants correct; no blank/broken dashboard for new players.

---

## Phase 7: User Story 5 — A place for alerts and account (Priority: P3)

**Goal**: The Alerts destination + a friendly placeholder (unread count zero/hidden); Profile/Account under the avatar (built in US1) verified.

**Independent Test**: Alerts is reachable from desktop bell + mobile tab and shows a placeholder; unread reads zero; avatar → Profile/Account reach existing screens.

### Tests for User Story 5

- [ ] T058 [P] [US5] Playwright (US5 slice) in `dashboard.spec.ts` — Alerts reachable from bell (desktop) + tab (mobile), placeholder shown, count hidden; avatar → Profile/Account

### Implementation for User Story 5

- [ ] T059 [P] [US5] Create `frontend/apps/web/src/app/features/alerts/alerts.component.{ts,html,css}` — friendly "you're all caught up" placeholder (no backend) — depends on T018
- [ ] T060 [US5] Add the `/alerts` route (in-shell, `authGuard`) in `app.routes.ts` and point the bell/Alerts destination at it; ensure the unread count is zero/hidden in top-nav + bottom-nav — depends on T059, T022, T023

**Checkpoint**: All destinations exist and are reachable; the shell is complete.

---

## Phase 8: Polish & cross-cutting

- [ ] T061 [P] Run the full quickstart.md scenarios (A–H) at desktop + mobile; fix any gaps
- [ ] T062 [P] DESIGN.md pass — warm sand/coral tokens, rounded, Lucide line icons, sentence case, mono for scores/times, one coral CTA per view, no emoji
- [ ] T063 [P] Accessibility pass — nav landmarks + `aria-current`, avatar menu keyboard/focus, ≥44px targets, contrast; verify with axe or manual
- [ ] T064 [P] Resilience — confirm one module's failure shows a retry and never blanks Home (SC-005); loading states avoid layout shift
- [ ] T065 Remove any dead code/imports from the sidebar removal and the old dashboard stub; `dotnet build` + `nx lint web` clean

---

## Dependencies & execution order

### Phase dependencies
- **Setup (P1)**: no dependencies.
- **Foundational (P2)**: depends on Setup — **blocks all stories**.
- **US1 / US2 (both P1)**: after Foundational. US1 is shell-only; US2 is dashboard content — independent of each other. **MVP = US1 + US2.**
- **US3, US4 (P2)**: after Foundational; US4's open-to-everyone reuses the US2 up-next card (T056 → T035).
- **US5 (P3)**: after US1 (bell/tab live in the reworked nav).
- **Polish (P8)**: after the desired stories.

### Story independence notes
- US1 and US2 touch different trees (layout vs features/dashboard) and can proceed in parallel.
- US2/US3/US4 all edit `HomeService.cs`/`dashboard.component` — sequence those edits (not [P] across stories on the same file), but their module components ([P]) are independent files.

### Parallel opportunities
- Setup: T001, T002, T003, T005, T007, T009, T010 in parallel.
- Foundational: T016, T017 in parallel (frontend services) alongside backend T011–T015.
- US1: T020, T021 (tests) and T023, T024 (bottom-nav, avatar-menu) in parallel.
- US2: T029–T032 (tests) in parallel; then T035 module.
- US3: all module components T046–T049 in parallel; backend T043–T045 in parallel after T012.

---

## Parallel example: User Story 3 module components

```bash
Task: "Create team-activity.component in features/dashboard/modules/"     # T046
Task: "Create news-list.component in features/dashboard/modules/"          # T047
Task: "Create tournament-card.component in features/dashboard/modules/"    # T048
Task: "Create team-snapshot.component in features/dashboard/modules/"      # T049
```

---

## Implementation strategy

### MVP (User Stories 1 + 2)
1. Phase 1 Setup → Phase 2 Foundational.
2. US1 (new nav shell) + US2 (Up next + RSVP) — the two P1 stories.
3. **STOP & VALIDATE**: sign in → new shell navigates everywhere; Home leads with Up next; RSVP toggles. Deploy/demo.

### Incremental delivery
- + US3 (catch-up modules + right rail) → demo.
- + US4 (new-player variant) → demo.
- + US5 (Alerts placeholder) → demo.
- Polish pass (quickstart, DESIGN.md, a11y, resilience).

## Notes
- No new entities/columns/writes — RSVP reuses existing event endpoints; only reads + UI are added.
- `[P]` = different files, no incomplete-task dependency. `[Story]` maps to spec.md US1–US5.
- Commit after each task or logical group; verify each story's tests fail before implementing.
