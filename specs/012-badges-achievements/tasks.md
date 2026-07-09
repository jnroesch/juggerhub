---
description: "Task list for Badges & Achievements"
---

# Tasks: Badges & Achievements

**Input**: Design documents from `/specs/012-badges-achievements/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/openapi.yaml](./contracts/openapi.yaml), [quickstart.md](./quickstart.md)

**Tests**: INCLUDED. Admin authorization is security-critical (constitution Principle I, NON-NEGOTIABLE), so backend integration tests — especially the non-admin/anonymous refusal tests — are first-class deliverables.

**Organization**: Grouped by user story. Both US1 and US2 are P1 (the MVP needs both — grant *and* display). US3 (automatic awarding) is **deferred / out of scope** and has no implementation tasks.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: US1 / US2 for story-phase tasks; Setup/Foundational/Polish carry no story label
- All paths are repo-relative. Backend namespace `JuggerHub` at `backend/`; frontend Nx app `web` at `frontend/apps/web/src/app/`; backend tests at `backend/tests/JuggerHub.Api.IntegrationTests/`; e2e at `frontend/apps/web-e2e/`.

> **Two parallel families**: badges and achievements are separate systems (spec FR-003). Many tasks touch both — where they are separate files they are `[P]`; where they share a file (e.g., `AppDbContext`, `Program.cs`, `MappingConfig`, `app.routes.ts`) they are sequential.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Config scaffolding and folders the rest builds on.

- [X] T001 [P] Create `backend/Common/AdminOptions.cs` — `SectionName = "Admin"`, `IReadOnlyList<string> Emails` (bound from config), with case-insensitive membership intent documented (temporary gate per FR-013).
- [X] T002 [P] Wire the admin allowlist config: add `Admin__Emails=admin@test.de` to `.env.sample` (documented as the local platform-admin allowlist; comma-separated for multiple), pass `Admin__Emails` through to the `backend` service `environment` in `docker-compose.yml`, and add an empty `Admin:Emails` default to `backend/appsettings.json`.
- [X] T003 Create feature folders: `backend/Dtos/Badges/`, `backend/Dtos/Achievements/`, `backend/Services/Badges/`, `backend/Services/Achievements/`, `backend/Security/PlatformAdmin/`, `backend/Controllers/Admin/`, and `frontend/apps/web/src/app/features/admin/`.

**Checkpoint**: Config keys resolve; folders exist.

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ CRITICAL**: No user story work begins until this phase is complete. This builds the data layer, the migration, the shared icon-upload validation, and the `PlatformAdmin` authorization policy every admin route depends on.

- [X] T004 [P] Create `backend/Entities/RecognitionEnums.cs` — `AwardSource { Manual, Automatic }`, `AwardStatus { Active, Revoked }`, `SubjectType { Player, Team }` (per [data-model.md](./data-model.md)).
- [X] T005 [P] Create badge entities in `backend/Entities/`: `BadgeDefinition.cs`, `BadgeIcon.cs`, `BadgeAward.cs` (all `: BaseEntity`; polymorphic `PlayerProfileId?`/`TeamId?`; award lifecycle fields per data-model).
- [X] T006 [P] Create achievement entities in `backend/Entities/`: `AchievementDefinition.cs`, `AchievementIcon.cs`, `AchievementAward.cs` (same shape as badges **plus** `ContextYear int?` and `ContextLabel string?` on the award).
- [X] T007 Extend `backend/Data/AppDbContext.cs`: add the 6 `DbSet`s and `OnModelCreating` config per family — max lengths, the `CHECK ((PlayerProfileId IS NOT NULL) <> (TeamId IS NOT NULL))`, filtered-unique indexes on `(DefinitionId, PlayerProfileId)` and `(DefinitionId, TeamId)` `WHERE Status = Active`, subject indexes, icon unique index, and delete behaviors (icon Cascade; award→definition Restrict; award→subject Cascade; award→granting User Restrict). Depends on T004–T006.
- [X] T008 Generate EF Core migration `AddBadgesAndAchievements` into `backend/Data/Migrations/` (auto-applied on startup). Verify the CHECK + filtered indexes are emitted. Depends on T007.
- [X] T009 [P] Add shared image-upload validation for icons in `backend/Common/` (magic-byte content-type sniff png/jpeg/webp + size cap), reusing/extracting the profile-avatar validation approach; used by both badge and achievement icon uploads.
- [X] T010 [P] Create the `PlatformAdmin` policy in `backend/Security/PlatformAdmin/`: `PlatformAdminRequirement.cs` + `PlatformAdminHandler.cs` — resolve the caller's user id from the JWT `sub`/`NameIdentifier` claim, load the user via `UserManager<User>`, and authorize iff the normalized email is in `AdminOptions.Emails` (case-insensitive). Fails closed.
- [X] T011 Extend `backend/Program.cs`: `Configure<AdminOptions>(GetSection("Admin"))`, register the authorization handler, and add an authorization policy named `PlatformAdmin` using the requirement. Depends on T001, T010.

**Checkpoint**: Solution builds, migration applies, `[Authorize(Policy="PlatformAdmin")]` resolves and denies by default. Stories can begin.

---

## Phase 3: User Story 1 — Admin curates & manually grants (Priority: P1) 🎯 MVP

**Goal**: A platform admin can define badges/achievements (with icons), grant them to a player or team, and revoke them — all gated server-side.

**Independent Test**: Using the `admin@test.de` account, create a badge, upload an icon, grant to a player and a team, prevent a duplicate (409) and a subject mismatch (400), then revoke; confirm every admin route refuses non-admins (403) and anonymous callers (401) when called directly.

### Tests for User Story 1 (write first, expect FAIL) ⚠️

- [X] T012 [P] [US1] Badge admin integration tests in `backend/tests/JuggerHub.Api.IntegrationTests/Badges/BadgeAdminTests.cs`: create/edit/retire definition; icon upload (valid + rejected type); grant to player and team; duplicate active grant → 409; subject-type mismatch → 400; retire preserves existing awards; revoke sets Revoked + allows re-grant.
- [X] T013 [P] [US1] Achievement admin integration tests in `.../Achievements/AchievementAdminTests.cs`: same coverage as badges plus grant with `contextYear`/`contextLabel` persisted.
- [X] T014 [P] [US1] Authorization tests in `.../Recognition/AdminAuthorizationTests.cs`: for **every** `/admin/badges*` and `/admin/achievements*` route, a non-admin authenticated caller → 403 and an anonymous caller → 401; an allowlisted admin → allowed. This is the security-critical suite (SC-002, SC-006).

### Implementation for User Story 1

- [X] T015 [P] [US1] Badge admin DTOs in `backend/Dtos/Badges/` (`DefinitionUpsertRequest`, `BadgeDefinitionDto`, `GrantRequest`, `RevokeRequest`, `AwardDto`) with data-annotation validation per [contracts/openapi.yaml](./contracts/openapi.yaml) (name ≤60, description ≤280, at-least-one applicability).
- [X] T016 [P] [US1] Achievement admin DTOs in `backend/Dtos/Achievements/` (same set + `contextYear`/`contextLabel` on grant/award DTOs).
- [X] T017 [US1] Add Mapster maps (definitions + awards → DTOs) in `backend/Common/MappingConfig.cs`.
- [X] T018 [US1] Create `IBadgeService` + `BadgeService` in `backend/Services/Badges/`: paginated definition list, create/edit/retire, icon upload/get, grant (validates not-retired, subject exists, applicability, duplicate-active), revoke (targeted update setting `ModifiedDate`). Depends on T005, T015.
- [X] T019 [US1] Create `IAchievementService` + `AchievementService` in `backend/Services/Achievements/` (same, incl. context). Depends on T006, T016.
- [X] T020 [US1] Create `backend/Controllers/Admin/BadgesAdminController.cs` — `[Authorize(Policy="PlatformAdmin")]`, `[ApiVersion("1.0")]`, thin actions per contract (definitions CRUD, icon PUT, grant, revoke). Depends on T018.
- [X] T021 [US1] Create `backend/Controllers/Admin/AchievementsAdminController.cs` (parallel to badges). Depends on T019.
- [X] T022 [US1] Register `IBadgeService`/`IAchievementService` (and any icon service) in `backend/Program.cs`.

### Frontend (admin surface) for User Story 1

- [X] T023 [P] [US1] Admin recognition models in `frontend/apps/web/src/app/core/models/recognition-admin.models.ts` (definition, award, grant/revoke request interfaces).
- [X] T024 [P] [US1] Admin recognition service in `frontend/apps/web/src/app/core/services/recognition-admin.service.ts` (define/list/edit/retire, icon upload, grant, revoke).
- [X] T025 [US1] Badge admin screens in `frontend/apps/web/src/app/features/admin/badges/` (catalog list, create/edit form with icon upload + applicability, grant-by-handle/slug, revoke) — separate `.html`/`.css`/`.ts`, styled per DESIGN.md.
- [X] T026 [US1] Achievement admin screens in `frontend/apps/web/src/app/features/admin/achievements/` (same + context fields on grant).
- [X] T027 [US1] Add guarded admin routes in `frontend/apps/web/src/app/app.routes.ts` with a client-side admin guard (UX-only; the server policy is the boundary) in `frontend/apps/web/src/app/core/guards/`.
- [X] T028 [P] [US1] Jest tests for the admin forms (applicability requires ≥1 subject type; grant form validates handle/slug) under `features/admin/`.

**Checkpoint**: US1 fully functional — an admin can define, grant, and revoke via API and UI; non-admins are refused server-side. MVP-demoable together with US2.

---

## Phase 4: User Story 2 — Players & teams display recognitions (Priority: P1)

**Goal**: Earned badges and achievements render (active-only, grouped, with empty states) on the player profile (replacing the stub) and the team page.

**Independent Test**: With seeded awards, load a profile and a team page → badges and achievements show grouped with icon/name/description/earned-date (+ context for achievements); a subject with none shows the empty state; a revoked award is absent.

### Tests for User Story 2 (write first, expect FAIL) ⚠️

- [X] T029 [P] [US2] Integration test in `backend/tests/JuggerHub.Api.IntegrationTests/Recognition/ProfileRecognitionTests.cs`: public and owner profile payloads include active badges/achievements and exclude revoked ones.
- [X] T030 [P] [US2] Integration test in `.../Recognition/TeamRecognitionTests.cs`: the team page payload includes the team's active awards (with achievement context) and excludes revoked.

### Implementation for User Story 2

- [X] T031 [P] [US2] Read DTO `EarnedRecognitionDto` in `backend/Dtos/` (definitionId, name, description, hasIcon, earnedAt, optional contextYear/contextLabel) and extend the profile DTOs (`PublicProfileDto`, `OwnerProfileDto`) and the team page DTO with `Badges`/`Achievements` arrays.
- [X] T032 [US2] Extend `backend/Services/Profile/ProfileService.cs` to project a bounded set of the subject's **active** badges/achievements into the owner + public profile responses (`AsNoTracking` + `Select`; revoked excluded). Depends on T031.
- [X] T033 [US2] Extend the team page service/DTO assembly (team-detail read path) to include the team's active awards. Depends on T031.
- [X] T034 [P] [US2] Public read models + service in `frontend/apps/web/src/app/core/models/recognition.models.ts` and `core/services/recognition.service.ts` (icon URL builder for `/api/v1/{badges|achievements}/{id}/icon`).
- [X] T035 [US2] Shared display component(s) in `frontend/apps/web/src/app/features/profile/components/` rendering the two groups with icon/name/description/earned-date (+ context) and an empty state, per DESIGN.md and the existing badge-slot visual.
- [X] T036 [US2] Replace the "Badges (stub)" sections in `features/profile/profile-public/profile-public.component.html` and `features/profile/profile-owner/profile-owner.component.html` (and their `.ts`) with the real display.
- [X] T037 [US2] Add the display to `features/teams/team-detail/` (team page).
- [X] T038 [P] [US2] Jest tests for the display component (renders list; renders empty state) under `features/profile/components/`.
- [X] T039 [US2] Playwright e2e in `frontend/apps/web-e2e/` (desktop + mobile): admin grants a badge → it appears on the player profile and team page; after revoke it no longer shows. *(End-to-end — exercises US1 admin API + US2 display.)*

**Checkpoint**: US1 + US2 together deliver the full manual-award MVP.

---

## Phase 5: User Story 3 — Automatic awarding (Priority: Deferred)

**Out of scope for v1** (spec "Out of Scope"). No tasks. The v1 model already reserves `AwardSource.Automatic` and keeps earned awards durable, so this can be added later without reworking history. Tracked conceptually in the spec; a future feature/spec will cover it.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T040 [P] Seed a couple of sample badges/achievements (and a demo award) in `backend/Data/DevDataSeeder.cs` for local/dev display, guarded to non-production.
- [X] T041 [P] Add a "Badges & Achievements" row to the Feature overview table in `README.md`.
- [X] T042 Verify DESIGN.md alignment for the display and admin UI: empty/loading/error states, responsive (desktop + mobile), and basic accessibility (alt text on icons, focus order).
- [X] T043 Run `/security-review` on the branch diff, focused on the admin authorization boundary and the temporary config gate; resolve findings.
- [X] T044 Run the full [quickstart.md](./quickstart.md) validation (all 7 scenarios) plus the three test suites (backend integration, Jest, Playwright desktop+mobile) and confirm green.

---

## Dependencies & Execution Order

- **Setup (Phase 1)** → **Foundational (Phase 2)** blocks everything.
- **US1 (Phase 3)** and **US2 (Phase 4)** both depend only on Foundational and are each independently testable (US2 backend/Jest against seeded data). The **US2 Playwright e2e (T039)** additionally needs US1's admin API to grant through the UI.
- **Polish (Phase 6)** after the desired stories are complete.

### Within a story
- Tests (T012–T014, T029–T030) written first and failing before implementation.
- Entities → DbContext → migration; DTOs → services → controllers; models/services → components.

### Parallel opportunities
- Setup: T001, T002 in parallel.
- Foundational: T004, T005, T006 in parallel; then T007→T008; T009, T010 in parallel; then T011.
- US1 tests T012/T013/T014 in parallel; DTOs T015/T016 in parallel; frontend models/service T023/T024 in parallel.
- US2 tests T029/T030 in parallel; T031 then T032/T033; T034 and T038 parallelizable.

---

## Parallel Example: User Story 1 tests

```text
# Launch the US1 test suites together (they are separate files):
Task: "Badge admin integration tests in .../Badges/BadgeAdminTests.cs"        (T012)
Task: "Achievement admin integration tests in .../Achievements/AchievementAdminTests.cs" (T013)
Task: "Admin authorization tests in .../Recognition/AdminAuthorizationTests.cs" (T014)
```

---

## Implementation Strategy

### MVP (both P1 stories)

1. Phase 1 Setup → Phase 2 Foundational (CRITICAL).
2. Phase 3 US1 (admin define/grant/revoke + admin UI).
3. Phase 4 US2 (display on profile + team).
4. **STOP and VALIDATE**: run quickstart scenarios 1–7. This is the shippable manual-award MVP.

### Notes
- Commit after each task or logical group; keep the temporary admin gate isolated behind the `PlatformAdmin` policy so GitHub #21 can later swap only T010/T011.
- Do not implement any automatic-awarding logic (US3) — it is deferred by decision.
- After merge, revisit spec 012 to swap the config gate for the real admin role when #21 lands.
