---

description: "Task list for 044 — Team-internal 'What's happening' section"
---

# Tasks: Team-internal "What's happening" section

**Input**: Design documents from `/specs/044-team-activity-feed/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/team-happenings.md](./contracts/team-happenings.md), [quickstart.md](./quickstart.md)

**Tests**: Included. The spec asks for them by name — SC-003 ("verified by a test per members-only kind"), SC-004, SC-006, SC-009 — and constitution quality gates 3 and 7 require verification before sign-off.

**Organization**: Grouped by user story. **US2 is fully independent of the backend** and can ship on its own; US1 and US3 both build on Phase 2.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 / US2 / US3, mapping to the user stories in [spec.md](./spec.md)

## Path Conventions

Web app: `backend/` (.NET API) and `frontend/apps/web/` (Angular). Paths below are repo-relative and exact.

> **⚠ Standing constraint for every task**: this feature adds **no entity, no column, no migration, no dependency, and no write path**. If any task produces an `Add-Migration`, an `HttpClient`, or a `SaveChangesAsync`, stop — it has gone wrong. See [plan.md](./plan.md) Constitution Check.

---

## Phase 1: Setup

**Purpose**: Establish the baseline and the design gate before any code moves.

- [X] T001 Copy `.specify/templates/ui-review-checklist-template.md` to `specs/044-team-activity-feed/checklists/ui-review.md` — constitution gate 7 requires it for any UI-bearing change, and it is written against the diff, so it must exist before the diff does
- [X] T002 Record the baseline: run `dotnet test backend/JuggerHub.sln` and `npx nx test web`, confirm green, and confirm `dotnet ef migrations list` shows nothing pending — SC-002 and SC-009 are "unchanged" claims that need a known-good starting point

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The members-only read model and its endpoint. US1 renders it; US3 verifies its gate. Neither can begin until this is done.

**⚠️ CRITICAL**: US1 and US3 are blocked until this phase is complete. US2 is **not** blocked and may proceed in parallel from the start.

- [X] T003 Create `backend/Dtos/Teams/TeamHappeningDtos.cs` with `TeamHappeningKind` (4 members: `MemberJoined`, `RecognitionAwarded`, `TrainingSeriesCreated`, `TrainingSessionCancelled`), `TeamHappeningParamsDto` (`ActorName`, `RecognitionName`, `TrainingName`, `SessionDate`), and `TeamHappeningDto(Kind, Params, LinkTarget, OccurredAt)` per [data-model.md](./data-model.md) §1. **Do not reuse or extend `ActivityKind`/`ActivityEntryDto` from `backend/Dtos/Home/HomeDtos.cs`** — research [R1](./research.md#r1--do-not-reuse-activityentrydto-introduce-a-team-scoped-dto) explains why; carry that reasoning into an XML doc comment so the next reader does not "unify" them
- [X] T004 Create `backend/Services/Teams/ITeamHappeningService.cs` exposing `Task<IReadOnlyList<TeamHappeningDto>?> GetForTeamAsync(string slug, Guid userId, CancellationToken ct = default)`, documenting that `null` means "unknown team **or** not a member" — matching `ITeamActivityService`/`ITeamNewsService` and `TeamMembershipGuard`'s enumeration-neutral contract
- [X] T005 Implement `backend/Services/Teams/TeamHappeningService.cs`: resolve access via the existing `TeamMembershipGuard`, return `null` unless `IsMember`, then run the five `AsNoTracking()` projections in [data-model.md](./data-model.md) §2, merge, order, and cap. Declare `WindowDays = 30` and `MaxEntries = 10` as `private const` in **one** place (FR-011/FR-012) and apply `Take(MaxEntries)` per query *and* after the merge
- [X] T006 In `backend/Services/Teams/TeamHappeningService.cs`, project player identity with the `_db.PlayerProfiles.Where(p => p.UserId == …).Select(…).FirstOrDefault()` sub-projection used by `HomeService.LoadActivityAsync` — **never** `m.User.Profile!.DisplayName`. `PlayerProfiles` carries a ban `HasQueryFilter` (`backend/Data/AppDbContext.cs:149`). Leave `ActorName` **null** when absent; do **not** call `MemberPlaceholder` (research [R2](./research.md#r2--player-identity-sub-project-return-null-let-the-client-translate))
- [X] T007 In `backend/Services/Teams/TeamHappeningService.cs`, apply the per-kind `OccurredAt` columns and predicates exactly as tabulated in [data-model.md](./data-model.md) §2 — `JoinedDate`, `EarnedAt` (+ `Status == AwardStatus.Active` on both award tables), `Trainings.CreatedDate`, `CancelledDate` (+ `Status == TrainingSessionStatus.Cancelled`). **Read `Trainings`, not `TrainingSessions`, for the series kind** — `RecurrenceExpander.MaxSessions` is 520 (research [R3](./research.md#r3--training-read-trainingscreateddate-never-trainingsessionscreateddate))
- [X] T008 In `backend/Services/Teams/TeamHappeningService.cs`, apply the total ordering from [data-model.md](./data-model.md) §4: `OccurredAt` desc, then `Kind`, then a stable per-entry key (FR-015) — a series creation and its first cancellation can share a timestamp
- [X] T009 Add the `GET {slug}/happenings` action to `backend/Controllers/TeamsController.cs` beside `GetActivity`/`GetNews`: resolve the user id, call the service, map `null → TeamNotFound()`, return the bare list. **No `PaginationRequest` parameter and no `PagedResult<T>` envelope** — FR-013 and the recorded deviation in [plan.md](./plan.md#complexity-tracking)
- [X] T010 Register `builder.Services.AddScoped<ITeamHappeningService, TeamHappeningService>();` in `backend/Program.cs` next to the existing team service registrations (~line 331)
- [X] T011 Run the [quickstart.md](./quickstart.md) "Sanity check before any UI exists" curls — 200 for a member, 404 for a non-member, 404 for an unknown slug, 401 anonymous — before any frontend work starts

**Checkpoint**: The endpoint exists, is members-only, and returns bounded data. US1 and US3 unblocked.

---

## Phase 3: User Story 1 — A member catches up on their own team (Priority: P1) 🎯 MVP

**Goal**: A member opening the team page sees a "What's happening" card listing the last 30 days of joins, team awards, training series added, and sessions cancelled — newest first, in their own language.

**Independent Test**: Add a member to a team, cancel one of its training sessions, open `/t/<slug>` as a member. Both appear in the new card, newest first, and neither appears in the events card.

### Tests for User Story 1

- [X] T012 [P] [US1] Create `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamHappeningsTests.cs` with one test per kind — a join, a team badge, a team achievement, a training series, a cancelled session each produce exactly one correctly-shaped entry (FR-004…FR-007). Model the fixture setup on the existing `Home/ActivityFeedTests.cs`
- [X] T013 [P] [US1] In `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamHappeningsTests.cs`, add the **flood guard**: create a weekly recurring training spanning two years and assert the response contains **exactly one** entry for it (SC-004, guarantee G5). This is the single highest-value test in the feature
- [X] T014 [P] [US1] In `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamHappeningsTests.cs`, add the bounds tests: never more than 10 items, never an item older than 30 days, and both together (SC-005, G1/G2)
- [X] T015 [P] [US1] In `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamHappeningsTests.cs`, add the ordering test: two calls over unchanged data return an identical order, including entries sharing a timestamp (FR-015, G3)
- [X] T016 [P] [US1] In `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamHappeningsTests.cs`, add the self-correction tests: a departed member's join disappears, a revoked award disappears, and a banned member's entry survives with `actorName == null` (data-model §7, G6, FR-025)
- [X] T017 [P] [US1] In `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamHappeningsTests.cs`, assert **no** entry describes an event the team played and **no** entry describes a departure or role change (FR-008/FR-009, G7/G8)

### Implementation for User Story 1

- [X] T018 [P] [US1] Add `TeamHappening`, `TeamHappeningKind`, and `TeamHappeningParams` types to `frontend/apps/web/src/app/core/models/team.models.ts`, mirroring the contract in [contracts/team-happenings.md](./contracts/team-happenings.md)
- [X] T019 [US1] Add `getHappenings(slug: string): Observable<TeamHappening[]>` to `frontend/apps/web/src/app/core/services/team.service.ts` hitting `/api/v1/teams/{slug}/happenings` — no `skip`/`take` params (depends on T018)
- [X] T020 [P] [US1] Add the 7 new keys to `frontend/apps/web/public/i18n/en.json` under `teams.detail`: `happeningTitle`, `noHappenings`, and `happening.{someone,memberJoined,recognitionAwarded,trainingSeriesCreated,trainingSessionCancelled}`. Sentence case, no emoji, warm "you" voice per DESIGN.md
- [X] T021 [P] [US1] Add the same 7 keys to `frontend/apps/web/public/i18n/de.json`. Heading MUST be **"Was passiert gerade"**, **not** "Was ist los" — that string is the dashboard's `home.activityTitle`, and reusing it recreates the exact confusion issue #178 reports (research [R9](./research.md#r9--renaming-the-existing-card), SC-010)
- [X] T022 [P] [US1] Add the same 7 keys to `frontend/apps/web/public/i18n/es.json`
- [X] T023 [US1] Create `frontend/apps/web/src/app/features/teams/team-detail/happenings/team-happenings.component.ts` modelled on `frontend/apps/web/src/app/features/dashboard/modules/activity-list.component.ts`: `toSignal(langChanges$)` to re-translate on language switch, `computed` rows, a kind-keyed `text()` switch ending in `default: return ''`, `injectRelativeTime()`. Takes `items` **and** `slug` as inputs (research [R7](./research.md#r7--divergences-from-activitylistcomponent-deliberate-and-enumerated)); depends on T018
- [X] T024 [US1] Create `frontend/apps/web/src/app/features/teams/team-detail/happenings/team-happenings.component.html` and `.css`. **Unlike the dashboard's list it must render an empty state** (`jh-empty-state` with `noHappenings`) instead of nothing when there are no rows — FR-014. Use `jh-card` and match the sibling cards on the page
- [X] T025 [US1] Implement `link()` in `frontend/apps/web/src/app/features/teams/team-detail/happenings/team-happenings.component.ts` per [data-model.md](./data-model.md) §5: `['/u', handle]`, `['/trainings/sessions', id]`, `['/t', slug, 'trainings']` built from the slug input, and `null` for awards. Render plain text when there is no route (FR-022)
- [X] T026 [US1] Wire the card into `frontend/apps/web/src/app/features/teams/team-detail/team-detail.component.ts`: a `happenings` signal plus a `loadHappenings()` called from the existing member branch alongside `loadNews()` (~line 95-99), and import the new component
- [X] T027 [US1] Render the card in `frontend/apps/web/src/app/features/teams/team-detail/team-detail.component.html` inside the existing `@if (isMember())` block, placed **after** the News card — the dashboard's own rationale is that a passive activity log goes last so authored posts are never buried
- [X] T028 [P] [US1] Create `frontend/apps/web/src/app/features/teams/team-detail/happenings/team-happenings.component.spec.ts` modelled on `activity-list.component.spec.ts`: a sentence per kind, the translated stand-in when `actorName` is null, an unrecognised kind dropped rather than rendered blank, and the empty state shown when there are no rows

**Checkpoint**: US1 is fully functional. This is the MVP and closes issue #178's reported gap.

---

## Phase 4: User Story 2 — The team page stops contradicting itself (Priority: P2)

**Goal**: The card listing events the team played is headed with wording that names *events*, so it no longer oversells and no longer collides with the new card or the dashboard's feed.

**Independent Test**: Open the team page and confirm the two headings name two distinguishable things, in all three languages.

**⚠ Independent of Phase 2** — pure i18n and template. Can be done first, last, or in parallel by another person.

- [X] T029 [P] [US2] In `frontend/apps/web/public/i18n/en.json`, rename `teams.detail.recentActivity` → `recentEvents` ("Recent events") and `teams.detail.noActivity` → `noEvents` ("No events yet.")
- [X] T030 [P] [US2] In `frontend/apps/web/public/i18n/de.json`, apply the same rename with "Letzte Events" / "Noch keine Events."
- [X] T031 [P] [US2] In `frontend/apps/web/public/i18n/es.json`, apply the same rename with "Eventos recientes" / "Aún no hay eventos."
- [X] T032 [US2] Update the two usages in `frontend/apps/web/src/app/features/teams/team-detail/team-detail.component.html` (lines ~152 and ~163) to the renamed keys. **Change nothing else in that card** — its data, cap of 6, and ordering are frozen by FR-016 (depends on T029-T031)
- [X] T033 [US2] Run `npx nx test web` and confirm `frontend/apps/web/src/app/core/i18n/catalog-parity.spec.ts` passes — it walks the whole catalogue, so a rename applied to one language and not another fails here. **Run it; do not assume it covers this**

**Checkpoint**: US1 and US2 both work. The page reads coherently in all three languages.

---

## Phase 5: User Story 3 — A non-member's view is unchanged (Priority: P3)

**Goal**: A signed-in non-member sees exactly what they saw before, and nothing about the team's internal life leaks to them.

**Independent Test**: Load a team page as a signed-in non-member before and after and compare.

- [X] T034 [P] [US3] In `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamHappeningsTests.cs`, assert a signed-in non-member gets `404`, an unknown slug gets `404`, and the two responses are **indistinguishable** — team existence must not be disclosed (FR-003, SC-003, G4)
- [X] T035 [P] [US3] In `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamHappeningsTests.cs`, assert an anonymous caller gets `401` from the global `FallbackPolicy` — confirming no `[AllowAnonymous]` crept onto the action
- [X] T036 [P] [US3] In `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamHappeningsTests.cs`, add the leak test: a team-only training with a cancelled session must produce **nothing** reachable by a non-member — no name, no date, no location (FR-012 equivalent, SC-003)
- [X] T037 [US3] In `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamHappeningsTests.cs`, assert `GET /teams/{slug}/public` returns a `recentActivity` array with the same contents and cap for a non-member as before this feature (FR-016, SC-002)
- [ ] T038 [US3] Verify in the browser as a signed-in non-member that the card is **absent entirely**, not present-and-empty (FR-002) — the `@if (isMember())` placement from T027 is what makes this true, and it is worth eyeballing rather than inferring

**Checkpoint**: All three stories independently functional and verified.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T039 [P] Verify the dashboard feed is unchanged: run the existing `backend/tests/JuggerHub.Api.IntegrationTests/Home/ActivityFeedTests.cs` and `frontend/apps/web/src/app/features/dashboard/modules/activity-list.component.spec.ts` (FR-027, SC-009)
- [X] T040 [P] Verify `GET /teams/{slug}/activity` and the profile activity surfaces still behave identically — existing tests in `backend/tests/JuggerHub.Api.IntegrationTests/Teams/` and `Profile/` (FR-018, FR-028)
- [X] T041 Complete `specs/044-team-activity-feed/checklists/ui-review.md` against the diff. Focus on the **awards overlap** (FR-019/FR-020) — for a member one award now appears in both the dated card and the undated "Badges & achievements" card; they must read as a log and a trophy shelf, not two happenings. Confirm the standing-collection card gained **no** dates
- [ ] T042 Verify at **375 px** that every kind renders without horizontal scrolling, including the longest German wording (SC-007)
- [ ] T043 Run the full [quickstart.md](./quickstart.md) validation end to end, including the bounds and self-correction tables
- [X] T044 [P] Raise the follow-up GitHub issue for departures, removals, and role changes — excluded by decision D1 because no record survives them. Reference #178 and note that recording must be added before a feed can show them, so the exclusion is tracked outside this spec
- [X] T045 Update `specs/044-team-activity-feed/spec.md` with any drift discovered during implementation, and comment on issue #178 with the outcome — specifically that the merge it proposed was replaced by the two-section split (D5)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: no dependencies
- **Phase 2 (Foundational)**: after Setup — **blocks US1 and US3**, does **not** block US2
- **Phase 3 (US1)**: after Phase 2
- **Phase 4 (US2)**: after Phase 1 only — genuinely independent
- **Phase 5 (US3)**: after Phase 2; T037/T038 also want US1's UI in place to be meaningful
- **Phase 6 (Polish)**: after all desired stories

### Within Phase 2

T003 → T004 → T005, then T006/T007/T008 all edit `TeamHappeningService.cs` (**sequential, same file**), then T009 → T010 → T011.

### Within User Story 1

- T012–T017 all edit the same test file — **sequential despite the [P] marks on distinct concerns**; treat [P] there as "independent to reason about", not "safe to write concurrently"
- T018 → T019 (models before service)
- T020/T021/T022 are three different catalogue files — genuinely parallel
- T023 → T024 → T025 (same component, sequential) → T026 → T027
- T028 after T023–T025

### Parallel Opportunities

- **US2 (T029–T033) can run start-to-finish alongside everything else** — it touches only the three catalogues and two lines of the team-detail template. Coordinate with T020–T022, which edit the same three catalogue files
- T020, T021, T022 in parallel (three files)
- T029, T030, T031 in parallel (three files)
- T039, T040, T044 in parallel during polish

> ⚠ **The one real collision**: T020–T022 (add happening keys) and T029–T031 (rename event keys) edit the same three `i18n/*.json` files. Do one set, then the other — or do both edits per file in a single pass.

---

## Parallel Example: User Story 1 i18n

```bash
# Three different catalogue files — safe to run together:
Task: "Add 7 happening keys to frontend/apps/web/public/i18n/en.json"
Task: "Add 7 happening keys to frontend/apps/web/public/i18n/de.json"
Task: "Add 7 happening keys to frontend/apps/web/public/i18n/es.json"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 (Setup) → Phase 2 (Foundational) → Phase 3 (US1)
2. **STOP and VALIDATE**: a member sees joins, awards, series, and cancellations on the team page
3. This alone closes issue #178's reported gap

### Incremental Delivery

1. Setup + Foundational → the endpoint exists and is gated
2. US1 → the section renders → **MVP, demo-able**
3. US2 → the two headings stop colliding → ship together with US1 if at all possible; adding a second section without renaming the first makes the page *more* confusing, not less
4. US3 → the safety property is verified by tests rather than assumed
5. Polish → design review, follow-up issue, quickstart

### Recommended shipping unit

**US1 + US2 together.** US2 is three catalogue edits and two template lines, and shipping US1 without it leaves the page with two similarly-named sections — the very complaint issue #178 opens with.

---

## Notes

- `[P]` = different files, no dependencies. Where several `[P]` tasks name the **same** file (the integration test file, the service file), they are independent *concerns* but must be written sequentially
- Commit after each task or logical group; keep the backend and frontend halves of a story in separate commits where they stand alone
- **No migration, no new dependency, no outbound HTTP, no write path** — if a task seems to need one, re-read [plan.md](./plan.md) before proceeding
- Constitution gate 7 (T001, T041) is not optional for this feature; it ships UI


---

## Outstanding after the 2026-08-11 implementation pass

**43 of 45 tasks complete.** Backend and frontend are implemented and verified by automated tests.
What remains needs either a running application or the owner's go-ahead.

| Task | Why it is still open |
|---|---|
| **T038** | Needs the app running — confirm as a signed-in non-member that the card is absent, not present-and-empty. The server side *is* covered by `Non_member_and_unknown_team_are_indistinguishable` and `A_team_only_trainings_cancellation_never_reaches_an_outsider`. |
| **T042** | Needs the app running — 375px check with the longest German wording. |
| **T043** | Needs the app running — full quickstart walkthrough. |
| ~~T044~~ | **Done** — raised as #184. |
| ~~T045~~ | **Done** — spec.md §Implementation Notes & Drift, plus the outcome comment on #178. Shipped as PR #183. |

The UI review checklist (`checklists/ui-review.md`) is filled in: 33 items verified by code
inspection, and the items needing a rendered page — including **CHK030** (the awards overlap) and
**CHK036** (375px German) — are explicitly recorded as outstanding rather than passed.

### Verification actually run

| Suite | Result |
|---|---|
| Backend, full solution | **808 passed, 0 failed** |
| Backend, feature tests | 16 passed (`TeamHappeningsTests`) |
| Frontend, full Jest suite | **434 passed / 65 suites, 0 failed** |
| Frontend, feature spec | 7 passed (`TeamHappeningsComponent`) |
| Angular compiler (`ngc --noEmit`) | clean, including template type-checking |
| ESLint on changed files | 0 errors (1 warning, identical to the sibling spec it mirrors) |

> ⚠ **`nx` is unreliable in this git worktree.** `nx test` / `nx build` resolve the workspace root to
> the *main* checkout and report that tree's results — they showed a green "449 passed / 67 suites"
> while this branch's code had never run, and `nx build` wrote its output into the main repo.
> Verification here used Jest and `ngc` directly. Worth a follow-up so CI and local runs cannot
> diverge silently.
