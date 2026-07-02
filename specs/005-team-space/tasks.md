---
description: "Task list for Team Space & Member Handling"
---

# Tasks: Team Space & Member Handling

**Input**: Design documents from `/specs/005-team-space/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/openapi.yaml, quickstart.md

**Tests**: INCLUDED — the plan's Testing section requires xUnit integration + Jest unit + Playwright e2e.

**Organization**: Tasks are grouped by user story (US1–US6) for independent implementation and testing. Backend namespace `JuggerHub`; frontend Nx app at `frontend/apps/web/src/app`. Shared `PaginationRequest`/`PagedResult<T>` already exist (feature 003).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US6 (Setup/Foundational/Polish carry no story label)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Enums, options, and model skeletons used across stories.

- [x] T001 [P] Create team enums (`TeamType {CityTeam,Mixteam}`, `TeamRole {Member,Admin}`, `InvitationKind {Link,Targeted}`, `InvitationStatus {Pending,Accepted,Declined,Revoked}`) in `backend/Entities/TeamEnums.cs`
- [x] T002 [P] Add `TeamOptions` (InviteLinkTtlDays=7, slug min/max length, news body cap) in `backend/Common/TeamOptions.cs`; bind in `backend/Program.cs` with safe defaults
- [x] T003 [P] Create the frontend team model skeleton (enums + DTO interfaces) in `frontend/apps/web/src/app/core/models/team.models.ts`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Schema, DTOs, and service seams every story depends on.

**⚠️ CRITICAL**: No user-story work begins until this phase is complete.

- [x] T004 [P] Create `Team : BaseEntity` (Slug immutable, Name, Type, City? + Memberships/Invitations/News navs) in `backend/Entities/Team.cs`
- [x] T005 [P] Create `TeamMembership : BaseEntity` (TeamId, UserId, Role, JoinedDate + Team/User navs) in `backend/Entities/TeamMembership.cs`
- [x] T006 [P] Create `TeamInvitation : BaseEntity` (TeamId, Kind, Token, Status, ExpiresDate, CreatedByUserId, TargetUserId? + navs) in `backend/Entities/TeamInvitation.cs`
- [x] T007 [P] Create `TeamNewsPost : BaseEntity` (TeamId, AuthorUserId, Body + navs) in `backend/Entities/TeamNewsPost.cs`
- [x] T008 Extend `EventParticipation` with nullable `TeamId` (FK → Team) + `Team?` nav in `backend/Entities/EventParticipation.cs` (keep `TeamLabel` as display snapshot)
- [x] T009 Configure DbSets + model config in `backend/Data/AppDbContext.cs`: unique `Team.Slug`, unique `TeamInvitation.Token`, unique (`TeamId`,`UserId`), index (`TeamId`,`Role`) & (`UserId`), partial-unique active `Link`, partial-unique pending `Targeted` per (`TeamId`,`TargetUserId`), `TeamNewsPost` (`TeamId`,`CreatedDate`) index, `EventParticipation.TeamId` FK `OnDelete(SetNull)` + index, string lengths, Team→memberships/invites/news cascade (depends on T004–T008)
- [x] T010 Generate EF migration `AddTeams` in `backend/Data/Migrations/` (depends on T009)
- [x] T011 [P] Implement `TeamSlugPolicy` (normalize + format regex + team reserved-word set {t,teams,team,new,join,invitations,settings,members,api,admin,…} + length) in `backend/Services/Teams/TeamSlugPolicy.cs`
- [x] T012 [P] Create team DTOs (`CreateTeamRequest`, `SetMemberRoleRequest`, `CreateTargetedInviteRequest`, `TeamDetailDto`, `TeamPublicDto`, `TeamMemberDto`, `TeamNewsDto`, `TeamInvitationDto`, `InviteLinkDto`, `InvitableUserDto`, `InvitePreviewDto`, `AcceptInviteResultDto`, `SlugAvailabilityDto`) in `backend/Dtos/Teams/TeamDtos.cs`
- [x] T013 [P] Add `TeamMapping` Mapster config (entities → DTOs; separate public/internal projections that never load roster/news for public callers) in `backend/Services/Teams/TeamMapping.cs`; register in `AddMappingConfig`
- [x] T014 Implement `TeamMembershipGuard` (resolve caller membership/role for a slug; **transactional last-admin count check** under RepeatableRead/Serializable) in `backend/Services/Teams/TeamMembershipGuard.cs` (depends on T004–T010)
- [x] T015 Define `ITeamService` + `TeamService` skeleton (create, get detail, public, delete, roster, set-role, remove, step-down) in `backend/Services/Teams/`; register DI in `backend/Program.cs` (depends on T012–T014)
- [x] T016 Define `ITeamInvitationService` + skeleton (link get/rotate/revoke, targeted create, list, user-search, preview, accept, decline) in `backend/Services/Teams/`; register DI (depends on T012–T014)
- [x] T017 [P] Define `ITeamActivityService`/`TeamActivityService` (team events paged by TeamId) and `ITeamNewsService`/`TeamNewsService` (news feed paged) skeletons in `backend/Services/Teams/`; register DI
- [x] T018 [P] Add frontend team models (Team detail/public, Member, Invitation, InviteLink, InvitablePreview, SlugAvailability + enums) in `frontend/apps/web/src/app/core/models/team.models.ts`
- [x] T019 [P] Add `TeamService` skeleton (teams/members/invitations/accept — signals) in `frontend/apps/web/src/app/core/services/team.service.ts`

**Checkpoint**: Schema migrates, service seams exist — stories can begin.

---

## Phase 3: User Story 1 - Create a team and become its admin (Priority: P1) 🎯 MVP

**Goal**: Any signed-in user creates a city team or Mixteam with a unique immutable slug and becomes its first admin.

**Independent Test**: Create a city team (with city) and a Mixteam (no city) → each created, creator is sole admin, lands on `/t/<slug>`; duplicate/reserved/malformed slug rejected; slug immutable.

### Tests for User Story 1 ⚠️

- [x] T020 [P] [US1] Integration tests (create city team + Mixteam; creator becomes admin; duplicate slug → 409; malformed/reserved slug → 400; city required for CityTeam / rejected for Mixteam; slug immutability — no mutating endpoint) in `backend/tests/JuggerHub.Api.IntegrationTests/Teams/CreateTeamTests.cs`
- [x] T021 [P] [US1] Integration test for `GET /teams/slug-available` (available/taken/reserved/malformed) in `backend/tests/JuggerHub.Api.IntegrationTests/Teams/SlugAvailabilityTests.cs`

### Implementation for User Story 1

- [x] T022 [US1] Implement `TeamService.CreateAsync` (validate slug via `TeamSlugPolicy` + uniqueness pre-check; city rule; create `Team` + creator `Admin` `TeamMembership` via explicit `DbSet.Add` in a transaction) and `CheckSlugAsync` in `backend/Services/Teams/TeamService.cs` (depends on T015)
- [x] T023 [US1] Create `TeamsController` with `POST /api/v1/teams` (create → 201 `TeamDetailDto`) and `GET /api/v1/teams/slug-available` (auth) in `backend/Controllers/TeamsController.cs` (depends on T022)
- [x] T024 [US1] Implement `team.service.ts` `createTeam` + `checkSlug` methods in `frontend/apps/web/src/app/core/services/team.service.ts` (depends on T019)
- [x] T025 [US1] Create `team-create` component (name, team-address/slug with live availability, City-team/Mixteam toggle with conditional city field) in `frontend/apps/web/src/app/features/teams/team-create/team-create.component.{ts,html,css}` (depends on T024)
- [x] T026 [US1] Add guarded route `/teams/new` (in shell, `authGuard`) + a "Create team" entry point in the sidebar/dashboard in `frontend/apps/web/src/app/app.routes.ts` and `frontend/apps/web/src/app/layout/` (depends on T025)
- [ ] T027 [P] [US1] Unit test for `team-create` (slug validation/availability; Mixteam hides city) in `frontend/apps/web/src/app/features/teams/team-create/team-create.component.spec.ts`

**Checkpoint**: A user can create a team and is its admin; MVP deployable.

---

## Phase 4: User Story 2 - Visit the team space (members, activity, news) (Priority: P1)

**Goal**: A member opens `/t/<slug>` and sees the cover header + Members/Activity/News tabs (Trainings disabled); non-members are blocked; public info is anonymous.

**Independent Test**: As a member, all three tabs render correct bounded data with empty states and role tags/positions; a non-member/unknown team → 404 (no roster/news); `/{slug}/public` returns only name/type/city/count.

### Tests for User Story 2 ⚠️

- [x] T028 [P] [US2] Integration test: `GET /teams/{slug}` (member) returns detail; **non-member and unknown both → 404** (no oracle); `GET /teams/{slug}/public` (anon) returns only name/type/city/memberCount (no roster/news) in `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamVisibilityTests.cs`
- [x] T029 [P] [US2] Integration test: roster (paged; role tags; pompfen from profile), activity (by `TeamId`, `Event.Date` desc, empty state, bounded), news (paged, author role) in `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TeamTabsTests.cs`

### Implementation for User Story 2

- [x] T030 [US2] Implement `TeamService` detail (member-gated via `TeamMembershipGuard`), public projection, and roster (paged; project `User.Profile` for name/handle/pompfen/hasAvatar) in `backend/Services/Teams/TeamService.cs` (depends on T015, T014)
- [x] T031 [US2] Implement `TeamActivityService.GetForTeamAsync` (participations `WHERE TeamId`, join `Event`, date desc, `ActivityItemDto`, `PagedResult<T>`) in `backend/Services/Teams/TeamActivityService.cs` (depends on T017, T008)
- [x] T032 [US2] Implement `TeamNewsService.GetFeedAsync` (paged, author-role join, `TeamNewsDto`, newest-first) in `backend/Services/Teams/TeamNewsService.cs` (depends on T017)
- [x] T033 [US2] Add endpoints to `TeamsController`: `GET {slug}` (member), `GET {slug}/public` (anon), `GET {slug}/members`, `GET {slug}/activity`, `GET {slug}/news` (member, paginated) in `backend/Controllers/TeamsController.cs` (depends on T030–T032)
- [x] T034 [US2] Add `team.service.ts` detail/public/members/activity/news methods in `frontend/apps/web/src/app/core/services/team.service.ts` (depends on T019)
- [x] T035 [US2] Create `team-detail` component (cover header, member count, underline tabs Members/Activity/News + disabled Trainings) at `/t/:slug` in `frontend/apps/web/src/app/features/teams/team-detail/team-detail.component.{ts,html,css}` (depends on T034)
- [x] T036 [P] [US2] Create `roster`, `activity-list`, `news-feed` tab-body components (role tags; positions via `pompfen.catalog`; empty states) in `frontend/apps/web/src/app/features/teams/team-detail/components/*.{ts,html,css}`
- [x] T037 [US2] Add guarded route `/t/:slug` (in shell, `authGuard`) + a "Teams" nav entry in `frontend/apps/web/src/app/app.routes.ts` and `frontend/apps/web/src/app/layout/` (depends on T035)
- [x] T038 [P] [US2] Extend `DevDataSeeder` with demo teams (a CityTeam "Rheinfeuer" + a Mixteam), memberships (mixed roles), a few news posts, and `EventParticipation.TeamId` backfill so Activity/News render (Development only) in `backend/Data/DevDataSeeder.cs` (depends on T010)

**Checkpoint**: The team space renders for members; public info is safe; non-members blocked.

---

## Phase 5: User Story 3 - Invite people by link or by searching users (Priority: P2)

**Goal**: An admin shares a single reusable invite link (7-day, rotate/revoke) or invites users directly by search (emailed), and manages the pending list.

**Independent Test**: Copy/rotate/revoke the link; search a user → targeted invite created + email sent (Mailpit); already-invited/member surfaced; pending list shows validity; revoke cancels; all admin-only.

### Tests for User Story 3 ⚠️

- [x] T039 [P] [US3] Integration test: link create/rotate (≤1 active via partial-unique) + revoke + 7-day expiry; admin-only (non-admin → 403) in `backend/tests/JuggerHub.Api.IntegrationTests/Teams/InviteLinkTests.cs`
- [x] T040 [P] [US3] Integration test: targeted invite creates + emails (assert `IEmailSender` invoked with a `/join/{slug}/{token}` link); duplicate → 200 already-invited (no dup, partial-unique); existing member → 400; `user-search` annotates Invitable/Invited/Member in `backend/tests/JuggerHub.Api.IntegrationTests/Teams/TargetedInviteTests.cs`

### Implementation for User Story 3

- [x] T041 [US3] Implement `TeamInvitationService`: `CreateOrRotateLinkAsync`, `GetActiveLinkAsync`, `RevokeAsync`, `CreateTargetedAsync` (dup/member guards), `ListPendingAsync`, `SearchUsersAsync` (annotate relation) — high-entropy token via `RandomNumberGenerator`, expiry from `TeamOptions` in `backend/Services/Teams/TeamInvitationService.cs` (depends on T016)
- [x] T042 [US3] Create `TeamEmailService` (render template, build `{FrontendBaseUrl}/join/{slug}/{token}`, send via `IEmailSender`) in `backend/Services/Email/TeamEmailService.cs` + `team-invite.html` (extends base header/footer) in `backend/EmailTemplates/`; add `GenerateTeamInviteEmailAsync` to `IEmailTemplateService`/impl; register DI in `backend/Program.cs`
- [x] T043 [US3] Wire `TeamEmailService` into `CreateTargetedAsync` (send on create) (depends on T041, T042)
- [x] T044 [US3] Add invitation endpoints to `TeamsController` (all admin): `GET`/`POST {slug}/invitations/link`, `POST {slug}/invitations` (targeted), `GET {slug}/invitations` (list, paged), `DELETE {slug}/invitations/{id}` (revoke), `GET {slug}/invitations/user-search` (paged) in `backend/Controllers/TeamsController.cs` (depends on T041)
- [x] T045 [US3] Add `team.service.ts` invitation methods (link get/rotate/revoke, targeted create, list, user-search) in `frontend/apps/web/src/app/core/services/team.service.ts` (depends on T019)
- [x] T046 [US3] Create `team-invitations` component (pending list with validity; copy/rotate/revoke link) in `frontend/apps/web/src/app/features/teams/team-invitations/team-invitations.component.{ts,html,css}` (depends on T045)
- [x] T047 [US3] Create `team-invite-people` component (copy link + user search → invite; Invited/Member states) in `frontend/apps/web/src/app/features/teams/team-invite-people/team-invite-people.component.{ts,html,css}` (depends on T045)
- [x] T048 [US3] Add routes `/t/:slug/invitations` (+ invite-people sub-view) in shell and wire the Members-tab "Invite people" button in `frontend/apps/web/src/app/app.routes.ts` + `team-detail` (depends on T046, T047)

**Checkpoint**: Admins can invite by link and by search; invites appear and are revocable.

---

## Phase 6: User Story 4 - Accept or decline an invitation (Priority: P2)

**Goal**: An invitee opens a token URL, sees a public preview + inviter, and accepts (joins as member) or declines; expired/revoked/used shows a terminal state.

**Independent Test**: Open a valid link/email → preview renders (no roster/news) → Accept adds you as member; open an expired/revoked link → terminal state; signed-out open → sign-in/register then return to accept.

### Tests for User Story 4 ⚠️

- [x] T049 [P] [US4] Integration test: `GET /invitations/{token}` preview (Usable/Expired/Invalid; public fields + inviter; no roster/news); `accept` adds member + is idempotent when already a member; expired/revoked/used → 409; link stays usable for other users while a targeted invite is consumed; `decline` in `backend/tests/JuggerHub.Api.IntegrationTests/Teams/AcceptDeclineTests.cs`

### Implementation for User Story 4

- [x] T050 [US4] Implement `TeamInvitationService`: `GetPreviewAsync` (anon; usable/expired/invalid), `AcceptAsync` (auth; usable check; add `Member` membership idempotently; link stays `Pending`, targeted → `Accepted`), `DeclineAsync` in `backend/Services/Teams/TeamInvitationService.cs` (depends on T016, T041)
- [x] T051 [US4] Create `InvitationsController`: `GET /api/v1/invitations/{token}` (anon), `POST /accept`, `POST /decline` (auth) in `backend/Controllers/InvitationsController.cs` (depends on T050)
- [x] T052 [US4] Add `team.service.ts` invite preview/accept/decline methods in `frontend/apps/web/src/app/core/services/team.service.ts` (depends on T019)
- [x] T053 [US4] Create `invite-accept` component (full-screen, outside shell): public preview, Accept & join / Decline, expired/invalid state, sign-in/register return handling in `frontend/apps/web/src/app/features/teams/invite-accept/invite-accept.component.{ts,html,css}` (depends on T052)
- [x] T054 [US4] Add anonymous route `/join/:slug/:token` (outside shell) in `frontend/apps/web/src/app/app.routes.ts` (depends on T053)

**Checkpoint**: The invite → accept loop works end to end for a second user.

---

## Phase 7: User Story 5 - Manage roles and remove members, with a last-admin guard (Priority: P2)

**Goal**: Admins promote/demote roles, remove members, and step down — never dropping the team below one admin.

**Independent Test**: With two admins, demote one; as the sole admin, every self-demote/remove/step-down is blocked (409); a member is removed off the roster; non-admins refused.

### Tests for User Story 5 ⚠️

- [x] T055 [P] [US5] Integration test: promote/demote/remove/step-down happy paths; **last-admin guard** blocks demote/remove/step-down of the sole admin (409, no state change); **concurrency** (two admins demote each other → exactly one succeeds, ≥1 admin remains); non-admin → 403 in `backend/tests/JuggerHub.Api.IntegrationTests/Teams/RolesLastAdminTests.cs`

### Implementation for User Story 5

- [x] T056 [US5] Implement `TeamService.SetMemberRoleAsync`, `RemoveMemberAsync`, `StepDownAsync` routing through `TeamMembershipGuard`'s transactional last-admin check in `backend/Services/Teams/TeamService.cs` (depends on T015, T014)
- [x] T057 [US5] Add member-management endpoints to `TeamsController`: `PATCH {slug}/members/{userId}/role`, `DELETE {slug}/members/{userId}` (admin; self-leave allowed; 409 on last-admin) in `backend/Controllers/TeamsController.cs` (depends on T056)
- [x] T058 [US5] Add `team.service.ts` `setMemberRole`/`removeMember`/`stepDown` methods in `frontend/apps/web/src/app/core/services/team.service.ts` (depends on T019)
- [x] T059 [US5] Create the member "…" menu (make/remove admin, view profile → `/u/:handle`, remove) in `team-detail/components/member-menu` and the settings "step down" control in `frontend/apps/web/src/app/features/teams/team-settings/team-settings.component.{ts,html,css}` (depends on T058)
- [ ] T060 [P] [US5] Unit test for role/last-admin UI gating (sole-admin blocks step-down; "Make admin" ↔ "Remove admin") in `frontend/apps/web/src/app/features/teams/team-settings/team-settings.component.spec.ts`

**Checkpoint**: Roles and removal work with the last-admin invariant enforced server-side.

---

## Phase 8: User Story 6 - Delete a team (Priority: P3)

**Goal**: An admin deletes the team (irreversible), cascading roster/invites/news while preserving historical event participations.

**Independent Test**: Admin deletes → team + roster + invites + news gone, `/t/<slug>` and links 404; members' profile activity for past events remains; non-admin delete refused.

### Tests for User Story 6 ⚠️

- [x] T061 [P] [US6] Integration test: admin delete cascades memberships/invites/news; `EventParticipation.TeamId` → null (event/participation rows preserved); non-admin → 403; after delete `GET {slug}` → 404 in `backend/tests/JuggerHub.Api.IntegrationTests/Teams/DeleteTeamTests.cs`

### Implementation for User Story 6

- [x] T062 [US6] Implement `TeamService.DeleteAsync` (admin-only; single transaction; FK cascade for memberships/invites/news; participations `SetNull`) in `backend/Services/Teams/TeamService.cs` (depends on T015)
- [x] T063 [US6] Add `DELETE /api/v1/teams/{slug}` (admin) to `backend/Controllers/TeamsController.cs` (depends on T062)
- [x] T064 [US6] Add `team.service.ts` `deleteTeam` + the danger-zone delete (with confirm) to `team-settings` in `frontend/apps/web/src/app/features/teams/team-settings/team-settings.component.{ts,html,css}` + route `/t/:slug/settings` in `app.routes.ts` (depends on T059)

**Checkpoint**: Team deletion works, is admin-only, and preserves event history.

---

## Phase 9: Polish & Cross-Cutting Concerns

- [ ] T065 [P] DESIGN.md conformance + responsiveness pass (phone ~375px, desktop ~1280px; empty/loading/error states; one coral CTA per view) across all team components
- [ ] T066 Playwright e2e `teams.spec.ts` — create → invite (link + search) → accept as 2nd user → manage roles/last-admin → delete, desktop + mobile in `frontend/apps/web-e2e/src/teams.spec.ts`
- [ ] T067 [P] Reconcile `.env.sample` / `appsettings*.json` / `docker-compose.yml` for any `Teams:*` config; confirm the global `JsonStringEnumConverter` serializes the new enums by name
- [ ] T068 Run `/speckit-analyze` cross-artifact consistency; note the create-form slug field as intentional wireframe drift; confirm the `TeamLabel`→`TeamId` change leaves profile activity (003) green
- [ ] T069 Run `quickstart.md` Scenarios A–G end-to-end (Docker) and record results

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: depends on Setup; **blocks all stories**. Entities T004–T008 parallel; then T009→T010 sequential; T011–T013, T017–T019 parallel; T014→T015/T016.
- **US1 (Phase 3)**: depends on Foundational. **MVP.**
- **US2 (Phase 4)**: depends on Foundational; needs a team to exist (US1) for data but is code-independent.
- **US3 (Phase 5)**: depends on Foundational + a team (US1); adds the invite surface.
- **US4 (Phase 6)**: depends on US3 producing invitations (accepts what US3 issues); independently testable via a seeded/issued token.
- **US5 (Phase 7)**: depends on Foundational + a roster (US1/US4); independently testable.
- **US6 (Phase 8)**: depends on Foundational; independently testable.
- **Polish (Phase 9)**: depends on the delivered stories.

### Story Independence

US1 stands alone (create). US2 reads a team. US3 issues invites. US4 consumes a token to join. US5 manages the roster. US6 deletes. Each is testable on its own once Foundational is done; later stories integrate without breaking earlier ones.

### Within Each Story

- Tests written first and expected to FAIL before implementation.
- Backend: models → guard/service → endpoints; then frontend service → component → route.

### Parallel Opportunities

- Setup: T001–T003 all [P].
- Foundational: entities T004–T008 [P]; then T009→T010; T011–T013, T017–T019 [P].
- Each story's `[P]` tests run together; a story's backend and frontend `[P]` tasks proceed in parallel.

---

## Parallel Example: Foundational entities

```bash
Task: "Create Team entity in backend/Entities/Team.cs"
Task: "Create TeamMembership entity in backend/Entities/TeamMembership.cs"
Task: "Create TeamInvitation entity in backend/Entities/TeamInvitation.cs"
Task: "Create TeamNewsPost entity in backend/Entities/TeamNewsPost.cs"
```

---

## Implementation Strategy

### MVP First (US1 + US2)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 (create) → 4. Phase 4 US2 (view the space) → **STOP & VALIDATE**: a user creates a team and sees its page → demo.

### Incremental Delivery

Foundation → US1 (create) → US2 (team page) → US3 (invite) → US4 (accept) → US5 (roles/guard) → US6 (delete) → Polish. Each story adds value without breaking the previous.

---

## Notes

- [P] = different files, no incomplete-task dependency. [Story] labels map to spec.md US1–US6.
- **Security invariants to keep green** (contracts/README): internal reads refused to non-members (404, no oracle) (FR-040/SC-002); public/preview carry only name/type/city/count(+inviter) (FR-041/042); all mutations authorized server-side by membership+role (FR-037/SC-002); slug immutable + unique (SC-009); last-admin guard atomic (FR-017/SC-005); expired/revoked invites never admit (SC-004); no duplicate membership; delete preserves event history (FR-036/SC-007); all lists paginated (FR-038).
- **Spec drift to flag at PR**: the create form adds a "team address" (slug) field vs the offline wireframe (name/type/city only) — intentional, recorded in `## Clarifications`. The `TeamLabel`→`TeamId` addition touches the 003 activity path; keep profile activity green.
- Commit after each task or logical group; stop at any checkpoint to validate independently.
