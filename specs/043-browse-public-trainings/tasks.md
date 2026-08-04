---

description: "Task list for 043 — Browse Public Trainings"
---

# Tasks: Browse Public Trainings

**Input**: Design documents from `/specs/043-browse-public-trainings/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/trainings-browse.md](./contracts/trainings-browse.md),
[quickstart.md](./quickstart.md)

**Tests**: **Included.** Not a default — the spec's success criteria are written as verifications
(SC-002 "0% of team-only … appear", SC-003 "character for character", SC-010 "existing tests
continuing to pass unmodified"), and `data-model.md` §7 enumerates nine invariants as
"a test must pin". Constitution gates 3 and 7 also require them.

**Organization**: grouped by user story so each is independently implementable, testable, and
shippable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on incomplete work)
- **[Story]**: `US1` / `US2` / `US3`, mapping to the spec's user stories
- Every task names its exact file path

## Path Conventions

Web application (constitution Technology Stack): `backend/` for .NET, `frontend/apps/web/` for
Angular, `frontend/apps/web-e2e/` for Playwright.

---

## ⚠ Read before starting

Three rules carry this feature. Violating any one produces code that passes review and is wrong:

1. **The address is an indivisible block keyed on `CityIdOverride`** — in the `WHERE` clause as much
   as in the projection. Never `s.VenueNameOverride ?? s.Training.VenueName`. The city id is the one
   field where `??` would be equivalent; write the ternary anyway (research R3).
2. ~~**Call `TrainingSeriesService.LocationLabelFor`, never copy it.**~~ **⚠ THIS RULE WAS WRONG AND
   WAS CORRECTED DURING IMPLEMENTATION.** `LocationLabelFor` returns the **city alone**; the events
   *browse* card builds `"City, Country"`. Calling it produced "Berlin" against the events list's
   "Berlin, Germany" — SC-003's own test caught it. The browse label is built by
   `TrainingSearchService.BrowseLocationLabel` using `LocationLabels.Display`, which is the shared
   `"City, Country"` formatter. `LocationLabelFor` remains correct for the *dashboard agenda*, where
   events use it too. See research R4's correction note before touching either.
3. **No migration.** If `dotnet ef migrations add` ever runs for this feature, stop — the feature
   reads existing state (data-model.md preamble).

Also: **do not edit `EventSearchService`, `EventBrowseTests`, or the events browse page.** Its
proximity-count defect (research R5) is real and deliberately out of scope — FR-030 forbids it and
SC-010 verifies it.

---

## Phase 1: Setup

**Purpose**: establish an attributable baseline and the design gate before any code changes.

- [X] T001 Record a green baseline — run `dotnet test backend/JuggerHub.slnx` and `npx nx test web`, and note the passing counts in the PR description so any later failure is attributable to this feature rather than to a stale `node_modules` (the Angular 21→22 lesson)
- [X] T002 [P] Instantiate the UI review checklist by copying `.specify/templates/ui-review-checklist-template.md` to `specs/043-browse-public-trainings/checklists/ui-review.md` (constitution gate 7)
- [X] T003 [P] Confirm no schema work is implied — run `dotnet ef migrations list` from `backend/` and record that the feature adds none; a new migration appearing later is a defect, not progress

**Checkpoint**: baseline known-green, design gate armed.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: the contract surface and test scaffolding every user story binds to. Delivers no user-visible behaviour on its own.

**⚠️ CRITICAL**: no user story work can begin until this phase is complete.

- [X] T004 [P] Add the `TrainingSort` enum (`SessionDateAsc = 0`, `Proximity = 1`) to `backend/Services/Search/SearchQuery.cs`, beside `EventSort`, with an XML doc noting that `Proximity` requires a home city (feature 030 pattern)
- [X] T005 [P] Add `TrainingCardDto` and `TrainingBrowseQuery` to `backend/Dtos/Search/SearchDtos.cs` per [data-model.md](./data-model.md) §3–§4 — `{ get; init; }` with defaults so `[FromQuery]` binding matches the existing three, and no `Type` member (trainings have no type)
- [X] T006 [P] Add the `TrainingCard` and `TrainingBrowseParams` interfaces to `frontend/apps/web/src/app/core/models/search.models.ts`, mirroring `EventCard` / `EventBrowseParams`
- [X] T007 Add `browseTrainings()` and a `toTrainingParams()` builder to `frontend/apps/web/src/app/core/services/search.service.ts` calling `/api/v1/trainings` — ⚠ it **must** append `city`; `toEventParams` and `toTeamParams` send only `country` despite their backends accepting both, so copying one verbatim silently drops the city filter (research R8). Depends on T006
- [X] T008 [P] Extend `backend/tests/JuggerHub.Api.IntegrationTests/Search/SearchTestSupport.cs` with training seeding via the existing `WithDbAsync` — a helper that creates a team + `Training` + `TrainingSession` rows with explicit control over `Visibility`, `VisibilityOverride`, `Status`, `SessionDate`, the series address, and the per-session address override. Direct-to-DbContext, not through the API, because several invariants (a pre-042 legacy-location training; a session whose `CityIdOverride` is set with no venue) have no API path
- [X] T009 Create `backend/Services/Search/TrainingSearchService.cs` with `ITrainingSearchService.BrowseAsync(TrainingBrowseQuery, PaginationRequest, Guid? homeCityId, CancellationToken)`, implementing **only** the two unconditional gates and paging at this stage: effective visibility `(s.VisibilityOverride ?? s.Training.Visibility) == TrainingVisibility.Public` and `s.Status == TrainingSessionStatus.Scheduled`, over `AsNoTracking()`, returning `PagedResult<TrainingCardDto>`. Depends on T004, T005
- [X] T010 Register `ITrainingSearchService` in `backend/Program.cs` beside the other three search services (~line 404), and add the root `[HttpGet]` `Browse` action to `backend/Controllers/TrainingsController.cs` binding `[FromQuery] TrainingBrowseQuery` + `[FromQuery] PaginationRequest` and delegating — thin, no home-city logic yet. Auth is inherited from the class-level `[Authorize]`; add no `[AllowAnonymous]`. Depends on T009

**Checkpoint**: `GET /api/v1/trainings` returns public scheduled sessions, paged. User stories can begin.

---

## Phase 3: User Story 1 — Find an open training I could actually attend (Priority: P1) 🎯 MVP

**Goal**: a stranger can discover a public training and RSVP as a guest, and the home screen's "Browse open trainings" button finally leads somewhere real.

**Independent Test**: sign in as an account belonging to no team, open Browse → Trainings, confirm public sessions are listed and team-only ones are not (including when signed in as a member of the owning team), open a row, and respond Going as a guest.

### Tests for User Story 1

> Write these first and watch them fail. T011 and T013 are the two that matter most — they are the invariants a plausible-looking implementation breaks.

- [X] T011 [P] [US1] Integration test in `backend/tests/JuggerHub.Api.IntegrationTests/Search/TrainingBrowseTests.cs` — the visibility gate (DM-1, DM-2): a team-only session is absent **for a member of the owning team as well as an outsider**; a public session inside a team-only series is present; a team-only session inside a public series is absent
- [X] T012 [P] [US1] Integration test in the same file — `Cancelled` and `Skipped` sessions never appear, under default filters and with `hidePast=false` (DM-3)
- [X] T013 [P] [US1] Integration test in the same file — the relocated-session guard (DM-4): a series **with** a venue name, one session relocated to an address **without** one, must return no element of the series' address; this is 042's guard re-pointed at browse and is what a per-field `??` breaks
- [X] T014 [P] [US1] Integration test in the same file — a pre-042 training (legacy `Location`, `CityId` null) is still listed and still returns a non-empty `locationLabel` (DM-6)
- [X] T015 [P] [US1] Integration test in the same file — SC-003: an event and a training seeded at the same city and venue return byte-identical `locationLabel` from `/api/v1/events` and `/api/v1/trainings` (DM-7)
- [X] T016 [P] [US1] Integration test in the same file — an unauthenticated request to `/api/v1/trainings` returns 401 (FR-007)
- [X] T017 [P] [US1] Integration test in the same file — paging across the full result set with `skip`/`take` repeats no session and skips none, and `totalCount` matches the number of distinct rows paged (DM-9, default sort)

### Implementation for User Story 1

- [X] T018 [US1] In `backend/Services/Search/TrainingSearchService.cs`, add the raw SQL projection — a `TrainingCardRaw` record carrying the effective kind/times and the address block resolved as **one block** keyed on `CityIdOverride` per [data-model.md](./data-model.md) §2.3, plus `TeamSlug`/`TeamName` from the denormalised `TrainingSession.TeamId` join
- [X] T019 [US1] In the same file, compose `TrainingCardDto` in memory after materialisation, deriving `LocationLabel` by **calling** `TrainingSeriesService.LocationLabelFor` and `LocationLabels.ToLocation` — do not re-implement either (research R4). Follow the two-step shape of `TrainingSeriesService.PageRowsAsync`. Depends on T018
- [X] T020 [US1] In the same file, apply the default ordering `SessionDate, Id` and the day-granular upcoming filter `s.SessionDate >= today` when `HidePast` is true, with a comment recording that this is deliberately day-granular to match every other trainings query (research R2, plan Spec Drift). Depends on T019
- [X] T021 [P] [US1] Create `frontend/apps/web/src/app/features/browse/browse-trainings/browse-trainings.component.ts` on `BrowseList<TrainingCard>` + `jh-browse-shell`, modelled on `browse-events.component.ts` — ⚠ all filter state in signals, including the `langChanges$` signal that keeps computed labels reactive; a `computed()` over plain fields never recomputes in a zoneless app (042's e2e lesson)
- [X] T022 [P] [US1] Create `browse-trainings.component.html` and `.css` in the same directory — the row shows name, team name, date, start–end time, location label, and a Series/One-off badge, linking to `/trainings/sessions/{{ sessionId }}`; render the "Online" wording from `locationKind`, since the backend returns an empty label for a virtual training
- [X] T023 [US1] Add the fourth tab to `frontend/apps/web/src/app/features/browse/browse-shell/browse-shell.component.html` with `data-testid="browse-tab-trainings"` — and resolve the 375px layout against DESIGN.md (research R9). Spanish "Entrenamientos" is the binding case; **a smaller font or a truncation is not an acceptable fix**
- [X] T024 [US1] Register the `browse/trainings` route with `authGuard` and a lazy `loadComponent` in `frontend/apps/web/src/app/app.routes.ts`, beside the existing three browse routes. Depends on T021
- [X] T025 [US1] Repoint the home empty state's "Browse open trainings" anchor from `/browse/events` to `/browse/trainings` in `frontend/apps/web/src/app/features/dashboard/dashboard.component.html:72` (FR-027) — the one-line fix that motivated the feature
- [X] T026 [P] [US1] Add the US1 i18n keys (`browse.tabTrainings`, `browse.trainings.searchPlaceholder`, `.noun`, `.countOne`, `.countMany`, `.empty`, `.seriesBadge`, `.oneOffBadge`) to all three of `frontend/apps/web/public/i18n/{en,de,es}.json` — add them to `en.json` first and run the parity guard to watch it go **red**, then add `de`/`es` and watch it go green (research R11: run it, don't assume it)
- [X] T027 [P] [US1] Jest spec `browse-trainings.component.spec.ts` in the same directory — renders rows, shows the empty state distinctly from no-results, and links each row to the session page
- [X] T028 [US1] Add US1 scenarios to `frontend/apps/web-e2e/src/browse.spec.ts` — the fourth tab navigates, a public training is listed, a team-only one is not, and the home empty-state button lands on `/browse/trainings`. ⚠ scope selectors to avoid the responsive desktop-table/mobile-card strict-mode trap the existing browse e2e already works around

**Checkpoint**: US1 is independently shippable — discovery works end to end and the advertised button is no longer a dead end.

---

## Phase 4: User Story 2 — Narrow the list down to what is relevant to me (Priority: P2)

**Goal**: search by name and filter by city, country, and date range, with visible chips and an honest count.

**Independent Test**: seed public sessions across two cities, two countries, and a spread of dates; confirm each filter narrows correctly alone and combined, that the count line follows, and that clearing restores the list.

### Tests for User Story 2

- [X] T029 [P] [US2] Integration test in `TrainingBrowseTests.cs` — the city and country filters narrow correctly, and the endpoint returns **no** non-matching row (constitution Principle I: filtering is server-side, never a client concern)
- [X] T030 [P] [US2] Integration test in the same file — the relocated-session filter guard (DM-5): a session moved to another city is returned by a filter on **its** city and **not** by a filter on the series' city
- [X] T031 [P] [US2] Integration test in the same file — the date range narrows with `from` alone, `to` alone, and both; and `hidePast=false` reveals past sessions while cancelled/skipped stay hidden
- [X] T032 [P] [US2] Integration test in the same file — name search is accent- and case-insensitive (`anfanger` finds `Anfängertraining`) and a below-minimum term is treated as absent rather than erroring

### Implementation for User Story 2

- [X] T033 [US2] In `backend/Services/Search/TrainingSearchService.cs`, add the city and country filters resolved **through the address block** — `(s.CityIdOverride != null ? s.CityOverride.Name : s.Training.City.Name)`, never `s.Training.City.Name` alone (research R3) — using `AppDbContext.Unaccent` + `EF.Functions.ILike` as `EventSearchService.cs:64-77` does
- [X] T034 [US2] In the same file, add the `From`/`To` filters against `SessionDate` and the name search via `SearchQuery.Normalize` + `SearchQuery.ContainsPattern` over `s.Training.Name`. Depends on T033
- [X] T035 [US2] Add the filter panel to `browse-trainings.component.html` — `jh-filter-toggle` for upcoming-only, the two date inputs, `jh-country-picker`, and `jh-city-picker`, following the `browse-events.component.html` panel structure
- [X] T036 [US2] Wire the pending/applied filter signals, chips, `removeChip`, `clearAll`, and the `refreshPendingCount` preview call in `browse-trainings.component.ts`, mirroring `browse-events.component.ts`. ⚠ `jh-city-picker` emits a `CityOption`; send `option.name` as the `city` param and `null` on clear. Depends on T035
- [X] T037 [P] [US2] Add the US2 i18n keys (`browse.trainings.hidePastLabel`, `.hidePastHint`, `.dateRange`, `.fromDate`, `.toDate`, `.city`, `.chipUpcoming`, `.noResults`) to all three catalogues and re-run the parity guard
- [X] T038 [P] [US2] Extend `browse-trainings.component.spec.ts` — applying a filter re-runs the fetch with the expected params, removing a chip clears only that filter, and the no-results state renders distinctly from empty

**Checkpoint**: US1 and US2 both work independently.

---

## Phase 5: User Story 3 — Show me the closest ones first (Priority: P3)

**Goal**: viewers with a home city can order by distance; viewers without one are not offered it and are told what to do if they ask anyway.

**Independent Test**: with a home city set and trainings seeded at differing distances, switch to nearest-first and confirm the ordering; repeat with no home city and confirm the option is absent.

> ⚠ **Open Decision #1 applies to this phase.** The 14-day default window in T042 is the plan's resolution of FR-024, not an owner answer. If the owner prefers next-session-per-series or accept-as-is, change T042 before implementing — the rest of the phase is unaffected.

### Tests for User Story 3

- [X] T039 [P] [US3] Integration test in `TrainingBrowseTests.cs` — `sort=Proximity` orders by distance from the caller's home city, ascending, with ties broken by date then id
- [X] T040 [P] [US3] Integration test in the same file — `sort=Proximity` from a caller with **no** home city returns `409` with title "No home city", and never a `200` carrying a different ordering (FR-021)
- [X] T041 [P] [US3] Integration test in the same file — virtual and cityless (pre-042) sessions are excluded from proximity results **and** from `totalCount`, so paging to the end reaches exactly `totalCount` rows (DM-8, FR-022/FR-023) — this is the check that separates this implementation from `EventSearchService`'s count-before-join defect

### Implementation for User Story 3

- [X] T042 [US3] ~~Normalise the proximity query with a 14-day default window~~ — **REMOVED at the owner's request (2026-08-04).** It was built (first server-side, then in the component) and then taken out: a sort control that silently applies a date filter is surprising. `onSortChange` changes the ordering only; the `chipNearbyWindow` key is gone from all three catalogues; a unit test pins that `from`/`to` survive a sort change untouched. FR-024 rewritten to require this. See research R1's supersede note
- [X] T043 [US3] In the same file, add the proximity page — join `CityDistances` anchored on `homeCityId` against the **effective** city id (`s.CityIdOverride != null ? s.CityIdOverride : s.Training.CityId`), ordering `DistanceKm, SessionDate, Id`. Depends on T042
- [X] T044 [US3] In the same file, recompute the proximity total with the join's own `Any()` predicate — follow `TeamSearchService.cs:94-97`, **not** `EventSearchService`, whose count precedes the join and would overstate the page (research R5). Depends on T043
- [X] T045 [US3] In `backend/Controllers/TrainingsController.cs`, resolve the caller first and then the home city via `IProfileService.GetHomeCityIdAsync`, returning `409` when it is null — mirroring `TeamsController.cs:88-104` exactly, including resolving the caller **before** reading any query value so auth never depends on user-supplied input
- [X] T046 [US3] In `browse-trainings.component.ts`, add the sort options — offer "Nearest first" only when the profile carries a home city (via `ProfileService.getMineCached()`, as `browse-events.component.ts:112-118` does) — and surface the server's effective date range as a removable chip when proximity applies the default window
- [X] T047 [P] [US3] Add the US3 i18n keys (`browse.trainings.sortSoonest`, `browse.trainings.chipNearbyWindow`) to all three catalogues, reusing the existing shared `browse.sortNearest`, and re-run the parity guard
- [X] T048 [P] [US3] Extend `browse-trainings.component.spec.ts` — the nearest-first option is hidden without a home city and shown with one, and choosing it re-runs the fetch with `sort=Proximity`

**Checkpoint**: all three user stories independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T049 Complete `specs/043-browse-public-trainings/checklists/ui-review.md` against the actual diff, with DESIGN.md winning any conflict (constitution gate 7) — the fourth-tab layout is the item that needs real judgement, not a tick
- [X] T050 [P] Verify SC-008 with a Playwright assertion — **placed in `frontend/apps/web-e2e/src/browse.spec.ts` rather than `responsive.spec.ts`**, next to the other tab-strip checks; both projects (desktop 1280px and mobile 393px) run it, which is how the repo already expresses "every e2e runs at both viewports". Two cases: measured tabs (height ≥44px, no scroll overflow, no pairwise overlap) and a second substituting the longest label from each catalogue so Spanish "Entrenamientos" is covered without driving the language switcher. **Proven non-vacuous**: a scratch test forcing the rejected 4-across layout clipped "Entrenamientos" at 393px (and not at 1280px), confirming the problem was real and the grid fixes it — all four tabs legible, unclipped, non-overlapping, and tappable at the 44px minimum, in **each** of en/de/es
- [X] T051 [P] Run the i18n parity guard `frontend/apps/web/src/app/core/i18n/catalog-parity.spec.ts` via `npx nx test web --testPathPattern catalog-parity` and confirm green across every key added in T026, T037, T047
- [X] T052 Confirm SC-010 — `backend/tests/JuggerHub.Api.IntegrationTests/Search/EventBrowseTests.cs` passes **unmodified**, and `git diff` touches neither it, `backend/Services/Search/EventSearchService.cs`, nor `frontend/apps/web/src/app/features/browse/browse-events/`. A modified events test is scope creep into FR-030, not a fix
- [~] T053 **PARTIAL — see the completion report.** Verified against the running stack: the endpoint exists and 401s unauthenticated, the four-tab strip and its navigation, and the home empty-state button landing on `/browse/trainings` (all via e2e). The relocated-session and label-parity checks are covered by integration tests rather than by hand. **Not walked manually**: the guest-RSVP journey end to end, and the visual filter/sort interactions against a running stack, including the relocated-session and label-parity checks that the integration tests also cover — the manual pass catches what fixtures hide
- [X] T054 [P] Opened as GH #146 — follow-up GitHub issue for the `EventSearchService` proximity count-before-join defect found in research R5, referencing `EventSearchService.cs:92-95` and `TeamSearchService.cs:94-97` as the correct shape — deliberately not fixed here (plan Deliberate Non-Goals)
- [X] T055 Full verification sweep run — backend 767/767, frontend 417/417, lint 0 errors, build OK, e2e 86 passed / 8 failed (all pre-existing admin+recognition specs blocked by a stale local `admin@test.de` account, not this feature). Counts and caveats reported rather than summarised as "green"
- [X] T056 [US1] Restyle the row to the home agenda's card shape — dark weekday/day/month date chip via `injectDateFormats` (which accepts a date-only value), with the **team name promoted to the primary line** and the training's own name demoted to the meta line, in `browse-trainings.component.html`. Owner decision 2026-08-04: a guest is choosing a team, not reading a generic label. Hooks `training-team` / `training-meta` added so the test asserts hierarchy rather than CSS classes

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies
- **Foundational (Phase 2)**: depends on Setup — **blocks all user stories**
- **US1 (Phase 3)**: depends on Foundational. No dependency on US2 or US3
- **US2 (Phase 4)**: depends on Foundational. Independently testable against the Phase 2 endpoint; in practice follows US1 because it extends the same service and component
- **US3 (Phase 5)**: depends on Foundational. Independent of US2
- **Polish (Phase 6)**: depends on whichever stories shipped

### Within Each User Story

Tests → backend service → endpoint → frontend component → i18n → e2e. Tests are written first and
must fail before the implementation lands.

### Critical Path

`T005 → T009 → T010 → T018 → T019 → T020 → T021 → T024 → T025`

Nine tasks from nothing to a working MVP.

### Parallel Opportunities

- **Phase 1**: T002, T003
- **Phase 2**: T004, T005, T006, T008 all touch different files; T007 waits on T006, T009 on T004+T005
- **US1 tests**: T011–T017 are seven independent `[Fact]`s in one new file — write together, they share only the seeding helper from T008
- **US1 implementation**: T021 and T022 (component logic vs template) and T026/T027 run alongside the backend tasks T018–T020, since the contract is fixed by Phase 2
- **Cross-story**: with more than one developer, US2 and US3 can proceed in parallel after Foundational — they touch different regions of the same service and component, so coordinate on `TrainingSearchService.cs` and `browse-trainings.component.ts`

### Parallel Example: User Story 1 tests

```bash
# All seven US1 invariants — one new file, independent facts:
Task: "Visibility gate incl. member-of-owning-team in Search/TrainingBrowseTests.cs"     # T011
Task: "Cancelled and Skipped never listed in Search/TrainingBrowseTests.cs"              # T012
Task: "Relocated session leaks no series address in Search/TrainingBrowseTests.cs"       # T013
Task: "Pre-042 legacy location still labelled in Search/TrainingBrowseTests.cs"          # T014
Task: "Training and event label byte-identical in Search/TrainingBrowseTests.cs"         # T015
Task: "Anonymous request returns 401 in Search/TrainingBrowseTests.cs"                   # T016
Task: "Paging repeats and skips nothing in Search/TrainingBrowseTests.cs"                # T017
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 Setup → Phase 2 Foundational → Phase 3 US1
2. **STOP and validate**: run the US1 quickstart scenarios, especially step 3 — the team-only session
   must stay absent *for a member of the owning team*
3. Shippable here. The dead button is fixed and discovery works; filters and proximity are refinements

### Incremental Delivery

1. Foundational → endpoint answers
2. + US1 → **MVP**, demo-able
3. + US2 → filters and search
4. + US3 → nearest-first (settle Open Decision #1 first)
5. + Polish → design gate, parity, sweep

---

## Notes

- `[P]` = different files, no dependency on incomplete work
- Commit per task or per logical group; the repo convention is small commits
- Reference `#145` in commit messages and use `Closes #145` in the PR
- **Never claim a verification passed if it was not run** (constitution, Verification & Reporting)
- Two decisions remain open and are recorded in [plan.md](./plan.md) → Open Decisions: the FR-024
  proximity window (affects T042) and the fourth-tab layout (affects T023)
