---
description: "Task list for feature 023 — My team home for teamless players"
---

# Tasks: "My team" home for teamless players

**Input**: Design documents from `specs/023-my-team-home/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/me-invitations.md](./contracts/me-invitations.md), [quickstart.md](./quickstart.md)

**Tests**: Included — the spec's quickstart and the plan explicitly call for backend integration tests and frontend specs (and the constitution's quality gates expect them).

**Organization**: Grouped by user story (US1 P1 → US2 P2 → US3 P3) so each is an independently testable increment.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1 / US2 / US3 (Setup/Foundational/Polish carry no story label)

## Path Conventions

Web app: backend at `backend/`, Angular SPA at `frontend/apps/web/src/app/`. Paths below are exact.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Feature-level scaffolding. No new project/tooling — this feature extends an existing app.

- [ ] T001 [P] Instantiate the UI review checklist: copy `.specify/templates/ui-review-checklist-template.md` to `specs/023-my-team-home/checklists/ui-review.md` (Constitution Gate 7; this is a UI-bearing change).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Establish a known-green baseline before change. There is **no** new schema/migration — `TeamInvitation.TargetUserId` already exists (see [data-model.md](./data-model.md)).

**⚠️ CRITICAL**: Complete before starting user stories.

- [ ] T002 Establish a green baseline: run `dotnet build backend/JuggerHub.Api.csproj` and `npx nx test web` (from `frontend/`) and confirm both pass; confirm `TeamInvitation.TargetUserId` exists in `backend/Entities/TeamInvitation.cs` so no migration is required.

**Checkpoint**: Baseline green — user stories can begin.

---

## Phase 3: User Story 1 - A teamless player lands on a real "My team" home (Priority: P1) 🎯 MVP

**Goal**: Route players on zero teams to `/my-team` (not Browse), where they get a clear "not on a team yet" home with **Find a team** and **Create a team** actions; the "My team" destination lights up correctly.

**Independent Test**: Sign in as a zero-team player; click "My team" in the desktop top bar and the mobile bottom bar; confirm you land on `/my-team` with the destination active, and that Find/Create reach the existing Browse and create flows.

### Tests for User Story 1 ⚠️ (write first, expect fail)

- [ ] T003 [P] [US1] Update `frontend/apps/web/src/app/layout/nav-model.spec.ts`: assert `myTeamTarget([])` returns `/my-team` (was `/browse/teams`); keep the single-team (`/t/:slug`) and many-team (`/my-team`) expectations green.
- [ ] T004 [P] [US1] In `frontend/apps/web/src/app/features/my-team/my-team.component.spec.ts`, add cases: teamless + no invites renders Find + Create and **no** invites section; a player on ≥1 team still renders the existing "Your teams" chooser.

### Implementation for User Story 1

- [ ] T005 [US1] In `frontend/apps/web/src/app/layout/nav-model.ts`, change `myTeamTarget` so `teams.length === 0` returns `/my-team` instead of `/browse/teams` (single/many branches unchanged). Update the function's doc comment.
- [ ] T006 [US1] Rebuild the teamless branch of `frontend/apps/web/src/app/features/my-team/my-team.component.html` per DESIGN.md: heading + "not on a team yet" copy, a **Find a team** action (`routerLink="/browse/teams"`) and a **Create a team** action (`routerLink="/teams/new"`), with loading/empty states. Preserve the `teams().length > 0` chooser branch.
- [ ] T007 [US1] Update `frontend/apps/web/src/app/features/my-team/my-team.component.ts` so the teamless branch is driven by `membership.loaded()`/`teams()`; ensure `membership.load()` runs on init (already present) and the component exposes what the template needs.

**Checkpoint**: Teamless players reach an actionable `/my-team`; nav highlights "My team"; Find/Create work. **US1 is a shippable MVP.**

---

## Phase 4: User Story 2 - A teamless player sees and acts on their pending invitations (Priority: P2)

**Goal**: List the caller's usable (pending + unexpired) **targeted** invitations on the teamless home and let them accept/decline inline. Adds one read endpoint; accept/decline reuse the existing token endpoints.

**Independent Test**: As a teamless player with a pending targeted invite, open `/my-team`; the invite card shows team name/type/city + inviter with Accept/Decline; accept joins the team; a second invite declined disappears without joining.

### Tests for User Story 2 ⚠️ (write first, expect fail)

- [ ] T008 [P] [US2] Add `backend/tests/JuggerHub.Api.IntegrationTests/Teams/MyInvitationsTests.cs` covering `GET /api/v1/profiles/me/invitations`: 401 when anonymous; returns only the caller's invites (not another user's); excludes link invites and expired/revoked/accepted/declined invites (targeted + usable only); respects pagination (`take` > 100 normalized); and a list→accept round-trip (an invite from the list, accepted via `POST /api/v1/invitations/{token}/accept`, no longer appears and the caller has a membership).

### Implementation for User Story 2 — Backend

- [ ] T009 [P] [US2] Add the `MyInvitationDto` record in `backend/Dtos/Teams/` (fields: `Token`, `TeamName`, `TeamSlug`, `TeamType`, `City`, `MemberCount`, `InviterDisplayName`, `CreatedDate`, `ExpiresDate`) per [data-model.md](./data-model.md).
- [ ] T010 [US2] Add `Task<PagedResult<MyInvitationDto>> ListMineAsync(Guid userId, PaginationRequest pagination, CancellationToken ct = default)` to `backend/Services/Teams/ITeamInvitationService.cs`.
- [ ] T011 [US2] Implement `ListMineAsync` in `backend/Services/Teams/TeamInvitationService.cs`: `AsNoTracking`, filter `Kind == Targeted && Status == Pending && ExpiresDate > now && TargetUserId == userId`, project to `MyInvitationDto` in `.Select(...)` (member count from `Team.Memberships.Count`, inviter from `CreatedBy.Profile.DisplayName` fallback "A teammate"), order `CreatedDate` descending, return `PagedResult`. (depends on T009, T010)
- [ ] T012 [US2] Add `GET me/invitations` to `backend/Controllers/ProfilesController.cs`: inject `ITeamInvitationService`, authorize with the JwtBearer scheme, resolve the subject via the existing `TryGetUserId` (401 if absent), bind `[FromQuery] PaginationRequest`, return `Ok(PagedResult<MyInvitationDto>)`. (depends on T011)

### Implementation for User Story 2 — Frontend

- [ ] T013 [P] [US2] Add the `MyInvitation` interface to `frontend/apps/web/src/app/core/models/team.models.ts` (mirror of `MyInvitationDto`, ISO date strings) per [data-model.md](./data-model.md).
- [ ] T014 [US2] Add `frontend/apps/web/src/app/core/services/invitation.service.ts` with `listMine(skip = 0, take = 100)` → `GET /api/v1/profiles/me/invitations` returning `PagedResult<MyInvitation>`; reuse the existing `TeamService.acceptInvite(token)` / `declineInvite(token)` for actions. (depends on T013)
- [ ] T015 [US2] Add the invitations section to `frontend/apps/web/src/app/features/my-team/my-team.component.html` (shown only when teamless): one card per invite with team name/type/city + inviter and **Accept**/**Decline** buttons, per DESIGN.md (incl. empty/error states).
- [ ] T016 [US2] Wire `frontend/apps/web/src/app/features/my-team/my-team.component.ts`: when teamless, load invites via `InvitationService.listMine()`; `accept(token)` → on success remove the row; `decline(token)` → remove the row; on a stale/`409`/`404` result show a friendly message and remove the stale row (FR-015). (depends on T014)
- [ ] T017 [P] [US2] Extend `frontend/apps/web/src/app/features/my-team/my-team.component.spec.ts`: teamless + invites renders cards; accept removes the row; decline removes the row; a stale/error accept removes the row and surfaces a message.

**Checkpoint**: Invites list works end-to-end; accept joins server-side, decline removes. (Auto-navigation is added in US3.)

---

## Phase 5: User Story 3 - Joining transitions the player out of the empty state (Priority: P3)

**Goal**: After a successful accept, refresh cached memberships and navigate straight into the joined team's space so "My team" stops resolving to the empty state.

**Independent Test**: As a teamless player, accept an invite from `/my-team`; confirm you land in `/t/{slug}` and that activating "My team" again resolves to that team, not the empty state.

### Implementation for User Story 3

- [ ] T018 [US3] In `frontend/apps/web/src/app/features/my-team/my-team.component.ts`, extend the accept success path (`Joined`/`AlreadyMember`) to call `MembershipService.load()` and then `Router.navigate(['/t', teamSlug])` using the slug returned by the accept response (FR-017, FR-018). (builds on T016)
- [ ] T019 [P] [US3] Extend `frontend/apps/web/src/app/features/my-team/my-team.component.spec.ts`: a successful accept triggers `MembershipService.load()` and navigation to `/t/{slug}`.

**Checkpoint**: All three stories independently functional; accept auto-navigates and the nav resolves to the joined team.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T020 [P] Complete `specs/023-my-team-home/checklists/ui-review.md` against the diff: DESIGN.md tokens/voice, responsive (both nav bars route to `/my-team`), empty/loading/error states, and basic accessibility (keyboard/focus on Accept/Decline). Report any DESIGN.md conflicts rather than silently resolving.
- [ ] T021 [P] Run backend + frontend suites: `dotnet test backend/tests/JuggerHub.Api.IntegrationTests` and `npx nx test web` (from `frontend/`); confirm green.
- [ ] T022 Run the [quickstart.md](./quickstart.md) manual walkthrough (steps 1–8) and confirm SC-001…SC-006.
- [ ] T023 [P] Verify the `/my-team` home at desktop and mobile widths: destination active in top bar and bottom bar, no horizontal overflow, focus order sane on the invitation cards.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: after Setup; baseline gate for all stories.
- **US1 (Phase 3)**: after Foundational. Frontend-only; the MVP.
- **US2 (Phase 4)**: after Foundational. Independently testable; shares the `my-team` component with US1 (extends its teamless branch — sequence after US1 to avoid file churn, but US2 is verifiable on its own).
- **US3 (Phase 5)**: after US2 (refines US2's accept handler).
- **Polish (Phase 6)**: after the stories you intend to ship.

### User Story Dependencies

- **US1 (P1)**: no dependency on other stories.
- **US2 (P2)**: no logical dependency on US1; both touch `my-team.component.*`, so implement US1 first to avoid merge churn.
- **US3 (P3)**: depends on US2 (extends the accept path).

### Within Each Story

- Tests first (they should fail), then implementation.
- Backend: DTO → interface → service → controller (T009 → T010 → T011 → T012).
- Frontend: model → service → template → component wiring (T013 → T014 → T015/T016).

### Parallel Opportunities

- T003 and T004 (different spec files) run in parallel.
- Across layers in US2: T008 (backend test), T009 (DTO), T013 (FE model) are different files and can start together; the backend chain T010→T011→T012 is sequential; T014 depends on T013.
- Polish T020, T021, T023 are parallel; T022 (manual) after the automated suites pass.

---

## Parallel Example: User Story 2 kickoff

```bash
# Different files, can start together:
Task: "Add MyInvitationsTests.cs (backend integration) — T008"
Task: "Add MyInvitationDto record in backend/Dtos/Teams — T009"
Task: "Add MyInvitation interface in core/models/team.models.ts — T013"
```

---

## Implementation Strategy

### MVP First (US1 only)

1. Phase 1 Setup → Phase 2 Foundational.
2. Phase 3 US1 (nav routing + teamless home with Find/Create).
3. **STOP and VALIDATE**: teamless players reach an actionable `/my-team`; nav lights up. Ship — the original "feels broken" complaint is resolved.

### Incremental Delivery

1. US1 → the reachable, oriented empty state (MVP).
2. US2 → in-app pending invites (list + accept/decline).
3. US3 → seamless post-accept transition.
Each increment is independently testable and adds value without breaking the last.

---

## Notes

- No database migration (reuses `TeamInvitation.TargetUserId`).
- Accept/decline are **not** new — reuse `POST /api/v1/invitations/{token}/accept|decline`; the new endpoint only returns each invite's token, scoped server-side to the caller.
- Nav highlighting needs no code beyond the routing flip — `isActiveDestination` already matches `/my-team`.
- Zoneless Angular: no `fakeAsync` in specs.
- Commit after each task or logical group; report DESIGN.md drift rather than resolving silently.
