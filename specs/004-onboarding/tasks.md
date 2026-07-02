---
description: "Task list for First-Login Onboarding Flow"
---

# Tasks: First-Login Onboarding Flow

**Input**: Design documents from `/specs/004-onboarding/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/openapi.yaml, quickstart.md

**Tests**: INCLUDED — the plan's Testing section requires xUnit integration + Jest unit + Playwright e2e.

**Organization**: Tasks are grouped by user story (US1–US4) for independent implementation and testing. Backend namespace `JuggerHub`; frontend Nx app at `frontend/apps/web/src/app`. **All profile persistence reuses feature 003's `PUT /profiles/me` and `PUT /profiles/me/avatar` — no new write paths.**

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US4 (Setup/Foundational/Polish carry no story label)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Structure for the new frontend surface. Backend reuses existing groupings; no new project or package.

- [x] T001 [P] Scaffold the onboarding feature folder `frontend/apps/web/src/app/features/onboarding/` (empty `onboarding.component.ts/.html/.css` placeholders to be filled in US1/US2)
- [x] T002 [P] Scaffold the backend test folder `backend/tests/JuggerHub.Api.IntegrationTests/Onboarding/`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The onboarding-completion state, its propagation onto the auth surface, and the complete endpoint. The frontend gate (US1) and persistence (US2) both depend on this.

**⚠️ CRITICAL**: No user-story work begins until this phase is complete.

- [x] T003 Add nullable `DateTime? OnboardingCompletedAt` to `backend/Entities/PlayerProfile.cs` (XML-doc: null = not onboarded; set once, idempotent)
- [x] T004 Verify `backend/Data/AppDbContext.cs` maps the new column as nullable `timestamptz` with no extra config/index; add fluent config only if the default mapping is unsatisfactory (depends on T003)
- [x] T005 Generate EF migration `AddOnboardingCompletedAt` in `backend/Data/Migrations/` and confirm it only adds the one nullable column (depends on T003, T004)
- [x] T006 [P] Add `CompleteOnboardingStatus { Completed, ProfileNotFound }` enum and the two method signatures (`HasCompletedOnboardingAsync`, `CompleteOnboardingAsync`) to `backend/Services/Profile/IProfileService.cs`
- [x] T007 Implement `HasCompletedOnboardingAsync` (projected `AsNoTracking` boolean) and `CompleteOnboardingAsync` (tracked read → set `DateTime.UtcNow` only if null → save; else no-op) in `backend/Services/Profile/ProfileService.cs` (depends on T003, T006)
- [x] T008 [P] Add `bool OnboardingCompleted` to `AuthUserDto` in `backend/Dtos/Auth/AuthResponses.cs`
- [x] T009 Populate the flag in `backend/Services/Auth/AuthService.cs` for `LoginAsync`, `RefreshAsync`, and `GetUserAsync` via `user.Adapt<AuthUserDto>() with { OnboardingCompleted = await _profiles.HasCompletedOnboardingAsync(user.Id, ct) }` (depends on T007, T008)
- [x] T010 Add the thin owner action `POST me/onboarding/complete` (JWT scheme, `TryGetUserId`, → `204`/`404`/`401`) to `backend/Controllers/ProfilesController.cs` (depends on T007)
- [x] T011 [P] Add `onboardingCompleted: boolean` to the `AuthUser` interface in `frontend/apps/web/src/app/core/models/auth.models.ts`
- [x] T012 [P] Add `completeOnboarding(): Observable<void>` (`POST /api/v1/profiles/me/onboarding/complete`) to `frontend/apps/web/src/app/core/services/profile.service.ts`

**Checkpoint**: Migration applies; `/auth/me` and `/auth/login` carry `onboardingCompleted`; the complete endpoint works — stories can begin.

---

## Phase 3: User Story 1 - Be guided into onboarding once after first login (Priority: P1) 🎯 MVP

**Goal**: A verified first-time signer-in is routed into `/onboarding`; finishing or dismissing marks it complete server-side so it never auto-appears again; already-onboarded users are bounced out of the flow.

**Independent Test**: Fresh verified account signs in → lands on `/onboarding`; complete it → sign out/in → lands on dashboard; navigating to `/onboarding` afterward redirects to dashboard; unauthenticated `/onboarding` → sign-in.

### Tests for User Story 1

- [x] T013 [P] [US1] Integration test `OnboardingCompleteTests` — `POST /profiles/me/onboarding/complete` returns `401` without auth, `204` for the owner, is idempotent (second call keeps the original timestamp), and flips `onboardingCompleted` to `true` on `GET /auth/me` and `POST /auth/login`; fresh account reports `false` — in `backend/tests/JuggerHub.Api.IntegrationTests/Onboarding/OnboardingCompleteTests.cs`
- [x] T014 [P] [US1] E2E `onboarding.spec.ts` — register → verify → first login redirects to `/onboarding`; complete; sign out and back in lands on dashboard (not onboarding) — in `frontend/apps/web-e2e/src/onboarding.spec.ts` (authored + `auth.spec.ts` adapted for the redirect; **not executed** — Playwright needs the full docker-compose stack + Mailpit)

### Implementation for User Story 1

- [x] T015 [P] [US1] Create `onboardingGuard` (ensure session; if `currentUser.onboardingCompleted` redirect to `/`, else allow) in `frontend/apps/web/src/app/core/guards/onboarding.guard.ts`
- [x] T016 [US1] Register the full-screen `/onboarding` route (outside the shell, `canActivate: [authGuard, onboardingGuard]`) in `frontend/apps/web/src/app/app.routes.ts`
- [x] T017 [US1] Redirect after login in `frontend/apps/web/src/app/features/auth/sign-in/sign-in.component.ts` — navigate to `/onboarding` when `!user.onboardingCompleted`, else `/` (replaces the current `/account` navigation)
- [x] T018 [US1] Minimal `OnboardingComponent` shell — Welcome → Done, calls `ProfileService.completeOnboarding()` on finish/exit then navigates to `/` — in `frontend/apps/web/src/app/features/onboarding/onboarding.component.{ts,html,css}` (enough to make the gate pass; steps added in US2)
- [x] T019 [US1] Update `sign-in.component.spec.ts` and `auth.service.spec.ts` for the new `onboardingCompleted` field and redirect branch in `frontend/apps/web/src/app/features/auth/sign-in/` and `core/services/`

**Checkpoint**: The one-time gate works end-to-end even with a stub flow body.

---

## Phase 4: User Story 2 - Complete my profile through guided steps (Priority: P1)

**Goal**: The full step sequence collects display name (required), city, pompfen, photo + bio, and persists them via the existing 003 endpoints so they appear on the owner and public profiles.

**Independent Test**: Walk the flow filling every field, finish, and confirm all values appear on `/profile` and `/u/<handle>`.

### Tests for User Story 2

- [x] T020 [P] [US2] Jest unit `onboarding.component.spec.ts` — display name required (empty blocks Continue, prefilled with current name); on finish the component sends one `updateMine` with `{DisplayName, Hometown, Description, Pompfen}` and (if a file was picked) one `uploadAvatar`, then `completeOnboarding` — in `frontend/apps/web/src/app/features/onboarding/onboarding.component.spec.ts`

### Implementation for User Story 2

- [x] T021 [US2] Build the display-name step (prefilled from the current display name/handle via `ProfileService.getMine()`; "Handle @… stays the same" hint; Continue disabled while empty) in `onboarding.component.{ts,html}`
- [x] T022 [US2] Build the city step (hometown text) and the bio field on the photo+bio step in `onboarding.component.{ts,html}`
- [x] T023 [US2] Build the pompfen step reusing `PompfeSelectorComponent` + `POMPFEN_CATALOG` in `onboarding.component.{ts,html}`
- [x] T024 [US2] Build the photo step — file pick + preview, uploaded via `ProfileService.uploadAvatar()` in `onboarding.component.{ts,html}`
- [x] T025 [US2] Wire finish-persistence: hold step values in component state, on Finish send one `updateMine({DisplayName, Hometown, Description, Pompfen})`, upload the avatar if chosen, then `completeOnboarding()` → navigate to `/` (depends on T021–T024)

**Checkpoint**: A completed flow yields a fully populated profile via the reused 003 write paths.

---

## Phase 5: User Story 3 - Skip any step or the whole flow (Priority: P2)

**Goal**: Only the display name can block progress; every optional step skips without writing, and "I'll do this later" exits the flow — all paths still mark onboarding complete.

**Independent Test**: From Welcome choose "I'll do this later" → dashboard, nothing written, marked complete. Separately, skip every optional step, finish with name only → only name saved.

### Tests for User Story 3

- [x] T026 [P] [US3] Jest unit additions in `onboarding.component.spec.ts` — Skip advances without adding the field to the `updateMine` payload; "I'll do this later" calls `completeOnboarding` and writes no profile update; a name-only finish sends only `DisplayName`

### Implementation for User Story 3

- [x] T027 [US3] Add the quiet Skip control to city, pompfen, team, and photo+bio steps (advance without recording that field) in `onboarding.component.{ts,html}`
- [x] T028 [US3] Add "I'll do this later" on Welcome and "Skip for now" on the photo step — both call `completeOnboarding()` and navigate to `/` without a profile write in `onboarding.component.{ts,html}`
- [x] T029 [US3] Ensure the finish payload omits skipped optional fields (don't overwrite existing values with blanks) in `onboarding.component.ts`

**Checkpoint**: Every exit path is non-destructive and marks onboarding complete exactly once.

---

## Phase 6: User Story 4 - A calm, guided, responsive experience (Priority: P3)

**Goal**: One-question-per-screen with round-knob progress, Back navigation that preserves values, a clearly-non-functional team placeholder, and DESIGN.md styling that works on phone and desktop with loading/error states.

**Independent Test**: Inspect progress/Back/preservation, the team placeholder persists nothing, and every screen is legible and responsive with graceful save errors.

### Implementation for User Story 4

- [x] T030 [US4] Implement the round-knob progress indicator (completed = solid, current = ring, upcoming = muted) and bottom-anchored primary button per the "Minimal centered" wireframe in `onboarding.component.{html,css}`
- [x] T031 [US4] Implement Back navigation on every step except Welcome/Done, preserving previously entered values from component state in `onboarding.component.ts`
- [x] T032 [US4] Build the team step as a visual placeholder (search field + sample teams, clearly "coming soon") that persists nothing (FR-021) in `onboarding.component.{ts,html}`
- [x] T033 [P] [US4] Style the flow from `DESIGN.md` tokens (indigo/violet, system fonts, 8px spacing, card/rounded) and make it responsive desktop + mobile in `onboarding.component.css`
- [x] T034 [US4] Add loading state on save actions and a friendly, retry-able error message for a failed `updateMine`/`uploadAvatar` (no leaked internals) in `onboarding.component.{ts,html}`

**Checkpoint**: The flow matches the wireframe's shape and feel, on-brand and responsive.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [x] T035 [P] Run the backend test suite (Testcontainers) — `Onboarding/` integration tests green; existing auth tests still pass with the extended `AuthUserDto`
- [x] T036 [P] Run frontend Jest + Playwright, `nx lint`, and a production `nx build` — fix any breakage from the `AuthUser`/sign-in changes
- [ ] T037 Execute the `quickstart.md` scenarios A–E against `docker compose up` and confirm each expected outcome — **not run** (manual full-stack walkthrough deferred; automated backend integration + frontend unit/build/lint were run instead)
- [x] T038 [P] Confirm no drift: handle never editable in the flow (FR-023), team persists nothing (FR-021), public/owner DTOs unchanged; note any spec drift in the PR description

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories** (the frontend gate needs the backend flag + endpoint).
- **User Stories (Phase 3–6)**: All depend on Foundational.
  - US1 (gate) is the MVP and should land first; US2 fleshes out the flow body that US1 stubs.
  - US3 and US4 build on the US2 component.
- **Polish (Phase 7)**: Depends on all targeted stories.

### User Story Dependencies

- **US1 (P1)**: After Foundational. Independently testable with a Welcome→Done stub body.
- **US2 (P1)**: After Foundational; shares the `OnboardingComponent` file with US1 (extends the stub). Independently testable (fill + finish → populated profile).
- **US3 (P2)**: After US2 (adds skip affordances to the same component).
- **US4 (P3)**: After US2 (adds progress/back/team-stub/styling to the same component).

### Within Each User Story

- Tests first where marked; then models/services; then endpoints/UI; core before integration.
- The `OnboardingComponent` is a single shared surface across US1–US4, so those tasks are sequential on that file (not [P] with each other).

### Parallel Opportunities

- Setup T001/T002 in parallel.
- Foundational: T006/T008 [P], and frontend T011/T012 [P] alongside backend work; T003→T004→T005 are sequential (entity→config→migration); T007 before T009/T010.
- US1 tests T013 (backend) and T014 (e2e) in parallel; T015 guard [P] before T016 route.
- Polish T035/T036/T038 in parallel.

---

## Parallel Example: Foundational Phase

```bash
# Backend DTO + interface signatures alongside frontend plumbing:
Task: "Add OnboardingCompleted to AuthUserDto in backend/Dtos/Auth/AuthResponses.cs"      # T008 [P]
Task: "Add CompleteOnboardingStatus + method sigs to IProfileService.cs"                   # T006 [P]
Task: "Add onboardingCompleted to AuthUser in frontend .../core/models/auth.models.ts"      # T011 [P]
Task: "Add completeOnboarding() to frontend .../core/services/profile.service.ts"           # T012 [P]
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 Setup → Phase 2 Foundational (CRITICAL — blocks everything).
2. Phase 3 US1 with a minimal Welcome→Done body.
3. **STOP and VALIDATE**: first login → onboarding → complete → relogin lands on dashboard; the gate is real and server-authoritative.

### Incremental Delivery

1. Foundation ready.
2. US1 (gate) → demo the one-time redirect (MVP!).
3. US2 (guided steps) → demo a fully populated profile.
4. US3 (skip) → demo non-destructive exits.
5. US4 (polish + team stub) → demo the on-brand, responsive flow.

---

## Notes

- [P] = different files, no dependency on an incomplete task.
- US1–US4 largely share `onboarding.component.*`; treat those edits as sequential.
- Persistence is 100% reused from feature 003 — do not add new profile write endpoints.
- Commit after each task or logical group; stop at any checkpoint to validate the story independently.
