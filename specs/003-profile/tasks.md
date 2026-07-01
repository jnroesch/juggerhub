---
description: "Task list for Player Profile & Public Share Link"
---

# Tasks: Player Profile & Public Share Link

**Input**: Design documents from `/specs/003-profile/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/openapi.yaml, quickstart.md

**Tests**: INCLUDED — the plan's Testing section requires xUnit integration + Jest unit + Playwright e2e.

**Organization**: Tasks are grouped by user story (US1–US4) for independent implementation and testing. Backend namespace `JuggerHub`; frontend Nx app at `frontend/apps/web/src/app`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US4 (Setup/Foundational/Polish carry no story label)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Structure and shared primitives used across stories.

- [x] T001 [P] Create the `Pompfe` enum catalog (`Stab, Langpompfe, Schild, QTip, Kette, DoppelKurz, Laeufer`) in `backend/Entities/Pompfe.cs`
- [x] T002 [P] Add `ProfileOptions` (MaxAvatarBytes ~2 MB, handle min/max length) in `backend/Common/ProfileOptions.cs`; bind it in `backend/Program.cs` with safe defaults
- [x] T003 [P] Add shared `PaginationRequest` and `PagedResult<T>` in `backend/Common/` (if not already present) per the constitution's pagination contract
- [x] T004 [P] Create the canonical pompfen catalog (order + DE/EN labels) in `frontend/apps/web/src/app/shared/pompfen.catalog.ts`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Schema, DTOs, and service seams every story depends on.

**⚠️ CRITICAL**: No user-story work begins until this phase is complete.

- [x] T005 [P] Create `PlayerProfile : BaseEntity` (UserId, Handle immutable, DisplayName, Hometown, Description + navs) in `backend/Entities/PlayerProfile.cs`
- [x] T006 [P] Create `ProfilePompfe : BaseEntity` (ProfileId, Pompfe) in `backend/Entities/ProfilePompfe.cs`
- [x] T007 [P] Create `ProfileAvatar : BaseEntity` (ProfileId, ContentType, Bytes) in `backend/Entities/ProfileAvatar.cs`
- [x] T008 [P] Create `Event : BaseEntity` (Name, Date, Location) in `backend/Entities/Event.cs`
- [x] T009 [P] Create `EventParticipation : BaseEntity` (ProfileId, EventId, TeamLabel) in `backend/Entities/EventParticipation.cs`
- [x] T010 Add 1:1 `PlayerProfile` navigation to `backend/Entities/User.cs`
- [x] T011 Configure DbSets + model config in `backend/Data/AppDbContext.cs`: unique `Handle`, unique `UserId`, unique (`ProfileId`,`Pompfe`), unique `ProfileAvatar.ProfileId`, unique (`ProfileId`,`EventId`), FKs with cascade, string lengths (depends on T005–T010)
- [x] T012 Generate EF migration `AddProfilesAndEvents` in `backend/Data/Migrations/` (depends on T011)
- [x] T013 [P] Implement `HandlePolicy` (normalize + format regex + reserved-word set + length) in `backend/Services/Profile/HandlePolicy.cs`
- [x] T014 [P] Create profile DTOs (`UpdateProfileRequest`, `OwnerProfileDto`, `PublicProfileDto`, `ActivityItemDto`, `HandleAvailabilityDto`) in `backend/Dtos/Profile/ProfileDtos.cs`
- [x] T015 [P] Add `ProfileMapping` Mapster config (entity → `OwnerProfileDto`/`PublicProfileDto`, sensitive fields excluded) in `backend/Services/Profile/ProfileMapping.cs`
- [x] T016 Define `IProfileService` (create-at-registration, get owner, update, avatar set/get, get public) + `ProfileService` skeleton in `backend/Services/Profile/`; register in `backend/Program.cs` DI (depends on T005–T015)
- [x] T017 [P] Add frontend profile models (`OwnerProfile`, `PublicProfile`, `UpdateProfileRequest`, `ActivityItem`, `HandleAvailability`, `Pompfe`) in `frontend/apps/web/src/app/core/models/profile.models.ts`
- [x] T018 [P] Add `ProfileService` skeleton (owner get/update, avatar upload, public get, activity — signals) in `frontend/apps/web/src/app/core/services/profile.service.ts`

**Checkpoint**: Schema migrates, service seams exist — stories can begin.

---

## Phase 3: User Story 1 - Claim an immutable handle at registration (Priority: P1) 🎯 MVP

**Goal**: A user chooses a unique, immutable, URL-safe handle at signup; a profile is created atomically with the account.

**Independent Test**: Register with a valid/unused handle → account+profile created; duplicate/reserved/malformed handle → rejected; no path changes the handle afterward.

### Tests for User Story 1 ⚠️

- [x] T019 [P] [US1] Integration tests (register with valid handle creates profile; duplicate → 409; malformed/reserved → 400; email neutrality preserved) in `backend/tests/JuggerHub.Api.IntegrationTests/Profile/RegisterHandleTests.cs`
- [x] T020 [P] [US1] Integration test asserting handle immutability (no endpoint/verb changes it) in `backend/tests/JuggerHub.Api.IntegrationTests/Profile/HandleImmutabilityTests.cs`
- [x] T021 [P] [US1] Integration test for `GET /auth/handle-available` (available/taken/reserved/malformed) in `backend/tests/JuggerHub.Api.IntegrationTests/Profile/HandleAvailabilityTests.cs`

### Implementation for User Story 1

- [x] T022 [US1] Extend `RegisterRequest` with required `Handle` (format annotation) in `backend/Dtos/Auth/AuthRequests.cs`
- [x] T023 [US1] Extend `AuthService.RegisterAsync` to validate handle (`HandlePolicy`) and create `User` + `PlayerProfile` atomically (transaction; DisplayName defaults to handle) in `backend/Services/Auth/AuthService.cs` (depends on T016, T022)
- [x] T024 [US1] Map handle-taken result to `409`/clear message; keep email enumeration-neutral in `backend/Controllers/AuthController.cs` Register (depends on T023)
- [x] T025 [US1] Add `GET /api/v1/auth/handle-available` (`[AllowAnonymous]`) returning `HandleAvailabilityDto` in `backend/Controllers/AuthController.cs` (depends on T013, T016)
- [x] T026 [US1] Add `handle` to frontend `RegisterRequest` in `frontend/apps/web/src/app/core/models/auth.models.ts`
- [x] T027 [US1] Add handle field + live availability (debounced call to handle-available) + format hint to `frontend/apps/web/src/app/features/auth/register/register.component.{ts,html,css}` (depends on T025, T026)
- [x] T028 [P] [US1] Unit test for register handle validation/availability in `frontend/apps/web/src/app/features/auth/register/register.component.spec.ts`

**Checkpoint**: Registration issues an immutable handle + profile; MVP deployable.

---

## Phase 4: User Story 2 - View and share a public profile (Priority: P1)

**Goal**: Anyone can view `/u/<handle>` without auth; only non-sensitive fields; easy copy-link.

**Independent Test**: Signed-out `/u/<handle>` renders public fields, response has no email/account data, unknown handle → friendly not-found, copy-link yields the short URL.

### Tests for User Story 2 ⚠️

- [x] T029 [P] [US2] Integration test: `GET /profiles/{handle}` returns `PublicProfileDto` with **no email/account fields** and only selected pompfen (SC-002) in `backend/tests/JuggerHub.Api.IntegrationTests/Profile/PublicProfileTests.cs`
- [x] T030 [P] [US2] Integration test: unknown handle → 404 generic ProblemDetails; `GET /profiles/{handle}/avatar` (present/absent) in `backend/tests/JuggerHub.Api.IntegrationTests/Profile/PublicProfileNotFoundAndAvatarTests.cs`

### Implementation for User Story 2

- [x] T031 [US2] Implement `GetPublicProfileAsync(handle)` in `backend/Services/Profile/ProfileService.cs` — projected query (`.Select`/`ProjectToType`, `AsNoTracking`) building `PublicProfileDto` without sensitive columns (depends on T016)
- [x] T032 [US2] Create anonymous `ProfilesController` with `GET /api/v1/profiles/{handle}` (public DTO) and `GET /api/v1/profiles/{handle}/avatar` (serve bytes / 404) in `backend/Controllers/ProfilesController.cs` (depends on T031)
- [x] T033 [US2] Implement `profile.service.ts` public-profile + avatar-URL methods in `frontend/apps/web/src/app/core/services/profile.service.ts` (depends on T018)
- [x] T034 [US2] Create `profile-public` component (full-screen) with identity, selected pompfen, avatar w/ placeholder, copy-link, not-found + empty states in `frontend/apps/web/src/app/features/profile/profile-public/profile-public.component.{ts,html,css}` (depends on T033)
- [x] T035 [US2] Add anonymous route `/u/:handle` (outside shell) in `frontend/apps/web/src/app/app.routes.ts` (depends on T034)

**Checkpoint**: Public share page works signed-out with no sensitive data on the wire.

---

## Phase 5: User Story 3 - Edit my own profile (Priority: P2)

**Goal**: Owner edits display name, picture, description, hometown, and pompfen selection; changes reflect on the public page.

**Independent Test**: Owner updates each field + pompfen and avatar, save; owner + public views reflect it; invalid image rejected; non-owner/unauthed edits refused.

### Tests for User Story 3 ⚠️

- [x] T036 [P] [US3] Integration test: `GET/PUT /profiles/me` require auth; update persists; PUT ignores/rejects any handle field (immutable) in `backend/tests/JuggerHub.Api.IntegrationTests/Profile/OwnerProfileEditTests.cs`
- [x] T037 [P] [US3] Integration test: avatar upload validation (content-type magic-byte sniff + size cap; invalid leaves existing avatar unchanged) in `backend/tests/JuggerHub.Api.IntegrationTests/Profile/AvatarUploadTests.cs`

### Implementation for User Story 3

- [x] T038 [US3] Implement `GetOwnerProfileAsync` + `UpdateProfileAsync` (replace pompfen set; length guards) in `backend/Services/Profile/ProfileService.cs` (depends on T016)
- [x] T039 [US3] Implement `SetAvatarAsync`/`GetAvatarAsync` (validate sniffed content-type + size vs `ProfileOptions`, upsert `ProfileAvatar`) in `backend/Services/Profile/ProfileService.cs` (depends on T016)
- [x] T040 [US3] Add owner endpoints to `ProfilesController` — `GET /profiles/me`, `PUT /profiles/me`, `PUT /profiles/me/avatar` (JWT bearer; act only on authenticated subject) in `backend/Controllers/ProfilesController.cs` (depends on T038, T039)
- [x] T041 [P] [US3] Create `pompfe-selector` component (full set: selected vs available, multi-toggle incl. Läufer) in `frontend/apps/web/src/app/features/profile/components/pompfe-selector/pompfe-selector.component.{ts,html,css}`
- [x] T042 [US3] Create `profile-owner` view/edit component (fields, avatar upload, pompfe-selector, save) in `frontend/apps/web/src/app/features/profile/profile-owner/profile-owner.component.{ts,html,css}` (depends on T041, T033)
- [x] T043 [US3] Add guarded route `/profile` (in shell, `authGuard`) in `frontend/apps/web/src/app/app.routes.ts` (depends on T042)
- [x] T044 [P] [US3] Unit test for `ProfileService` (frontend) update/avatar + pompfe-selector in `frontend/apps/web/src/app/core/services/profile.service.spec.ts`

**Checkpoint**: Owner editing works end-to-end and reflects on the public page.

---

## Phase 6: User Story 4 - See genuine recent activity (Priority: P3)

**Goal**: Real recent activity (events + team) from the minimal events model, newest-first, capped/paginated.

**Independent Test**: With seeded participations, activity shows correct events/team newest-first, capped; no participation → empty state; endpoint is bounded.

### Tests for User Story 4 ⚠️

- [x] T045 [P] [US4] Integration test: activity ordering (Event.Date desc), pagination + hard cap, team label, empty state in `backend/tests/JuggerHub.Api.IntegrationTests/Profile/ActivityTests.cs`

### Implementation for User Story 4

- [x] T046 [P] [US4] Implement `IEventActivityService`/`EventActivityService.GetRecentAsync(profileId, pagination)` — join participations→events, `Event.Date` desc, projected to `ActivityItemDto`, `PagedResult<T>` in `backend/Services/Events/` (depends on T012); register in `backend/Program.cs`
- [x] T047 [US4] Embed recent-activity (capped few) into `OwnerProfileDto`/`PublicProfileDto` builds and add `GET /api/v1/profiles/{handle}/activity` (paginated, anonymous) in `backend/Controllers/ProfilesController.cs` + `ProfileService` (depends on T046, T031, T038)
- [x] T048 [P] [US4] Add a dev-only seed for sample events + participations (per quickstart Scenario D) in `backend/Data/` (guarded to non-production)
- [x] T049 [US4] Render recent-activity list (date/month, name, location, "with <Team>") + empty state in `profile-public` and `profile-owner` components; add `activity` fetch to `profile.service.ts` (depends on T034, T042)

**Checkpoint**: Activity is real, ordered, bounded, with team attribution.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [x] T050 [P] Add Teams/Badges **stub** sections (empty-state UI only, imply no data) to `profile-owner` and `profile-public` components
- [x] T051 [P] DESIGN.md conformance + responsiveness pass (phone ~375px, desktop ~1280px; empty/loading/error states) across profile components
- [x] T052 Playwright e2e `profile.spec.ts` — register-with-handle → edit → signed-out `/u/<handle>` (assert no email on the wire), desktop + mobile in `frontend/apps/web-e2e/src/profile.spec.ts`
- [x] T053 [P] Reconcile `.env.sample` / `appsettings*.json` / `docker-compose.yml` for any `Profile:*` config added
- [x] T054 Update `frontend/apps/web/src/app/core/models/auth.models.ts` + any auth spec touched by the handle field; ensure `speckit-analyze` cross-artifact consistency
- [ ] T055 Run `quickstart.md` scenarios A–E end-to-end (Docker) and record results

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: depends on Setup; **blocks all stories**. T011→T012 sequential; entities T005–T009 parallel.
- **US1 (Phase 3)**: depends on Foundational. **MVP.**
- **US2 (Phase 4)**: depends on Foundational; profiles exist once US1 lands (needed to have data to view, but code is independent).
- **US3 (Phase 5)**: depends on Foundational; reuses US2's `ProfilesController`/frontend service.
- **US4 (Phase 6)**: depends on Foundational; enriches US2/US3 views but is independently testable via the activity endpoint.
- **Polish (Phase 7)**: depends on the stories being delivered.

### Story Independence

- US1 is self-contained (registration + handle). US2 reads profiles US1 creates. US3 edits them. US4 adds activity. Each story is testable on its own once Foundational is done; later stories integrate without breaking earlier ones.

### Within Each Story

- Tests written first and expected to FAIL before implementation.
- Models → services → endpoints → frontend.

### Parallel Opportunities

- Setup: T001–T004 all [P].
- Foundational: entities T005–T009 [P]; then T010→T011→T012; T013–T015, T017–T018 [P].
- Each story's `[P]` tests run together; frontend/backend `[P]` tasks in a story can proceed in parallel.

---

## Parallel Example: Foundational entities

```bash
Task: "Create PlayerProfile entity in backend/Entities/PlayerProfile.cs"
Task: "Create ProfilePompfe entity in backend/Entities/ProfilePompfe.cs"
Task: "Create ProfileAvatar entity in backend/Entities/ProfileAvatar.cs"
Task: "Create Event entity in backend/Entities/Event.cs"
Task: "Create EventParticipation entity in backend/Entities/EventParticipation.cs"
```

---

## Implementation Strategy

### MVP First (US1)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 → **STOP & VALIDATE** (register issues an immutable handle + profile) → demo.

### Incremental Delivery

Foundation → US1 (handle) → US2 (public share page, the headline value) → US3 (editing) → US4 (activity) → Polish. Each story adds value without breaking the previous.

---

## Notes

- [P] = different files, no incomplete-task dependency. [Story] labels map to spec.md US1–US4.
- **Security invariants to keep green** (from contracts/README): public responses carry no email/account data (SC-002); edits act only on the authenticated subject (SC-005); handle immutable (SC-006); activity bounded (SC-007).
- **Spec drift**: US1 changes the 002 registration contract (adds handle) — call it out at PR/review time.
- Commit after each task or logical group; stop at any checkpoint to validate independently.
