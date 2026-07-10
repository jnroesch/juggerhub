# Tasks: Platform Admin Area

**Input**: Design documents from `/specs/013-admin-area/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/admin-api.md, quickstart.md

**Tests**: Included — the feature is a privilege boundary; spec SC-001/SC-005/SC-008 and issue #21 explicitly demand server-side authorization tests. Integration tests follow the existing `JuggerHubApiFactory` pattern.

**Organization**: Grouped by user story (US1–US5 from spec.md). Foundational schema work (account state, action log, query filter) serves US2–US4 and sits in Phase 2.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

- [X] T001 Create `backend/Entities/AccountEnums.cs` with `AccountStatus` (Active=0, Suspended=1, Banned=2) and `AdminAccountAction` (Suspend, Reinstate, Ban, Unban, PasswordResetSent) per data-model.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Account-state schema + ban-invisibility filter that US2–US4 all read. **No user story work until this phase is green.**

- [X] T002 Add `Status` (`AccountStatus`, default Active) and `StatusChangedAt` (`DateTime?`) to `backend/Entities/User.cs`
- [X] T003 [P] Create `backend/Entities/AdminActionRecord.cs` (`BaseEntity`; `ActorUserId`, `TargetUserId`, `Action`, `Note?`; navs to `User`) per data-model.md
- [X] T004 Register in `backend/Data/AppDbContext.cs`: `DbSet<AdminActionRecord>`, FK config (`DeleteBehavior.Restrict` both FKs), index `(TargetUserId, CreatedDate)`, and the global query filter `PlayerProfile → p.User.Status != AccountStatus.Banned`
- [X] T005 Add EF migration `AddAccountStateAndAdminActions` (backend project conventions; verify snapshot includes filter-neutral schema + new columns backfill Active)
- [X] T006 Run the full existing backend test suite (`dotnet test backend/tests/JuggerHub.Api.IntegrationTests`) and fix any fallout from the new global query filter (required-navigation warnings, dropped joins) — all pre-013 suites must stay green before stories start

**Checkpoint**: schema + filter in place, existing behavior unchanged (all accounts Active).

---

## Phase 3: User Story 1 — A real platform-administrator role (P1) 🎯 MVP

**Goal**: `PlatformAdmin` Identity role mirrored from `AdminOptions` config at startup; policy handler checks role membership per request; interim allowlist retired as an authorization path; 012 behavior unchanged.

**Independent Test**: quickstart Scenario 1 — API-only: non-admin 403, synced admin 200, config removal revokes at restart, empty config fails closed with logged warning.

- [X] T007 [US1] Create `backend/Security/PlatformAdmin/PlatformAdminRoleSync.cs`: idempotent startup sync per research §1 (ensure role, grant configured+existing, revoke unconfigured members, skip+log missing accounts, warn loudly on zero admins, never throw — log error and continue)
- [X] T008 [US1] Wire the sync into `backend/Program.cs` immediately after `ApplyMigrationsAsync` (scoped block, before dev seeding); register any needed DI
- [X] T009 [US1] Rewrite `backend/Security/PlatformAdmin/PlatformAdminHandler.cs` to succeed iff the JWT `sub` user is currently in the `PlatformAdmin` role (per-request store check via `UserManager`/role query; fail closed on missing user/role) — controllers and `PlatformAdminRequirement` untouched
- [X] T010 [P] [US1] Update the now-stale "TEMPORARY / interim" documentation comments in `backend/Common/AdminOptions.cs` (now: sync source, mirror semantics, restart-to-apply) and `backend/Security/PlatformAdmin/*` to match research §1–§2
- [X] T011 [P] [US1] New integration tests `backend/tests/JuggerHub.Api.IntegrationTests/Admin/PlatformAdminRoleSyncTests.cs`: grant on sync, revoke on config removal, skip+later-pickup for unregistered email, empty-config warning + fail closed, idempotent re-run
- [X] T012 [US1] Update `backend/tests/JuggerHub.Api.IntegrationTests/Recognition/AdminAuthorizationTests.cs` (+ `JuggerHubApiFactory`/`RecognitionTestSupport` helpers as needed) for the role-based gate; add the SC-001 case "email present in config but sync not run for that account → refused"; run all `Recognition/*`, `Badges/*`, `Achievements/*` suites unchanged-green (SC-008)

**Checkpoint**: privilege boundary replaced; GH issue #21's core is done and independently shippable.

---

## Phase 4: User Story 2 — The gated door and the admin landing (P2)

**Goal**: Lock-marked Admin entry (desktop nav + mobile account-menu row) rendered only for admins; `/admin` becomes a shell (shield header, Overview/Users/Catalogue nav, back-to-app) landing on the overview (4 stats, new players, recent grants, search hand-off).

**Independent Test**: quickstart Scenario 2.

- [ ] T013 [US2] Create `backend/Services/Admin/IAdminOverviewService.cs` + `AdminOverviewService.cs`: one aggregate read (players count via filtered `PlayerProfiles`, teams count, events last 30 days, suspended count, ≤5 new players this week, ≤5 newest grants across `BadgeAward`+`AchievementAward` with subject/grantor/date) — `AsNoTracking` + projections; register in `Program.cs`
- [ ] T014 [US2] Create `backend/Dtos/Admin/AdminOverviewDtos.cs` and thin `backend/Controllers/Admin/AdminOverviewController.cs` (`GET api/v1/admin/overview`, `[Authorize(Policy = PlatformAdminPolicy.Name)]`) per contracts/admin-api.md
- [ ] T015 [P] [US2] Integration tests `backend/tests/JuggerHub.Api.IntegrationTests/Admin/AdminOverviewTests.cs`: authz (401/403/200), counts correct against seeded data (suspended count included; banned excluded from players), list shapes + caps
- [ ] T016 [US2] Frontend: create `frontend/apps/web/src/app/core/models/admin.models.ts` + `frontend/apps/web/src/app/core/services/admin.service.ts` (cached `checkAccess` probe signal reused by guard+nav, `getOverview()`); refactor `admin.guard.ts` and any probe use in `recognition-admin.service.ts` onto it
- [ ] T017 [US2] Frontend nav gating per wireframe 1a: lock-marked "Admin" item after normal links in `frontend/apps/web/src/app/layout/top-nav/` + "Admin panel · Only you and other admins see this" row in `frontend/apps/web/src/app/layout/avatar-menu/` (adjust `nav-model.ts` as needed); rendered only when the probe succeeds; DESIGN.md tokens/voice
- [ ] T018 [US2] Frontend: create `frontend/apps/web/src/app/features/admin/shell/admin-shell.component.{ts,html,css}` (shield header, sidebar Overview·Users·Catalogue, back-to-app) and restructure `app.routes.ts`: lazy `/admin` → shell with children `''`=overview, `users`, `users/:handle`, `catalogue`= existing `AdminRecognitionComponent` (kept working at its new path)
- [ ] T019 [US2] Frontend: create `frontend/apps/web/src/app/features/admin/overview/admin-overview.component.{ts,html,css}`: 4 stats (mono numerals), "New players this week" → detail links, "Recently granted" list, search box navigating to `/admin/users?q=…`; loading/empty/error states per DESIGN.md; mobile 2×2 stats per wireframe 1b
- [ ] T020 [P] [US2] e2e `frontend/apps/web-e2e/src/admin-area.spec.ts`: admin sees entry + overview renders; non-admin sees no entry and `/admin` redirects home

**Checkpoint**: admin area exists as a place; overview live.

---

## Phase 5: User Story 3 — Find and open any player (P2)

**Goal**: `/admin/users` search (name/@handle/team), status filter (All/Active/Suspended/Banned), paginated table → mobile cards; rows open the detail; banned players findable here only.

**Independent Test**: quickstart Scenario 3.

- [ ] T021 [US3] Create `backend/Services/Admin/IAdminUserService.cs` + `AdminUserService.cs` with `SearchAsync(q, status, PaginationRequest)`: `IgnoreQueryFilters` + `AsNoTracking`, case-insensitive contains over display name/handle/team name, status filter, projection to list items (teams, badge count, `isAdmin` from role membership, status), `PagedResult` envelope; register in `Program.cs`
- [ ] T022 [US3] Create `backend/Dtos/Admin/AdminUserDtos.cs` (list item; detail DTO added in US4) and `backend/Controllers/Admin/AdminUsersController.cs` with `GET api/v1/admin/users` per contracts/admin-api.md
- [ ] T023 [P] [US3] Integration tests `backend/tests/JuggerHub.Api.IntegrationTests/Admin/AdminUsersListTests.cs`: authz, search by each field, each status filter (banned included here), combined search+filter, pagination envelope accuracy, admin marker
- [ ] T024 [US3] Frontend: create `frontend/apps/web/src/app/features/admin/users/admin-users.component.{ts,html,css}` per wireframe 1c: search-first input (reads `?q=`), filter chips, desktop table (player/teams/status/badges) folding to tappable cards on mobile, status color-coding, admin chip, pagination ("Showing X–Y of Z"), friendly empty state; row → `/admin/users/:handle`; extend `admin.service.ts` with `searchUsers()`
- [ ] T025 [P] [US3] e2e in `frontend/apps/web-e2e/src/admin-area.spec.ts`: search → filter → open row lands on detail

**Checkpoint**: any player reachable in seconds (SC-003 path complete).

---

## Phase 6: User Story 4 — One player, everything an admin needs (P2)

**Goal**: Player admin detail (identity/teams/pompfen/last-active/activity/status) + account actions: suspend (login-block only), reinstate, ban (soft-delete + implicit re-registration block), unban, send reset link — enforced in auth flows, recorded in `AdminActionRecord`, admin-shielded per FR-019.

**Independent Test**: quickstart Scenario 4.

- [ ] T026 [US4] Extend `AdminUserService` with `GetDetailAsync(handle)`: `IgnoreQueryFilters`, identity + joined date + teams(with slugs) + pompfen + last-active (newest participation activity or null) + recent activity (feature-003 data, capped) + status/statusChangedAt/isAdmin/userId; extend `backend/Dtos/Admin/AdminUserDtos.cs` with `AdminUserDetailDto`
- [ ] T027 [US4] Extend `AdminUserService` with account actions per data-model.md state machine: `SuspendAsync`/`ReinstateAsync`/`BanAsync`/`UnbanAsync`/`SendPasswordResetAsync(actorId, handle)` — transition validation (409 conflicts), FR-019 shield (422: target in role or self), `AdminActionRecord` written in the same `SaveChanges`, `IRefreshTokenService.RevokeAllForUserAsync` on suspend/ban, reset via password-reset token + `AuthEmailService.SendPasswordResetEmailAsync` (always-sent semantics)
- [ ] T028 [US4] Extend `backend/Controllers/Admin/AdminUsersController.cs`: `GET admin/users/{handle}` (404 mapping) + `POST admin/users/{handle}/suspend|reinstate|ban|unban|reset-password` (204/404/409/422 per contracts/admin-api.md)
- [ ] T029 [US4] Enforce statuses in `backend/Services/Auth/AuthService.cs` (+ `AuthResults.cs`/`AuthController.cs`/auth DTOs as needed): `LoginAsync` — after correct password, Suspended → distinct "account suspended" 403 result (parallel to needs-verification), Banned → generic failure; `RefreshAsync` — non-Active → rejected; `RegisterAsync` — skip resend-verification courtesy for non-Active existing accounts (response stays neutral)
- [ ] T030 [P] [US4] Integration tests `backend/tests/JuggerHub.Api.IntegrationTests/Admin/AccountActionTests.cs`: every transition + idempotence conflicts, shield rules (admin target, self), records written with actor/target/action, refresh tokens revoked on suspend/ban, reset-password sends mail + records
- [ ] T031 [P] [US4] Integration tests `backend/tests/JuggerHub.Api.IntegrationTests/Admin/AccountEnforcementTests.cs`: login/refresh outcomes per status (incl. distinct suspended message, generic banned failure); ban invisibility per public surface — `/u/{handle}` 404, player browse, team roster, event participants (SC-005); suspended stays fully visible; banned email re-registration neutral + no account + no verification mail; unban restores visibility and login
- [ ] T032 [US4] Frontend: create `frontend/apps/web/src/app/features/admin/user-detail/admin-user-detail.component.{ts,html,css}` per wireframe 1d: identity card (teams, plays, last active, player id, activity), status chip, account-help cards with plain-language descriptions + confirm dialogs (suspend/reinstate, send reset link, ban/unban), 422 shield explanation surfaced; mobile order: badges → account → identity; extend `admin.service.ts` with detail + action methods
- [ ] T033 [US4] Frontend: show the distinct "account suspended" message on the sign-in screen in `frontend/apps/web/src/app/features/auth/sign-in/` (map the new 403 result; DESIGN.md voice)
- [ ] T034 [P] [US4] e2e in `frontend/apps/web-e2e/src/admin-area.spec.ts`: suspend → sign-in refused with suspended message → reinstate; ban → public profile gone → unban restores

**Checkpoint**: account help complete and enforced server-side.

---

## Phase 7: User Story 5 — Grant badges & achievements from the player's detail (P3)

**Goal**: Awards section on the detail with per-item revoke and an Assign picker (badges/achievements tabs, "Given" marking, optional note) reusing 012 endpoints; wireframe 1e.

**Independent Test**: quickstart Scenario 5.

- [ ] T035 [US5] Frontend: awards section + Assign picker (dialog on desktop, bottom-sheet on mobile) in `frontend/apps/web/src/app/features/admin/user-detail/` (sub-component if cleaner, e.g. `assign-picker.component.{ts,html,css}`): catalogue tabs via existing `recognition-admin.service.ts`, already-held items marked "Given"/disabled via `admin/players/{handle}/awards`, optional note field, grant + revoke with confirm, list refresh; grants surface in overview "Recently granted"
- [ ] T036 [P] [US5] e2e in `frontend/apps/web-e2e/src/admin-area.spec.ts`: assign with note → appears on detail; revoke → gone; "Given" items not grantable

**Checkpoint**: all five stories functional.

---

## Phase 8: Polish & Cross-Cutting

- [ ] T037 [P] Extend `backend/Data/DevDataSeeder.cs` with stable dev fixtures: a suspended and a banned sample account (+ note in `.env.sample` for `ADMIN_EMAILS` if not already present) so quickstart/e2e have data
- [ ] T038 Instantiate `.specify/templates/ui-review-checklist-template.md` → `specs/013-admin-area/checklists/ui-review.md` and verify every item against the UI diff (DESIGN.md wins conflicts) — constitution quality gate 7
- [ ] T039 Full verification: `dotnet test` (all suites), `dotnet build backend`, `npx nx test web`, `npx nx build web`, `npx nx e2e web-e2e`; walk quickstart Scenarios 1–5 against the running stack
- [ ] T040 Close out: comment on GitHub issue #21 (what shipped, mirror semantics, no runtime grant/revoke this pass), note any spec/design drift in the PR description

---

## Dependencies & Execution Order

- **Phase 1 → Phase 2 → everything**: T001–T006 block all stories (schema + filter).
- **US1 (Phase 3)** blocks nothing functionally but is the MVP and the security boundary — do it first; US2–US5 admin endpoints assume the role-based policy exists.
- **US2 (Phase 4)** provides the shell + `admin.service.ts` that US3/US4/US5 UI mounts into.
- **US3 (Phase 5)** provides the route into **US4 (Phase 6)**; backend halves of US3/US4 are parallelizable after US1.
- **US5 (Phase 7)** needs US4's detail page; backend already exists (012).
- **Phase 8** last.

Within stories: services → controllers → tests can run alongside frontend tasks ([P] marks file-disjoint work).

## Parallel Opportunities

- T003 ∥ T002; T010/T011 ∥ T012 finish; T015 ∥ T016–T019; T023 ∥ T024; T030/T031 ∥ T032–T033; T036 ∥ T037.
- Backend (T021–T023, T026–T031) and frontend (T024, T032–T034) tracks can proceed in parallel per story once contracts are fixed.

## Implementation Strategy

**MVP = Phase 1–3 (US1)**: the role replaces the interim gate — shippable alone, closes issue #21's core. Then incremental: US2 (place + overview) → US3 (find) → US4 (account help) → US5 (assign) — each independently testable per its quickstart scenario, with a checkpoint commit per phase.
