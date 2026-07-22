---
description: "Task list for Home Participation Makeover (feature 025)"
---

# Tasks: Home Participation Makeover

**Input**: Design documents from `specs/025-home-participation-makeover/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/home-api.md](./contracts/home-api.md), [quickstart.md](./quickstart.md)

**Tests**: Included — this repo tests home heavily (existing `dashboard.component.spec.ts`, `home.service.spec.ts`, backend integration + Playwright e2e), and quickstart.md defines per-story validation scenarios.

**Organization**: Grouped by user story. Because all sections live in the one composite (`GET /api/v1/home` / `HomeService`) and the one dashboard component, the shared DTO reshape and section removals are in **Foundational** (they change the contract every story builds on); each story then implements population + UI for its section.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1–US5 (maps to spec.md user stories)

## Path Conventions

- Backend: `backend/` (.NET 10, EF Core)
- Frontend: `frontend/apps/web/src/app/` (Angular, Nx)
- Backend tests: `backend/tests/JuggerHub.Api.IntegrationTests/`

---

## Phase 1: Setup

**Purpose**: Config + review scaffolding. No new project.

- [x] T001 [P] Instantiate the UI review checklist by copying `.specify/templates/ui-review-checklist-template.md` to `specs/025-home-participation-makeover/checklists/ui-review.md` (Quality Gate 7)
- [x] T002 [P] Add caps to `backend/Services/Home/HomeOptions.cs`: `NearWindowDays` (default 14), `ActivityCap`, `NeedsYouCap`; remove `TournamentCap`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Reshape the shared contract and remove dead sections so every story has a compiling base. **⚠️ No user story work can begin until this phase is complete.**

- [x] T003 Reshape `backend/Dtos/Home/HomeDtos.cs`: add `NeedsYouItemDto` + `NeedsYouKind`, `AgendaItemDto` + `AgendaKind`, `ActivityEntryDto` + `ActivityKind`; change `HomeDto` to `{ Viewer, Teams, NeedsYou, UpNext (AgendaItemDto), OpenToEveryone (AgendaItemDto), News, Activity }`; delete `TournamentCardDto`, `TeamSnapshotDto`, `TeamActivityDto`, `NextFixtureDto` (per [data-model.md](./data-model.md))
- [x] T004 Rework `backend/Services/Home/HomeService.cs` `GetHomeAsync` to the new shape: remove the tournaments, snapshots, and teams-activity queries; keep viewer/teams/news/openToEveryone; return **empty** `NeedsYou` and `Activity` for now so the project compiles and existing news/up-next still paint
- [x] T005 [P] Mirror the reshaped contract in `frontend/apps/web/src/app/core/models/home.models.ts`: add `NeedsYouItem`, `AgendaItem`, `ActivityEntry` interfaces and `NeedsYouKind`/`AgendaKind`/`ActivityKind` unions; remove `TournamentCard`/`TeamSnapshot`/`NextFixture`/`TeamActivity`; add `"party"` to `NewsSource`; reshape `Home`
- [x] T006 [P] Reorder `frontend/apps/web/src/app/features/dashboard/dashboard.component.html` (+ `.ts` imports) to the four-section skeleton (Needs you → Up next → News → What's going on); delete the tournaments, team-snapshots, and your-teams-activity markup; preserve greeting, loading skeleton, and failure/retry
- [x] T007 Grep the repo for `TournamentCardDto`, `TeamSnapshotDto`, `TeamActivityDto`, `NextFixtureDto`, `Tournaments`, `Snapshots`, `TeamsActivity` and remove/repoint any lingering references (backend + `home.service.ts`)

**Checkpoint**: Home compiles and renders greeting + Up next (events only) + News; new sections are empty placeholders.

---

## Phase 3: User Story 1 — Needs you (Priority: P1) 🎯 MVP

**Goal**: A pinned-top actionable block consolidating team invites, party requests, marketplace invites/applications, and near-window un-answered trainings; resolvable in place; hidden when empty.

**Independent Test**: Seed a viewer with a team invite, a marketplace invite, a marketplace application, and an un-answered training within ~14 days → all appear at top with inline actions; resolving each removes it; resolving all hides the block; chat messages never appear.

- [x] T008 [P] [US1] Integration test in `backend/tests/JuggerHub.Api.IntegrationTests/Home/NeedsYouTests.cs`: aggregation of all actionable types; **stale/read notification does not produce a needs-you row** (authoritative-source check per research R1); empty when nothing pending
- [x] T009 [US1] Confirm and pin the feature-016 **party-participation-request** pending query surface (research R1 open item) — identify the service/entity that backs the `PartyRequest` "I'm-in/can't-make-it" action and expose a pending-for-viewer read
- [x] T010 [US1] Implement needs-you aggregation in `backend/Services/Home/HomeService.cs`: team invites, party participation requests (T009), party co-admin invites (`IPartyInvitationService`), marketplace invites+applications (`IMarketRequestService.ListMineAsync`), and near-window un-answered trainings (`ITrainingResponseService.GetMyAgendaAsync` filtered `myAnswer == null` && within `NearWindowDays`); project to `NeedsYouItemDto`, cap at `NeedsYouCap`
- [x] T011 [P] [US1] Create `frontend/apps/web/src/app/features/dashboard/modules/needs-you-card.component.{ts,html,css}`: render each `NeedsYouKind` with inline actions (accept/decline, going/maybe/can't; application shown pending), removing a row on resolve without full reload
- [x] T012 [US1] Wire needs-you-card into `dashboard.component.html` as the top section, hidden entirely when the list is empty (FR-005); actions call the existing per-domain endpoints (invitation/party/market/training services)
- [x] T013 [US1] Remove the folded `frontend/apps/web/src/app/features/dashboard/modules/market-card.component.*` and its references (superseded by Needs you)
- [x] T014 [P] [US1] Component spec `frontend/apps/web/src/app/features/dashboard/modules/needs-you-card.component.spec.ts` (zoneless — no `fakeAsync`): renders each kind, resolves an item, hides when empty

**Checkpoint**: Needs you is fully functional and independently testable — the MVP.

---

## Phase 4: User Story 2 — Unified Up next (Priority: P1)

**Goal**: One chronological agenda combining the viewer's events (individual + team-mode) and trainings, soonest-first, inline actions per kind; no unrelated events; multi-team dedup.

**Independent Test**: Seed an individual event, a team-mode event, and an answered training at interleaved dates plus an unrelated tournament → all three participation items appear in one ordered list with correct inline controls; the tournament appears nowhere; a two-team viewer sees a shared event once.

- [x] T015 [P] [US2] Integration test `backend/tests/JuggerHub.Api.IntegrationTests/Home/UpNextAgendaTests.cs`: unified ordering (events+trainings), event dedup for multi-team viewer, near-window un-answered trainings **excluded** (they belong to Needs you), unrelated tournament absent
- [x] T016 [US2] Implement the unified agenda in `backend/Services/Home/HomeService.cs` `LoadUpNextAsync`: merge event rows with `GetMyAgendaAsync` training rows into `AgendaItemDto` (`Kind` discriminator), exclude near-window un-answered trainings, de-dup by event, order by `StartsAt`
- [x] T017 [US2] Update `ListUpNextAsync` (`GET /home/up-next` see-all) to return `PagedResult<AgendaItemDto>` with the same unified merge
- [x] T018 [US2] Extend `frontend/apps/web/src/app/features/dashboard/modules/up-next-card.component.{ts,html}` to branch on `AgendaItem.kind`: Event (RSVP/withdraw toggle, team-going read-only) vs Training (going/maybe/can't)
- [x] T019 [US2] Update the see-all page `frontend/apps/web/src/app/features/dashboard/see-all/up-next-list.component.*` to render unified `AgendaItem`s
- [x] T020 [US2] Remove the folded `frontend/apps/web/src/app/features/dashboard/modules/your-trainings-card.component.*` and its references (superseded by Up next / Needs you)
- [x] T021 [P] [US2] Update `frontend/apps/web/src/app/features/dashboard/modules/up-next-card.component.spec.ts` to cover both Event and Training rendering + actions

**Checkpoint**: Up next shows one participation timeline; US1 and US2 both work independently.

---

## Phase 5: User Story 3 — News with party posts (Priority: P2)

**Goal**: News aggregates team + event + **party** authored posts, newest-first, party posts gated to `In` members; never contains system activity.

**Independent Test**: Seed a team, an event, and a party news post (viewer `In`) → all three appear tagged by source; a rescheduled training appears only in What's going on, never in News; a non-`In` party's news is invisible.

- [x] T022 [P] [US3] Integration test `backend/tests/JuggerHub.Api.IntegrationTests/Home/NewsPartyTests.cs`: three-source merge, `In`-member gating (FR-023), and disjointness from activity (SC-004)
- [x] T023 [US3] Add `PartyNewsPost` to `HomeService.LoadNewsAsync` for parties where the viewer is `PartyMemberStatus.In`, tagged `source = "party"` with the party/event link target; applies to composite **and** `/home/news` see-all
- [x] T024 [US3] Render the `"party"` source in `frontend/apps/web/src/app/features/dashboard/modules/news-list.component.{ts,html}` (label + link target)

**Checkpoint**: News carries party posts; still free of system noise.

---

## Phase 6: User Story 4 — What's going on (Priority: P3)

**Goal**: A quiet, read-only activity feed as the last section: participation/social signals + pure state-changes, scoped to the viewer's teams/parties, no actions, disjoint from News.

**Independent Test**: Seed a teammate event sign-up, a new team member, a badge award, and a rescheduled training → all appear as read-only entries at the bottom with no action controls; no authored news post appears here.

- [x] T025 [P] [US4] Integration test `backend/tests/JuggerHub.Api.IntegrationTests/Home/ActivityFeedTests.cs`: each source appears, scoping to the viewer's teams/parties, entries carry no actions, disjoint from News, capped/ordered
- [x] T026 [US4] Implement derive-on-read activity in `backend/Services/Home/HomeService.cs`: teammate event sign-ups (`EventSignup` in myTeams, excl. self), new team members (`TeamMembership` in myTeams, excl. self), badges (`BadgeAward` to viewer or teammates), party member joins (`PartyMember` in viewer's parties) — bounded windows, projected to `ActivityEntryDto`
- [x] T027 [US4] Add pure state-change entries from the viewer's passive notification rows (`TeamRoleChanged`, `TrainingUpdated`), then merge all sources newest-first and cap at `ActivityCap`; exclude the news types (`TeamNews`/`PartyNews`/`EventNews`) to keep streams disjoint
- [x] T028 [P] [US4] Create `frontend/apps/web/src/app/features/dashboard/modules/activity-list.component.{ts,html,css}`: read-only entries per `ActivityKind`, no action controls
- [x] T029 [US4] Wire activity-list as the last dashboard section with an empty state / hidden-when-empty

**Checkpoint**: All four sections present; News and activity fully disjoint.

---

## Phase 7: User Story 5 — No-team variant preserved (Priority: P2)

**Goal**: A no-team viewer still gets the find-a-team prompt and an open-to-everyone joinable list; team/party-only sections stay absent — no regression under the reshaped contract.

**Independent Test**: Load home with no memberships → find-a-team + open-to-everyone render; Needs you / team-mode agenda / team+party news / activity are empty/absent.

- [x] T030 [P] [US5] Integration test `backend/tests/JuggerHub.Api.IntegrationTests/Home/NoTeamVariantTests.cs`: `openToEveryone` populated as `AgendaItemDto` (Kind=Event); team/party-scoped sections empty
- [x] T031 [US5] Populate `OpenToEveryone` as `AgendaItemDto` (Kind=Event) in `HomeService` for no-team viewers (adapt the existing `LoadOpenToEveryoneAsync` projection to the new item type)
- [x] T032 [US5] Verify/adjust the no-team branch in `dashboard.component.html`: find-a-team prompt + open-to-everyone list render with the new `AgendaItem` type; team/party sections hidden

**Checkpoint**: Both viewer variants correct under the new contract.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [x] T033 [P] Update `frontend/apps/web/src/app/core/services/home.service.spec.ts` and `frontend/apps/web/src/app/features/dashboard/dashboard.component.spec.ts` for the reshaped contract and section order
- [x] T034 [P] Update Playwright e2e in `frontend/apps/web-e2e/` for the four sections and no-team variant (heed the responsive dual-render strict-mode gotcha: scope with `.filter({ visible })`/`.first()`)
- [x] T035 Complete `specs/025-home-participation-makeover/checklists/ui-review.md` against the diff — verify layout, responsiveness, states, and accessibility; DESIGN.md wins on any conflict (Quality Gate 7)
- [x] T036 Run `specs/025-home-participation-makeover/quickstart.md` validation scenarios end-to-end (all user stories + cross-cutting)
- [x] T037 [P] Final cleanup: remove dead model/DTO references, confirm new enums serialize by name (global `JsonStringEnumConverter`), confirm no unbounded lists were introduced (composite caps + paginated see-alls)

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (P1)**: no dependencies.
- **Foundational (P2)**: depends on Setup; **blocks all user stories** (reshapes the shared contract).
- **User Stories (P3–P7)**: all depend on Foundational. They touch the same `HomeService.cs` and `dashboard.component.html`, so parallelizing across stories risks merge conflicts — prefer **sequential in priority order** (US1 → US2 → US3 → US4 → US5). Frontend module files (needs-you-card, activity-list) are story-isolated and safe to parallelize.
- **Polish (P8)**: depends on all desired stories.

### Within each story

- Integration test → backend population → frontend rendering → module removal/cleanup.
- Models/contract already exist from Foundational.

### Parallel opportunities

- Setup: T001, T002 together.
- Foundational: T005 (models) + T006 (dashboard shell) run parallel to backend T003/T004; T007 after.
- Per story, the `[P]` test task and the new-component file task can run alongside the backend population task (different files). Backend tasks within one story are sequential (same `HomeService.cs`).

---

## Implementation Strategy

### MVP

- **Minimum**: Setup + Foundational + **US1 (Needs you)** → the highest-value slice; stop and validate.
- **P1 milestone**: add **US2 (Up next)** → the participation core (both P1 stories). Demo-ready home.

### Incremental delivery

1. Setup + Foundational → contract reshaped, home still paints.
2. US1 → Needs you → validate → demo (MVP).
3. US2 → Unified Up next → validate → demo.
4. US3 → Party news → validate.
5. US4 → What's going on → validate.
6. US5 → No-team variant confirmed → validate.
7. Polish → UI review + quickstart + e2e.

### Notes

- Sequential backend order avoids `HomeService.cs` conflicts; keep each section's population in its own private helper for clean diffs.
- Commit after each task or logical group; stop at any checkpoint to validate a story independently.
- No database migration is expected (all reads from existing entities) — flag immediately if any task appears to require one.
