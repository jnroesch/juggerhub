---
description: "Task list for feature: Remove the Player-Search Opt-Out"
---

# Tasks: Remove the Player-Search Opt-Out

**Input**: Design documents from `specs/020-remove-search-optout/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/)

**Tests**: This feature adds no new test suites; it **updates existing tests** that
encode the opt-in invariant. Those updates are mandatory (the suites won't compile /
pass otherwise) and are listed as implementation tasks under US1.

**Organization**: Grouped by user story. Note the shape of this change: the backend
removal is a single **compile-coherent** unit (C# won't build with references to a
removed property), so it lands together in Phase 2 (Foundational). US1 then verifies
the resulting directory behavior; US2 is the independent frontend removal.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 / US2 (Setup, Foundational, Polish carry no story label)

## Path Conventions

Web app: backend at `backend/`, frontend at `frontend/apps/web/src/`.

---

## Phase 1: Setup

**Purpose**: Establish a clean baseline before removing anything.

- [X] T001 Confirm baseline is green on the feature branch: `dotnet build` + `dotnet test` (backend) and `npx nx lint web` + `npx nx test web` (frontend) all pass before changes.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The atomic backend removal of `AppearInSearch`. These edits must land
together because the project will not compile while any reference remains; the
migration is generated only after the model is clean. This phase delivers the
inclusive-directory behavior (US1) and removes the persisted/transmitted field
(FR-002).

**⚠️ CRITICAL**: No downstream verification (US1 tests) can pass until this compiles.

- [X] T002 Remove the `AppearInSearch` property (and its XML-doc block) from `backend/Entities/PlayerProfile.cs`.
- [X] T003 [P] Remove the `AppearInSearch` model configuration in `backend/Data/AppDbContext.cs`: delete `entity.Property(p => p.AppearInSearch).HasDefaultValue(false)` and the partial `entity.HasIndex(p => p.AppearInSearch).HasFilter("\"AppearInSearch\"")`. Leave the global ban query filter, `Handle`/`UserId` indexes, and all unrelated config untouched.
- [X] T004 [P] Remove the opt-in gate in `backend/Services/Search/PlayerSearchService.cs`: change `_db.PlayerProfiles.AsNoTracking().Where(p => p.AppearInSearch)` to `_db.PlayerProfiles.AsNoTracking()`, and update the class/interface XML-doc that describes the "only opted-in players are returned" privacy invariant to state the directory now returns all non-banned players.
- [X] T005 [P] Remove the `AppearInSearch` parameter from `OwnerProfileDto` and `UpdateProfileRequest` in `backend/Dtos/Profile/ProfileDtos.cs`.
- [X] T006 [P] Remove `AppearInSearch` from `backend/Services/Profile/ProfileService.cs`: the `GetOwnerAsync` projection field and its argument to `OwnerProfileDto`, the `profile.AppearInSearch = request.AppearInSearch` write in `UpdateAsync`, and the field on the private `ProfileProjection` record (so both owner and public projections use the same shape).
- [X] T007 [P] Remove `AppearInSearch` seeding in `backend/Data/DevDataSeeder.cs`.
- [X] T008 Generate the migration: `dotnet ef migrations add RemoveAppearInSearch`. Verify `Up` drops index `IX_PlayerProfiles_AppearInSearch` and column `AppearInSearch`; `Down` re-adds the column (`boolean, nullable:false, defaultValue:false`) and the partial index; confirm the migration does NOT touch the `unaccent` extension, `Teams.BeginnersWelcome`, or `IX_Events_Status`, and that the model snapshot updated.
- [X] T009 Build the backend analyzer-clean (`dotnet build`, warnings-as-errors) confirming no dangling `AppearInSearch` reference remains.

**Checkpoint**: Backend compiles; directory query is unfiltered; column + field gone.

---

## Phase 3: User Story 1 - Every player is discoverable in the directory (Priority: P1) 🎯 MVP

**Goal**: The anonymous player directory returns every non-banned player, with no
per-player visibility filtering.

**Independent Test**: Browse/search the directory with a previously-hidden player and
a banned player present; the previously-hidden player appears, the banned player does
not, and the suspended player appears.

- [X] T010 [US1] Update `backend/tests/JuggerHub.Api.IntegrationTests/Search/SearchTestSupport.cs`: remove the `appearInSearch` parameter and the `profile.AppearInSearch = appearInSearch` assignment; the helper now just ensures the profile exists (searchable by default).
- [X] T011 [US1] Update `backend/tests/JuggerHub.Api.IntegrationTests/Search/PlayerBrowseTests.cs`: replace the opt-in-invariant assertions (previously-hidden player absent) with assertions that all non-banned players appear; remove/replace the "opt-in is a hard invariant" case (feature 007 SC-003) with an "all players appear" case; update helper calls to the new `SearchTestSupport` signature.
- [X] T012 [US1] Update `backend/tests/JuggerHub.Api.IntegrationTests/Admin/AccountEnforcementTests.cs`: remove the `ExecuteUpdateAsync(... SetProperty(p => p.AppearInSearch, true) ...)` setup (~line 76); the player is searchable by default, so the banned-excluded / suspended-visible assertions keep their intent.
- [X] T013 [US1] Run the backend Search + Admin integration suites (`dotnet test`) and confirm green.

**Checkpoint**: US1 fully functional and independently verified.

---

## Phase 4: User Story 2 - The profile no longer offers a search-visibility toggle (Priority: P1)

**Goal**: The owner profile has no "appear in search" toggle or status text, and the
owner profile data carries no visibility preference.

**Independent Test**: Open the owner profile edit view — no toggle and no
appear/hidden text; saving succeeds and `GET /api/v1/profiles/me` returns no
`appearInSearch`. (This story is independent of the backend phase and may be worked
in parallel; it must ship together with US1.)

- [X] T014 [US2] Remove `appearInSearch` from `OwnerProfile` and from `UpdateProfileRequest` in `frontend/apps/web/src/app/core/models/profile.models.ts`.
- [X] T015 [P] [US2] Remove the `appearInSearch` signal, the `toggleAppearInSearch()` method, its reads in `reload()` and `cancelEdit()`, and its inclusion in the `updateMine({...})` save payload in `frontend/apps/web/src/app/features/profile/profile-owner/profile-owner.component.ts`.
- [X] T016 [P] [US2] Remove the toggle button (the `role="switch"` block) and the "You appear in / are hidden from player search" status text from `frontend/apps/web/src/app/features/profile/profile-owner/profile-owner.component.html`, tidying the surrounding section (no orphaned divider/heading).
- [X] T017 [US2] Run `npx nx lint web` and `npx nx test web`; confirm the owner-profile component builds and passes with the field removed.

**Checkpoint**: US2 fully functional and independently verified.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Reconcile the source-of-truth specs and run whole-feature verification.

- [X] T018 [P] Amend feature 007 docs: add a dated "Amended by 021" note to `specs/007-search/spec.md` retiring the player opt-in invariant (FR-041/FR-042 and the 100%-invariant SC-003), remove `appearInSearch` from `specs/007-search/contracts/openapi.yaml`, and note the change in `specs/007-search/data-model.md`.
- [X] T019 [P] Amend feature 003 docs: add a dated "Amended by 021" note to `specs/003-profile/spec.md` for the removed profile field (and its DTO representation).
- [X] T020 Instantiate `specs/020-remove-search-optout/checklists/ui-review.md` from `.specify/templates/ui-review-checklist-template.md` and verify the owner-profile edit view against DESIGN.md after the toggle removal (Quality Gate 7).
- [X] T021 Run the [quickstart.md](quickstart.md) validation scenarios end-to-end (previously-hidden player appears; owner view has no control; banned hidden / suspended visible; count/results agree).
- [X] T022 Final verification: backend `dotnet build` + `dotnet test`, frontend `npx nx lint web` + `npx nx test web`, and confirm the `RemoveAppearInSearch` migration applies cleanly (`dotnet ef database update` on a fresh DB).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: none — start immediately.
- **Foundational (Phase 2)**: after Setup. Its atomic backend edits (T002–T007) precede migration (T008) and build (T009). **Blocks US1 verification.**
- **US1 (Phase 3)**: after Phase 2 (needs the compiled, gate-free backend).
- **US2 (Phase 4)**: independent of Phase 2 at the code level (frontend-only) and may run in parallel; must ship together with US1.
- **Polish (Phase 5)**: after US1 and US2 are complete.

### Within Phase 2

- T002–T007 are different files with no ordering constraint among themselves (all remove `AppearInSearch` references); T003–T007 are marked [P]. ALL must complete before T008 (migration) and T009 (build).

### Within US1

- T010 (test support) before T011/T012 (which call it); T013 after T010–T012.

### Within US2

- T014 (model) before T015 (component consumes the model type); T015 and T016 are different files ([P]); T017 after T014–T016.

### Parallel Opportunities

- Phase 2: T003, T004, T005, T006, T007 in parallel (distinct files), with T002 alongside.
- US1 vs US2: the entire frontend story (US2) can proceed in parallel with the backend (Phase 2 + US1), by different developers.
- Polish: T018 and T019 in parallel (distinct spec trees).

---

## Parallel Example: Phase 2 (backend removal)

```bash
# After T002, remove the remaining references across distinct files together:
Task: "T003 Remove AppearInSearch model config in backend/Data/AppDbContext.cs"
Task: "T004 Remove the browse gate in backend/Services/Search/PlayerSearchService.cs"
Task: "T005 Remove field from DTOs in backend/Dtos/Profile/ProfileDtos.cs"
Task: "T006 Remove reads/writes in backend/Services/Profile/ProfileService.cs"
Task: "T007 Remove seeding in backend/Data/DevDataSeeder.cs"
# Then T008 (generate migration) and T009 (build) sequentially.
```

---

## Implementation Strategy

### MVP scope

Both stories are P1 and ship together, but the natural MVP is **Phase 2 + US1**
(the directory becomes inclusive and the field is gone server-side) — independently
demonstrable via the browse API. US2 (removing the now-inert toggle) completes the
user-facing change and must land in the same release so no dead control ships.

### Recommended order

1. Phase 1 (baseline green).
2. Phase 2 (atomic backend removal + migration) → build green.
3. US1 test updates → backend suites green.  ← backend MVP
4. US2 frontend removal → frontend green (can be done in parallel from step 2).
5. Polish: spec amendments, UI review (Gate 7), quickstart, final full verification.

---

## Notes

- [P] = different files, no dependency. Story labels US1/US2 are for traceability.
- Commit after each task or logical group; the migration (T008) is a natural commit boundary.
- No new tests are added; existing opt-in tests are updated in place (US1).
- FR-004/SC-004 (backward tolerance) were retired — there is deliberately **no**
  tolerance task.
- Security note: removing the browse gate is safe because banned players stay hidden
  via the global `User.Status != Banned` query filter (see [research.md](research.md) R1).
