---
description: "Task list for feature 014 — Admin catalogue management"
---

# Tasks: Admin catalogue management (badges, achievements & team awards)

**Input**: Design documents from `/specs/014-admin-catalogue/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/admin-catalogue-api.md](contracts/admin-catalogue-api.md)

**Tests**: Included — the repo's quality gates (constitution) and the spec's
success criteria require xUnit integration, Angular component/service, and
Playwright e2e coverage.

**Organization**: Grouped by user story (US1–US6 from the spec) so each is an
independently testable increment. All backend routes stay behind the
`PlatformAdmin` policy; all UI follows DESIGN.md.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no incomplete dependency)
- **[Story]**: US1…US6 (setup/foundational/polish carry no story label)

## Path Conventions

Web app: backend at `backend/`, Angular at `frontend/apps/web/src/app/`, e2e at
`frontend/apps/web-e2e/src/`.

---

## Phase 1: Setup

- [ ] T001 Confirm a green baseline before changes: `dotnet build backend/JuggerHub.sln`, `dotnet test backend/tests/JuggerHub.Api.IntegrationTests`, `nx test web`, `nx lint web`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared backend read fields and the frontend service/model surface that
every catalogue story builds on. **No story work starts until this is complete.**

- [ ] T002 [P] Add `GrantedCount` (int) and `CreatedAt` (DateTime) to `BadgeDefinitionDto` in `backend/Dtos/Badges/BadgeDtos.cs`.
- [ ] T003 [P] Add `GrantedCount` (int) and `CreatedAt` (DateTime) to `AchievementDefinitionDto` in `backend/Dtos/Achievements/AchievementDtos.cs`.
- [ ] T004 Update `ListDefinitionsAsync` projection to populate `GrantedCount` (COUNT of `Status == Active` awards) and `CreatedAt` (`CreatedDate`) in `backend/Services/Badges/BadgeService.cs`.
- [ ] T005 Update `ListDefinitionsAsync` projection likewise (and any create/update return path so a new type reports `GrantedCount = 0`) in `backend/Services/Achievements/AchievementService.cs`.
- [ ] T006 [P] Extend `RecognitionDefinition` with `grantedCount: number` and `createdAt: string` in `frontend/apps/web/src/app/core/models/recognition.models.ts`.
- [ ] T007 Add all admin-catalogue methods (`createBadge`/`createAchievement`, `updateBadge`/`updateAchievement`, `retireBadge`/`retireAchievement`, `reinstateBadge`/`reinstateAchievement`, `setBadgeIcon`/`setAchievementIcon` with a raw `File`/`Blob` body, `removeBadgeIcon`/`removeAchievementIcon`) to `frontend/apps/web/src/app/core/services/recognition-admin.service.ts`.
- [ ] T008 [P] Unit spec asserting each new service method hits the right URL/verb/body in `frontend/apps/web/src/app/core/services/recognition-admin.service.spec.ts`.

**Checkpoint**: catalogue list data (count + date) is available; the client service
can drive every catalogue action.

---

## Phase 3: User Story 1 — Browse both catalogues (Priority: P1) 🎯 MVP

**Goal**: Replace the placeholder with a real, filterable list of both catalogues
showing icon, name, description, applies-to, grant count, and status.

**Independent Test**: Open `/admin/catalogue`, toggle Badges⇄Achievements, filter
All/Active/Retired, and confirm the table folds into cards on a narrow viewport.

- [ ] T009 [P] [US1] Integration test: `GET /admin/badges` returns `grantedCount` (active-only) and `createdAt`, and excludes retired unless `includeRetired=true`, in `backend/tests/JuggerHub.Api.IntegrationTests/Recognition/BadgeCatalogueListTests.cs`.
- [ ] T010 [P] [US1] Integration test: same assertions for `GET /admin/achievements` in `backend/tests/JuggerHub.Api.IntegrationTests/Recognition/AchievementCatalogueListTests.cs`.
- [ ] T011 [US1] Rewrite the component (signals for kind toggle + `all|active|retired` filter, load via `RecognitionAdminService`, loading/empty/error states) in `frontend/apps/web/src/app/features/admin/catalogue/admin-catalogue.component.ts`.
- [ ] T012 [US1] Build the list template: desktop table (icon+name+description · applies-to · grant count · status · actions) folding into mobile cards, plus the catalogue toggle and filter chips, in `frontend/apps/web/src/app/features/admin/catalogue/admin-catalogue.component.html`.
- [ ] T013 [US1] Add any needed styles (retired-row treatment, icon frame) in `frontend/apps/web/src/app/features/admin/catalogue/admin-catalogue.component.css`.
- [ ] T014 [P] [US1] Component spec: toggle switches catalogue, filter narrows results, count/status render, in `frontend/apps/web/src/app/features/admin/catalogue/admin-catalogue.component.spec.ts`.

**Checkpoint**: US1 fully functional and testable on its own (read-only oversight).

---

## Phase 4: User Story 2 — Create a new type (Priority: P2)

**Goal**: Add a badge or achievement (kind, name, description, applies-to) that
appears immediately and becomes grantable.

**Independent Test**: Create a players badge; confirm it lists as Active with
`granted 0` and is offered in a player's Assign picker.

- [ ] T015 [US2] Add the create modal (Kind selector, name, description, applies-to with ≥1 required; POST via service by kind; refresh list; disabled/submitting states) in `admin-catalogue.component.ts` + `admin-catalogue.component.html`.
- [ ] T016 [P] [US2] Integration test: `POST /admin/badges` returns `grantedCount: 0` and the created type appears in the list, in `backend/tests/JuggerHub.Api.IntegrationTests/Recognition/BadgeCreateTests.cs` (extend if an equivalent exists).
- [ ] T017 [P] [US2] Component spec: submitting with no applies-to is rejected; a valid submit adds a row, in `admin-catalogue.component.spec.ts`.

**Checkpoint**: US1 + US2 both work; admins can curate new types.

---

## Phase 5: User Story 3 — Edit an existing type (Priority: P2)

**Goal**: Edit name/description/applies-to via the same form pre-filled, with Kind
locked and grant count + created date shown.

**Independent Test**: Edit a badge's description; confirm the list updates and the
Kind control is not changeable.

- [ ] T018 [US3] Extend the shared form modal with an edit mode (pre-fill from the row, lock Kind, show grant count + created date, PUT via service) in `admin-catalogue.component.ts` + `admin-catalogue.component.html`.
- [ ] T019 [P] [US3] Component spec: edit pre-fills fields, Kind control is disabled, save reflects new values, in `admin-catalogue.component.spec.ts`.

**Checkpoint**: create and edit share one form; US1–US3 independent.

---

## Phase 6: User Story 4 — Give a type an icon (Priority: P2)

**Goal**: Upload/replace/remove an icon with a live, shape-masked preview.

**Independent Test**: Upload a square PNG (preview at 32/40/56), confirm it shows in
list + picker; replace it; remove it (placeholder returns); reject a non-image.

- [ ] T020 [P] [US4] Add `RemoveIconAsync` to `IBadgeService` + `BadgeService` (delete the `BadgeIcon` row; 404 if no definition) in `backend/Services/Badges/`.
- [ ] T021 [P] [US4] Add `RemoveIconAsync` to `IAchievementService` + `AchievementService` in `backend/Services/Achievements/`.
- [ ] T022 [US4] Add `DELETE {definitionId}/icon` to `BadgesAdminController` and `AchievementsAdminController` in `backend/Controllers/Admin/`.
- [ ] T023 [P] [US4] Integration tests: replace icon (existing PUT), then remove → `hasIcon` false and the public icon endpoint 404s, in `backend/tests/JuggerHub.Api.IntegrationTests/Recognition/RecognitionIconAdminTests.cs`.
- [ ] T024 [US4] Add the icon editor modal (drag/drop + file picker, `URL.createObjectURL` preview at 32/40/56 masked circle vs rounded-square, upload raw `File` / replace / remove via service) in `admin-catalogue.component.ts` + `admin-catalogue.component.html` + `admin-catalogue.component.css`.
- [ ] T025 [P] [US4] Component spec: preview appears on select, remove calls the service, unsupported file shows a client hint (server remains the authority), in `admin-catalogue.component.spec.ts`.

**Checkpoint**: types are illustratable end-to-end.

---

## Phase 7: User Story 5 — Retire and reinstate a type (Priority: P2)

**Goal**: Retire (reversible, amber confirm) and reinstate; awards on holders are
never touched.

**Independent Test**: Retire an active type → gone from the Assign picker, still on
holders; reinstate → back in the picker.

- [ ] T026 [P] [US5] Add `ReinstateDefinitionAsync` (set `IsRetired = false`; `bool` result) to `IBadgeService` + `BadgeService` in `backend/Services/Badges/`.
- [ ] T027 [P] [US5] Add `ReinstateDefinitionAsync` to `IAchievementService` + `AchievementService` in `backend/Services/Achievements/`.
- [ ] T028 [US5] Add `POST {definitionId}/reinstate` to `BadgesAdminController` and `AchievementsAdminController` in `backend/Controllers/Admin/`.
- [ ] T029 [P] [US5] Integration tests: retire then reinstate flips `isRetired` and an existing award survives both; reinstate on a missing id → 404, in `backend/tests/JuggerHub.Api.IntegrationTests/Recognition/RecognitionLifecycleTests.cs`.
- [ ] T030 [US5] Add the retire confirm (amber, plain-spoken per spec FR-013) and the reinstate action (row action for retired types; also from the edit form) via service, refreshing the list, in `admin-catalogue.component.ts` + `admin-catalogue.component.html`.
- [ ] T031 [P] [US5] Component spec: retire marks the type Retired and it drops from the Active filter; reinstate restores it, in `admin-catalogue.component.spec.ts`.

**Checkpoint**: full type lifecycle; catalogue CRUD complete (US1–US5).

---

## Phase 8: User Story 6 — Assign & revoke team awards (Priority: P2)

**Goal**: A dedicated admin teams area (search list + `/admin/teams/{slug}` detail)
with the Assign picker, reusing the existing team-awards read + `teamSlug`
grant/revoke.

**Independent Test**: Admin nav → Teams → search → open a team → grant a
team-applicable badge (shows on `/t/{slug}`) → revoke it.

- [ ] T032 [P] [US6] Add `AdminTeamListItemDto` and `AdminTeamDetailDto` in `backend/Dtos/Admin/AdminTeamDtos.cs`.
- [ ] T033 [US6] Add `IAdminTeamService` + `AdminTeamService` (paginated search over `Team.Name`/`City`; detail by slug; projections + `AsNoTracking`; member/award counts) in `backend/Services/Admin/`.
- [ ] T034 [US6] Add `AdminTeamsController` (`GET /admin/teams`, `GET /admin/teams/{slug}`, `PlatformAdmin` policy, thin) in `backend/Controllers/Admin/AdminTeamsController.cs`.
- [ ] T035 [US6] Register `IAdminTeamService`→`AdminTeamService` in DI (service registration in `backend/Program.cs` or the admin services extension).
- [ ] T036 [P] [US6] Integration tests: teams search + detail shape/pagination and non-admin `401/403`, in `backend/tests/JuggerHub.Api.IntegrationTests/Admin/AdminTeamsTests.cs`.
- [ ] T037 [US6] Extract a reusable `AssignPickerComponent` (inputs: subject type `player|team`, subject ref, held awards; grants via `RecognitionAdminService`, filters to the subject's applicable non-retired types, marks held) in `frontend/apps/web/src/app/features/admin/shared/assign-picker.component.{ts,html,css}`.
- [ ] T038 [US6] Refactor `admin-user-detail` to consume `AssignPickerComponent` (behavior-preserving) in `frontend/apps/web/src/app/features/admin/user-detail/admin-user-detail.component.{ts,html}`.
- [ ] T039 [P] [US6] Add `AdminTeamListItem` and `AdminTeamDetail` models in `frontend/apps/web/src/app/core/models/admin.models.ts`.
- [ ] T040 [US6] Add `searchTeams(q, skip, take)` and `getTeamDetail(slug)` to `frontend/apps/web/src/app/core/services/admin.service.ts`.
- [ ] T041 [US6] Build the admin teams list component (search + status-free table folding to cards, links to detail) in `frontend/apps/web/src/app/features/admin/teams/admin-teams.component.{ts,html,css}`.
- [ ] T042 [US6] Build the admin team detail component (identity header + current awards + `AssignPickerComponent`) in `frontend/apps/web/src/app/features/admin/team-detail/admin-team-detail.component.{ts,html,css}`.
- [ ] T043 [US6] Add `/admin/teams` and `/admin/teams/:slug` child routes in `frontend/apps/web/src/app/app.routes.ts`, and a Teams nav entry (sidebar + mobile bottom bar, with `teamsActive()`) in `frontend/apps/web/src/app/features/admin/shell/admin-shell.component.{ts,html}`.
- [ ] T044 [P] [US6] Component specs for the teams list (search) and team detail (grant/revoke through the shared picker) beside those components.

**Checkpoint**: teams can be recognized again; all six stories independent.

---

## Phase 9: Polish & Cross-Cutting

- [ ] T045 [P] Instantiate the gate-7 UI review checklist from `.specify/templates/ui-review-checklist-template.md` into `specs/014-admin-catalogue/checklists/ui-review.md` and verify the catalogue, modals, and team surfaces against DESIGN.md (tokens, sentence case, no emoji, rounded, focus, empty/loading/error, mobile).
- [ ] T046 Extend e2e in `frontend/apps/web-e2e/src/admin-area.spec.ts`: catalogue create → edit → set icon → retire → reinstate, and a team grant → visible on `/t/{slug}` → revoke round trip.
- [ ] T047 [P] Optionally enrich `backend/Data/DevDataSeeder.cs` with a retired sample and an icon so the catalogue demonstrates all states locally.
- [ ] T048 Final verification: `dotnet test`, `nx test web`, `nx lint web`, `nx build web`, run the admin e2e; confirm **no** EF migration was generated (`dotnet ef migrations list` unchanged).

---

## Dependencies & Execution Order

- **Setup (P1)** → **Foundational (P2)** blocks everything. T004/T005 depend on
  T002/T003; T008 depends on T007.
- **US1** depends only on Foundational. **US2, US3, US4, US5** each extend the same
  catalogue component, so in practice they run in priority order (shared files),
  though their backend tasks (T020–T023, T026–T029) are independent `[P]`.
- **US6** is largely independent (its own controller/service/components); its only
  cross-touch is T038 (refactor player detail to the shared picker) which must
  follow T037.
- **Polish** depends on the stories it validates (T046 needs US1–US6; T045 needs the
  UI in place).

## Parallel Opportunities

- Foundational: T002 ∥ T003 ∥ T006 (then T004/T005, then T007→T008).
- Backend story endpoints are independent of the frontend: T020/T021 ∥ T026/T027 ∥
  T032, and their tests T023 ∥ T029 ∥ T036.
- US6 backend (T032–T036) can proceed in parallel with US1–US5 frontend work.

## Implementation Strategy

- **MVP**: Setup + Foundational + **US1** (browse) — real oversight replacing the
  placeholder. Practical first release adds **US2** (create).
- **Incremental**: US1 → US2 → US3 → US4 → US5 complete the catalogue; **US6** adds
  the teams area. Each checkpoint is independently demoable.
- Commit after each task or logical group; keep the `PlatformAdmin` boundary and
  DESIGN.md compliance in every UI task.

## Notes

- No EF migration is expected (all changes additive/computed) — T048 verifies this.
- Server is always the authority; client validation (e.g., icon type/size hints) is
  UX only.
- `[P]` = different files, no incomplete dependency; `[Story]` maps to the spec.
