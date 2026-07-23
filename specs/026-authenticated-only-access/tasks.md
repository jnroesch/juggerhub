---
description: "Task list for feature 026 — Authenticated-Only Access with Opt-In Public Profiles"
---

# Tasks: Authenticated-Only Access with Opt-In Public Profiles

**Input**: Design documents from `specs/026-authenticated-only-access/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/access-and-visibility.md, quickstart.md

**Tests**: INCLUDED — the spec's Success Criteria (SC-002 "verified independently of the UI", SC-004 no-oracle) and the constitution's quality gates require server-side verification. Existing anonymous-access tests must also be flipped.

**Organization**: Tasks grouped by user story (US1 → US2 → US3) for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 / US2 / US3
- Paths are repo-relative.

## Path Conventions

- Backend: `backend/` (ASP.NET Core, xUnit tests under `backend/tests/JuggerHub.Api.IntegrationTests/`)
- Frontend: `frontend/apps/web/src/app/`, e2e under `frontend/apps/web-e2e/src/`

---

## Phase 1: Setup

**Purpose**: Prepare review scaffolding and a clean baseline before touching access control.

- [x] T001 Instantiate the UI review checklist by copying `.specify/templates/ui-review-checklist-template.md` to `specs/026-authenticated-only-access/checklists/ui-review.md` (for the settings toggle + anonymous not-found/sign-in states).
- [x] T002 Establish a green baseline: run `dotnet test` (backend) and `npx nx test web` (frontend) and note current pass counts, so post-change regressions/flips are attributable.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Secure-by-default authorization mechanism that both US1 and US3 rely on.

**⚠️ CRITICAL**: Complete before US1/US3. Applying the fallback is inert until US1 removes the `[AllowAnonymous]` attributes (team/event/browse reads keep their attribute until then), so this phase does not by itself change behavior — but the allowlist audit must land with it.

- [x] T003 Add a global `FallbackPolicy` in `backend/Program.cs` (`AddAuthorization`): `new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser().Build()`. Do not disturb the existing `PlatformAdmin` policy.
- [x] T004 Audit every controller/action for the intended-anonymous allowlist (Auth, Health, RecognitionIcons, invite previews in `InvitationsController`/`EventInvitationsController`/`PartyInvitationsController`/`MarketController`, and the profile `{handle}*` reads). Add an explicit `[AllowAnonymous]` to any intended-anonymous endpoint that currently relies on the absence of `[Authorize]` (so the new fallback does not 401 it). Record the final allowlist in a comment near the fallback registration.

**Checkpoint**: Default-deny is in place; intended-anonymous endpoints explicitly allowlisted.

---

## Phase 3: User Story 1 — Data is behind sign-in (Priority: P1) 🎯 MVP

**Goal**: Teams, events, and all search/browse require authentication; anonymous callers are refused server-side and redirected in the UI. Authenticated users retain full access.

**Independent Test**: Signed out, every team page / event page / browse view (direct URL + in-app link) lands on sign-in and every corresponding API read returns 401; signed in, all load normally.

### Tests for User Story 1 (write first; expect them to fail pre-implementation)

- [x] T005 [P] [US1] Integration test — anonymous `GET /api/v1/teams` and `GET /api/v1/teams/{slug}/public` return 401, in `backend/tests/JuggerHub.Api.IntegrationTests/Teams/AnonymousAccessTests.cs`.
- [x] T006 [P] [US1] Integration test — anonymous `GET /api/v1/events`, `/{id}`, `/{id}/participants`, `/{id}/news`, `/{id}/contacts` return 401, in `backend/tests/JuggerHub.Api.IntegrationTests/Events/AnonymousAccessTests.cs`.
- [x] T007 [P] [US1] Integration test — anonymous `GET /api/v1/profiles` (browse) returns 401, in `backend/tests/JuggerHub.Api.IntegrationTests/Profile/AnonymousBrowseTests.cs`.
- [x] T008 [P] [US1] Integration test — authenticated caller still gets 200 for the same team/event/browse reads (regression guard for SC-003), in the files above or a shared `AuthenticatedAccessTests.cs`.

### Implementation for User Story 1

- [x] T009 [US1] In `backend/Controllers/TeamsController.cs`, remove `[AllowAnonymous]` from `Browse` (line ~77) and `GetPublic` (`{slug}/public`, line ~122); update the controller XML doc that says "Only `{slug}/public` is anonymous" to reflect auth-required.
- [x] T010 [US1] In `backend/Controllers/EventsController.cs`, remove `[AllowAnonymous]` from `Browse`, `GetDetail`, `GetParticipants`, `GetNews`, `GetContacts` (lines ~77/103/111/215/243); update the "Public reads (anonymous…)" section comments.
- [x] T011 [US1] In `backend/Controllers/ProfilesController.cs`, add class-level `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`, remove `[AllowAnonymous]` from `Browse` (keep it on the three `{handle}` reads), and update the class XML doc ("Public routes … anonymous by design") to describe the new visibility-gated model.
- [x] T012 [US1] Flip existing integration tests that assert anonymous success to expect 401 (anonymous) / 200 (authenticated): audit `Events/EventTests.cs`, `Home/HomeTests.cs`, `Teams/*`, `Profile/ProfileTests.cs`, and any search/browse tests. Keep authenticated paths green.

### Frontend for User Story 1

- [x] T013 [P] [US1] In `frontend/apps/web/src/app/app.routes.ts`, add `canActivate: [authGuard]` to `t/:slug`, `events/:id`, `browse/teams`, `browse/events`, `browse/players`; update the inline comments ("anonymous-viewable", "the event page itself is public", "Browse … anonymous").
- [x] T014 [P] [US1] Add/adjust guard coverage: a route/guard spec asserting the five routes redirect when unauthenticated, and update `frontend/apps/web-e2e/src/` (e.g. a `signed-out-redirect.spec.ts`) to assert signed-out access to a team/event/browse route lands on sign-in.

**Checkpoint**: US1 fully functional — the app is authenticated-only for teams/events/browse. (Profile-by-handle remains anonymous until US2; that is the intended partial state.)

---

## Phase 4: User Story 2 — Opt in to a public profile (Priority: P2)

**Goal**: A per-profile `IsPublic` flag (default private) lets an owner expose their profile card + team memberships + activity to anonymous visitors via the direct `/u/{handle}` link; private profiles are indistinguishable from missing ones; authenticated users can view any profile.

**Independent Test**: Toggle a profile public → anonymous `/u/{handle}` returns the profile; toggle private → anonymous returns the same 404 as a missing handle; a signed-in viewer sees it either way.

### Data & migration

- [x] T015 [US2] Add `public bool IsPublic { get; set; } = false;` to `backend/Entities/PlayerProfile.cs` with an XML doc noting owner-controlled anonymous visibility.
- [x] T016 [US2] Configure the column in `backend/Data/AppDbContext.cs` (PlayerProfile config): non-null with default `false`. Leave the banned-owner global query filter unchanged.
- [x] T017 [US2] Generate the EF migration `AddProfileVisibility` (`dotnet ef migrations add AddProfileVisibility`) — adds `IsPublic boolean NOT NULL DEFAULT false`, backfilling existing rows to false (FR-017); verify the `Down` drops the column.

### DTOs & service

- [x] T018 [P] [US2] In `backend/Dtos/Profile/ProfileDtos.cs`, add `bool IsPublic` to `OwnerProfileDto` and to `UpdateProfileRequest`. Leave `PublicProfileDto` unchanged (must not carry the flag — FR-013).
- [x] T019 [US2] In `backend/Services/Profile/IProfileService.cs`, add a `Guid? viewerUserId` parameter to `GetPublicAsync`, `GetProfileIdAsync`, and `GetAvatarAsync`.
- [x] T020 [US2] In `ProfileService` (impl of `IProfileService`), implement the visibility gate in those three methods (return result only when `IsPublic || viewerUserId is not null`, else `null` — mapping to the existing not-found branch, no oracle); persist `IsPublic` in `UpdateAsync`; include it in `GetOwnerAsync`.
- [x] T021 [US2] In `backend/Controllers/ProfilesController.cs`, add a `GetOptionalUserId()` helper (mirroring `TeamsController`) and pass its result into `GetPublic`, `GetAvatar`, and `GetActivity`.

### Tests for User Story 2

- [x] T022 [P] [US2] Integration test — visibility matrix in `backend/tests/JuggerHub.Api.IntegrationTests/Profile/VisibilityTests.cs`: private→404 (anonymous), public→200 (anonymous), authed→200 (either), and a **no-oracle** assertion that private-anonymous and missing-handle responses are identical (status/title/detail); plus banned-owner→404 with `IsPublic=true`.
- [x] T023 [P] [US2] Integration test — owner round-trip: `PUT /me` with `isPublic:true` then `GET /me` reflects it; toggling false flips anonymous access, in `Profile/OwnerVisibilityTests.cs`.

### Frontend for User Story 2

- [x] T024 [US2] Carry `isPublic` on the owner profile model + update payload in `frontend/apps/web/src/app/core/services/profile.service.ts` (and the owner model type).
- [x] T025 [US2] Add a "Make my profile public" toggle (separate `.html`/`.css`/`.ts`) to the owner profile/account settings component (per DESIGN.md; location resolved in T027), bound to `isPublic` and saving through the existing owner update.
- [x] T026 [P] [US2] Component spec for the toggle + e2e `public-profile-optin.spec.ts` in `frontend/apps/web-e2e/src/` covering the opt-in round-trip and the team-link-still-requires-auth step.
- [x] T027 [US2] Run the UI review checklist (`checklists/ui-review.md`) against the toggle and the anonymous not-found/sign-in states; DESIGN.md wins on any conflict (also settles whether the toggle lives on the profile-owner or account page).

**Checkpoint**: US1 + US2 both work — everything is auth-only except owner-opted public profiles reachable by direct link.

---

## Phase 5: User Story 3 — Public profiles are not anonymously discoverable (Priority: P3)

**Goal**: Prove there is no anonymous enumeration/search surface; a public profile is reachable only by direct link.

**Independent Test**: Signed out, no players/teams/events browse or search is reachable; a public profile opens only via its `/u/{handle}` link.

- [x] T028 [US3] Integration test — route-enumeration allowlist in `backend/tests/JuggerHub.Api.IntegrationTests/Security/AnonymousAllowlistTests.cs`: assert every anonymously-reachable endpoint is exactly the intended allowlist (auth, health, recognition icons, invite previews, `{handle}*`), and that no players/teams/events browse is anonymous.
- [x] T029 [US3] Verify no anonymous discovery entry points remain in the UI (nav/links to browse for signed-out users) and add an e2e assertion that a signed-out session exposes no browse/search surface.

**Checkpoint**: All three stories independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Reconcile documented drift and run full verification.

- [x] T030 [P] Annotate feature specs `006-events`, `007-search`, and `009-team-public-page` as superseded by 026 (add a note at the top of each `spec.md` pointing to `specs/026-authenticated-only-access/`); these previously documented the now-reversed "anonymous by design" invariants.
- [x] T031 [P] Update lingering "anonymous by design / shown on public pages" code comments (e.g. `RecognitionIconsController` rationale — icons stay anonymous, but the "public profile and team pages" wording should reflect auth-gating) so comments match behavior.
- [x] T032 Run full verification: `dotnet test`, `npx nx test web`, and the Playwright e2e suite; all green (with the flipped assertions from T012 now asserting the new behavior).
- [x] T033 Execute `specs/026-authenticated-only-access/quickstart.md` end-to-end. **Covered by the automated suite** rather than a manual docker-compose run: §1 anonymous-denied → `Security/AnonymousAccessTests`; §2 authenticated-access → `AnonymousAccessTests.Authenticated_reads…`; §3 opt-in round-trip → `Profile/OwnerVisibilityTests` + `web-e2e/profile.spec.ts`; §4 no-oracle → `Profile/VisibilityTests`; §5 default-private → `ProfileTests.Register_creates_a_private_by_default_profile`; §6 banned-owner → `VisibilityTests`; §7 no-discovery → `Security/AnonymousAllowlistTests` + `web-e2e/authenticated-only.spec.ts`. The **live docker-compose walkthrough is the one step not run here** (needs the stack up); the Playwright specs encode those browser flows for CI.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: after Setup; blocks US1 and US3. (Inert until US1's attribute removals.)
- **US1 (Phase 3)**: after Foundational. The MVP.
- **US2 (Phase 4)**: after Foundational; independent of US1 (touches profile `{handle}` reads + owner settings, not team/event routes). Can proceed in parallel with US1 if staffed.
- **US3 (Phase 5)**: after US1 (its allowlist test asserts the post-US1 posture) and after US2 (allowlist includes the gated `{handle}` reads).
- **Polish (Phase 6)**: after the user stories being shipped.

### Within Each User Story

- Write the story's tests first (they should fail before implementation).
- Backend: entity → DbContext/migration → DTO → service → controller.
- Frontend after (or alongside) its backend contract exists.

### Parallel Opportunities

- T005–T008 (US1 tests) run in parallel.
- T013/T014 (US1 frontend) run parallel to the US1 backend edits.
- T018 parallel to T015–T017; T022/T023 parallel once the service/controller land.
- T030/T031 (doc drift) run any time in parallel.
- US1 and US2 can be developed by different people in parallel after Foundational.

---

## Parallel Example: User Story 1 tests

```bash
Task: "Anonymous 401 for team reads — Teams/AnonymousAccessTests.cs"
Task: "Anonymous 401 for event reads — Events/AnonymousAccessTests.cs"
Task: "Anonymous 401 for profiles browse — Profile/AnonymousBrowseTests.cs"
Task: "Authenticated 200 regression — AuthenticatedAccessTests.cs"
```

---

## Implementation Strategy

### MVP First (US1)

1. Phase 1 Setup → 2. Phase 2 Foundational (fallback + allowlist) → 3. Phase 3 US1.
4. **STOP and VALIDATE**: signed-out is fully locked out of teams/events/browse; signed-in unchanged.
5. Deploy/demo — the core privacy outcome is delivered.

### Incremental Delivery

- US1 → auth-only app (MVP).
- US2 → owner-opt-in public profiles by direct link.
- US3 → verified no anonymous discovery surface.
- Polish → drift reconciled + full verification.

---

## Notes

- [P] = different files, no incomplete-task dependency.
- Biggest hidden cost is T012 (flipping existing anonymous-success tests) — budget for it.
- The migration (T017) must be applied before running quickstart (T033).
- No constitution amendment; drift is reconciled in specs/comments (T030/T031).
