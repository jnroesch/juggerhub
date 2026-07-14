---
description: "Task list for Event Parties (feature 016)"
---

# Tasks: Event Parties

**Input**: Design documents in `specs/016-event-parties/` (plan.md, spec.md, research.md,
data-model.md, contracts/party-api.md, quickstart.md)

**Tests**: Included. The spec's per-story Independent Tests, the constitution's quality gates, and
research R9 call for a backend integration suite (mirroring events/teams) plus zoneless frontend
specs. Backend tests are written per story; write them to fail first, then implement.

**Organization**: Grouped by the 9 user stories from spec.md so each is an independently testable
increment. Backend = `backend/`, frontend = `frontend/apps/web/src/app/`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: May run in parallel (different files, no dependency on an incomplete task).
- **[Story]**: US1–US9; Setup/Foundational/Polish carry no story label.

---

## Phase 1: Setup

**Purpose**: Baseline before any code changes.

- [X] T001 Confirm branch `016-event-parties` is checked out and the backend + frontend build clean (`dotnet build backend/JuggerHub.Api.sln`; `npx nx build web` from `frontend/`) to establish a green baseline.
- [X] T002 Instantiate the UI review checklist by copying `.specify/templates/ui-review-checklist-template.md` to `specs/016-event-parties/checklists/ui-review.md` (verified during UI stories and Polish).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The schema, roster-cap on the event, enums, shared guards/helpers, DI, and client
scaffolding every story needs.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Backend — entities & enums

- [X] T003 [P] Create `backend/Entities/PartyEnums.cs` with `PartyStatus { Open, Applied }`, `PartyMemberStatus { In, Declined }`, `PartyMemberRole { Member, Admin }` (name-serialized, XML docs per repo style).
- [X] T004 [P] Create `backend/Entities/Party.cs` (`BaseEntity`): `TeamId`, `EventId`, `RosterCap`, `Message?`, `Status`, `EventSignupId?`, `CreatedByUserId`, navigations `Team`/`Event`/`EventSignup?`/`Members`/`News`/`Invitations` per data-model.md.
- [X] T005 [P] Create `backend/Entities/PartyMember.cs` (`BaseEntity`): `PartyId`, `UserId`, `Status`, `Role`, navigations `Party`/`User` (arrival order = `CreatedDate`).
- [X] T006 [P] Create `backend/Entities/PartyNewsPost.cs` (`BaseEntity`): `PartyId`, `AuthorUserId`, `Body`, navigations `Party`/`Author`.
- [X] T007 [P] Create `backend/Entities/PartyAdminInvitation.cs` (`BaseEntity`) mirroring `EventAdminInvitation`: `PartyId`, `Kind`, `Token`, `Status`, `ExpiresDate`, `CreatedByUserId`, `TargetUserId?`, navigations.
- [X] T008 Add `int? RosterCap` to `backend/Entities/Event.cs` (players-per-team; teams-only; XML doc noting default 8 / min 5 / null for individuals).
- [X] T009 [P] Extend `backend/Entities/NotificationEnums.cs`: add `NotificationType.PartyRequest` and `NotificationType.PartyNews`, and map them in `NotificationCategories.For(...)` to `InvitesAndRoster` and `TeamNews` respectively.

### Backend — roster cap in event creation (extends feature 006)

- [X] T010 Add `RosterCap` to the event-create request DTO + `EventService` create/validation in `backend/` (feature 006): required when `ParticipantMode = Teams` (default 8 if omitted, **minimum 5**, sane upper guard), refused (400) when supplied for an individuals-only event; persist onto `Event.RosterCap`. Update the event detail DTO to expose it.
- [X] T011 Edit the event-create wizard "who can join" step (`frontend/apps/web/src/app/features/events/event-create/event-create.component.{ts,html,css}`) to capture `rosterCap` for teams-only events (default 8, min 5, live client validation mirroring the server) and include it in the create payload.

### Backend — persistence

- [X] T012 Register `DbSet`s (`Parties`, `PartyMembers`, `PartyNewsPosts`, `PartyAdminInvitations`) and add `OnModelCreating` config in `backend/Data/AppDbContext.cs`: property lengths (`Message` 500, news `Body` 1000, `Token` 64), FKs + cascade rules, and all indexes/constraints from data-model.md (partial-unique `Parties(TeamId,EventId)`; unique partial `Parties(EventSignupId)`; unique `PartyMembers(PartyId,UserId)` + index `(PartyId,Status)`; news `(PartyId,CreatedDate)`; invitation indexes identical in shape to `EventAdminInvitation`).
- [X] T013 Generate the EF migration `AddParties` (`dotnet ef migrations add AddParties` in `backend/`; copy `Directory.Build.props` + `.editorconfig` before `dotnet restore` if tooling context is fresh, per branch-027 fix); verify it adds `Events.RosterCap` + the four tables and applies cleanly (`dotnet ef database update`).

### Backend — shared guard/helper/DI

- [X] T014 [P] Create `backend/Services/Parties/PartyAccess.cs` — resolve `(PartyExists, TeamId, EventId, Status, MyRole, IsTeamMember)` for a `(partyId, userId)` in one projected query (mirrors `EventAdminGuard`/`TeamMembershipGuard`); expose helpers `IsPartyAdmin`, `IsCrew`, `IsTeamMember`.
- [X] T015 [P] Create `backend/Services/Parties/PartyCapacity.cs` — `InCountAsync(partyId)` and `LockPartyRowAsync(partyId)` (`SELECT 1 FROM "Parties" WHERE "Id" = {id} FOR UPDATE`), mirroring `EventCapacity`.
- [X] T016 Create the service interfaces (empty method stubs to be filled per story): `IPartyService`, `IPartyRosterService`, `IPartyNewsService`, `IPartyInvitationService` in `backend/Services/Parties/`.
- [X] T017 Register the new guard, capacity helper, four services, and `PartyEmailService` in DI in `backend/Program.cs` (scoped, matching the events registrations).

### Backend — DTOs & email scaffolding

- [X] T018 [P] Create `backend/Dtos/Parties/PartyDtos.cs` with the response records referenced by contracts/party-api.md (`PartyDto`, `PartyMemberDto`, `PartyNewsDto`, `PartyRequestCardDto`, `PartyContextDto`, `PartyInvitationDto`, `PartyInvitableUserDto`, `PartyInvitePreviewDto`, request bodies) and a Mapster mapping registration if the events slice uses one.
- [X] T019 [P] Create `backend/Services/Email/PartyEmailService.cs` (skeleton) following `EventEmailService`: constructor deps, `BuildInviteLink`, and method signatures `SendPartyRequestEmailAsync`, `SendPartyNewsEmailAsync`, `SendCoAdminInviteEmailAsync` (bodies finalized in their stories).

### Frontend — scaffolding

- [X] T020 [P] Create `frontend/apps/web/src/app/core/models/party.models.ts` (Party, PartyMember, roster group, request card, context, invitation types) matching the DTOs.
- [X] T021 [P] Create `frontend/apps/web/src/app/core/services/party.service.ts` — a signal/HttpClient service with method stubs for every endpoint in contracts/party-api.md (filled per story).
- [X] T022 Add party routes to `frontend/apps/web/src/app/app.routes.ts` (`/t/:slug/party/:eventId` → party-manage; party-invite-accept route `/party-invite/:token`), lazy-loaded, behind the auth guard.

**Checkpoint**: Schema migrated, roster cap live in event creation, guards/DI/DTOs/client
scaffolding in place — stories can begin.

---

## Phase 3: User Story 1 — Form a party from a teams-only event (Priority: P1) 🎯 MVP

**Goal**: A team admin sees "Enter a party" on a teams-only event and can form a party (creator
becomes party admin; team not yet on the event; one party per team+event).

**Independent Test**: As a team admin, form a party from a seeded teams-only event; confirm creator
is `In+Admin`, cap copied, no event entry, and a second party for the same team+event is refused.

### Tests for User Story 1

- [X] T023 [P] [US1] Integration test `backend/tests/JuggerHub.Api.IntegrationTests/Parties/FormPartyTests.cs`: team-admin-only forming, one-per-team+event (409), teams-only + non-cancelled/not-ended guard, creator seeded `In+Admin`, cap snapshot, no `EventSignup` created.
- [X] T024 [P] [US1] Integration test `.../Parties/PartyContextTests.cs`: `GET /events/{id}/party-context` returns administered teams with `partyId`/`canForm`/`myState`; empty for non-admins; individuals-only shape.

### Implementation for User Story 1

- [X] T025 [US1] Implement `PartyService.FormAsync(eventId, teamId, message, actorUserId)` in `backend/Services/Parties/PartyService.cs`: validate teams-only + open event, team-admin via `TeamMembershipGuard`, one-per-team+event (pre-check + catch unique violation), snapshot `RosterCap`, insert `Party` + creator `PartyMember(In,Admin)`; return `PartyDto`. (Notification fan-out is US2 — leave a seam/call point.)
- [X] T026 [US1] Implement party-context read in `EventService`/`PartyService`: for a teams-only event + caller, return administered teams and existing-party state (`PartyContextDto`).
- [X] T027 [US1] Create `backend/Controllers/PartiesController.cs` with `POST /parties` (form) delegating to `PartyService`; thin controller, DTO out, problem responses for 400/409.
- [X] T028 [US1] Add `GET /events/{id}/party-context` to `backend/Controllers/EventsController.cs` (thin; delegates to the service from T026).
- [X] T029 [P] [US1] Implement `party.service.ts` methods `getPartyContext(eventId)` and `formParty(...)`.
- [X] T030 [US1] Edit `frontend/.../events/event-detail/components/join-actions.component.{ts,html,css}`: for teams-only events, replace the direct team-join with "Enter a party" (opens form) / "Manage party" (when a party exists), driven by party-context; individuals-only path unchanged. Follow DESIGN.md.
- [X] T031 [US1] Create `frontend/.../features/parties/party-create/party-create.component.{ts,html,css}`: team picker (auto-select single), read-only roster-cap display, message field, submit → navigate to Manage; empty/loading/error states.

**Checkpoint**: Teams-only events show "Enter a party"; forming works and is guarded.

---

## Phase 4: User Story 2 — The participation request reaches the team (Priority: P1)

**Goal**: Forming a party pins a request card in the team space and sends a notification + email to
every team member; non-members see nothing.

**Independent Test**: Form a party; confirm every team member gets the pinned card + notification +
email; a non-member gets none.

### Tests for User Story 2

- [X] T032 [P] [US2] Integration test `.../Parties/PartyRequestFanoutTests.cs`: forming fans out `PartyRequest` notifications + emails to all team members except the creator, respects preferences, excludes non-members; dedupe by `party-request:{partyId}`.
- [X] T033 [P] [US2] Integration test `.../Parties/TeamPartyRequestsTests.cs`: `GET /teams/{slug}/party-requests` returns member-visible active parties with `inCount/rosterCap/message/myState`; 404 for non-members.

### Implementation for User Story 2

- [X] T034 [US2] Wire notification + email fan-out into `PartyService.FormAsync`: gather team member ids (minus creator) and call `INotificationService.CreateManyAsync(PartyRequest, payload, actor, "party-request:{partyId}")`; send `PartyEmailService.SendPartyRequestEmailAsync` to each.
- [X] T035 [P] [US2] Create `backend/EmailTemplates/party-request.html` extending the base header/footer (event name, team, dates, fill, message, "See the request" CTA), inline CSS per constitution.
- [X] T036 [P] [US2] Implement `PartyEmailService.SendPartyRequestEmailAsync` (render template + `IEmailSender`), mirroring `EventEmailService`.
- [X] T037 [US2] Implement team party-requests read: `PartyService.ListTeamRequestsAsync(slug, actorUserId, pagination)` (member-gated via `TeamMembershipGuard`) → `PagedResult<PartyRequestCardDto>`; add `GET /teams/{slug}/party-requests` to `backend/Controllers/TeamsController.cs`.
- [X] T038 [P] [US2] Implement `party.service.ts` `getTeamPartyRequests(slug)`.
- [X] T039 [US2] Edit `frontend/.../features/teams/team-detail/team-detail.component.{ts,html,css}`: render pinned party-request card(s) at the top (event, `In/cap` fill, message) with I'm-in / Can't-make-it affordances (actions wired in US3); admin's card links to Manage. DESIGN.md styling.
- [X] T040 [US2] Edit `frontend/.../features/alerts/*`: render the `PartyRequest` notification type (icon, text, navigation target to the pinned card / party).

**Checkpoint**: Forming a party notifies the team via card + notification + email.

---

## Phase 5: User Story 3 — A team member answers the request (Priority: P1)

**Goal**: Members accept/decline/leave; decline is reversible and visible to the admin; capacity is
respected.

**Independent Test**: Accept → added + count rises; decline from another account → recorded + still
visible + can rejoin; leave from an In member → spot freed.

### Tests for User Story 3

- [X] T041 [P] [US3] Integration test `.../Parties/AnswerRequestTests.cs`: join adds `In`, decline records `Declined` (visible to admin), rejoin from declined, leave frees spot, non-team-member refused (403/404), disbanded party refused.
- [X] T042 [P] [US3] Integration test `.../Parties/RosterGroupsTests.cs`: `GET /parties/{id}/members?group=` returns In/Declined and derived NoResponse (current team members minus rows); paginated; member-gated.

### Implementation for User Story 3

- [X] T043 [US3] Implement `PartyRosterService.JoinAsync/DeclineAsync/LeaveAsync` in `backend/Services/Parties/PartyRosterService.cs`: team-membership check via `PartyAccess`; Join takes `PartyCapacity.LockPartyRowAsync` + In-count vs `RosterCap` (409 `full`), upsert row (Declined→In); Decline upserts Declined; Leave deletes own In row (last-admin guarded); event not cancelled/ended.
- [X] T044 [US3] Implement `PartyRosterService.ListGroupAsync(partyId, group, pagination, actorUserId)`: In/Declined from rows joined to current `TeamMembership`; NoResponse = team members with no row; project `PartyMemberDto`.
- [X] T045 [US3] Add roster endpoints to `PartiesController`: `GET /parties/{id}/members`, `POST /parties/{id}/join`, `POST /parties/{id}/decline`, `POST /parties/{id}/leave`, plus `GET /parties/{id}` (detail with roster summary + readiness).
- [X] T046 [P] [US3] Implement `party.service.ts` `getParty`, `listMembers`, `join`, `decline`, `leave`.
- [X] T047 [US3] Wire the team-detail pinned card actions (US2 T039) to join/decline (and change-of-mind), reflecting fill and the caller's state; optimistic UI with server as source of truth.

**Checkpoint**: Members can answer; the crew fills; declines are reversible.

---

## Phase 6: User Story 4 — Manage the party roster and tools (Priority: P2)

**Goal**: The party admin hub: three roster groups, readiness, nudge, remove, and the tools list
(news/co-admins/apply/disband + placeholder payment & travel).

**Independent Test**: Groups + counts correct; nudge re-sends; remove drops from party but keeps team
membership; non-admin cannot nudge/remove.

### Tests for User Story 4

- [X] T048 [P] [US4] Integration test `.../Parties/ManageRosterTests.cs`: nudge (admin-only, re-sends with fresh dedupe, only to no-response), remove (admin-only, frees spot, team membership + badges untouched, last-admin guard), readiness counts.

### Implementation for User Story 4

- [X] T049 [US4] Implement `PartyRosterService.NudgeAsync(partyId, targetUserId, actor)` (party-admin; target must be no-response; re-send notification + email with dedupe `party-nudge:{partyId}:{round}`) and `RemoveAsync(partyId, targetUserId, actor)` (party-admin; delete row; last-admin guard).
- [X] T050 [US4] Add readiness computation to the `GET /parties/{id}` detail projection (enough-to-field / spots-open / unanswered counts).
- [X] T051 [US4] Add `DELETE /parties/{id}/members/{userId}` and `POST /parties/{id}/members/{userId}/nudge` to `PartiesController`.
- [X] T052 [P] [US4] Implement `party.service.ts` `nudge`, `removeMember`.
- [X] T053 [US4] Create `frontend/.../features/parties/party-manage/party-manage.component.{ts,html,css}`: dense desktop roster + readiness + tools; mobile tabbed groups (In/Declined/No reply); nudge/remove actions; payment-split & travel/carpool rendered as clearly-labelled non-functional "LATER" placeholders; apply/disband entry points (wired in US5/US9). DESIGN.md; empty/loading/error states.

**Checkpoint**: Full admin management hub over the roster.

---

## Phase 7: User Story 5 — Apply the party to the event (Priority: P2)

**Goal**: A deliberate apply lists the team on the event (reusing the feature-006 flow); withdraw
removes the entry but keeps the party. Direct team-join is removed.

**Independent Test**: Apply to a free event → single Joined entry + card "applied"; paid → awaiting
approval; full → event waiting list; withdraw → team leaves, party stays.

### Tests for User Story 5

- [X] T054 [P] [US5] Integration test `.../Parties/ApplyPartyTests.cs`: free→Joined, paid→AwaitingApproval, full→Waitlisted routing; party-admin-only; duplicate team entry refused; cancelled/ended refused; `Party.Status`/`EventSignupId` set.
- [X] T055 [P] [US5] Integration test `.../Parties/WithdrawAndDirectJoinRemovedTests.cs`: withdraw deletes the `EventSignup` and reverts to Open (party kept); and `POST /events/{id}/signup` for a team subject now returns 400 `useParty`.

### Implementation for User Story 5

- [X] T056 [US5] Implement `PartyService.ApplyAsync(partyId, actor)`: party-admin check; `EventCapacity.LockEventRowAsync` + occupied count → decide Joined/AwaitingApproval/Waitlisted; insert `EventSignup{EventId,TeamId,Status}`; set `Party.EventSignupId` + `Status=Applied`; duplicate-team guard (unique index) → 409; cancelled/ended → 400.
- [X] T057 [US5] Implement `PartyService.WithdrawAsync(partyId, actor)`: party-admin; delete the linked `EventSignup`; `Status=Open`; clear `EventSignupId`.
- [X] T058 [US5] Remove the teams-only branch from `backend/Services/Events/EventSignupService.cs` `SignupAsync` and return a `useParty` refusal for team subjects; keep the individuals path; adjust/relocate any now-unused team-admin sign-up logic.
- [X] T059 [US5] Add `POST /parties/{id}/apply` and `POST /parties/{id}/withdraw` to `PartiesController`.
- [X] T060 [P] [US5] Implement `party.service.ts` `apply`, `withdraw`.
- [X] T061 [US5] Add the pre-apply readiness screen + Apply/Withdraw actions to `party-manage`, and reflect the "applied" state on the team-detail pinned card and event-detail (team shown as the event entry).

**Checkpoint**: Two-phase entry works end-to-end; direct team-join is gone.

---

## Phase 8: User Story 6 — Auto-close and reopen at the roster cap (Priority: P2)

**Goal**: Joining auto-closes at the cap and reopens on a drop, first-come, with no waitlist and no
admin action; the cap is atomic under concurrency.

**Independent Test**: Fill to cap → card full/no join; a leave reopens it; two racing last-spot joins
never exceed the cap.

### Tests for User Story 6

- [X] T062 [P] [US6] Concurrency integration test `.../Parties/CapConcurrencyTests.cs`: parallel joins on the final spot admit exactly one (the other gets 409 `full`); a leave below the cap lets the next join succeed (first-come).

### Implementation for User Story 6

- [X] T063 [US6] Surface the derived open/closed state in `PartyDto`/request card (`isFull`, spots-open) computed from In-count vs `RosterCap` (no stored flag); ensure the join path from US3 already enforces atomicity via `PartyCapacity` (add the guard if any gap surfaced by T062).
- [X] T064 [US6] Reflect full → "no I'm in" and auto-reopen → "I'm in" states on the team-detail pinned card and party-manage; optional "notify me when a spot opens" affordance (display-only) per the wireframe.

**Checkpoint**: The cap governs joining automatically and safely.

---

## Phase 9: User Story 7 — Post party news (Priority: P3)

**Goal**: Private party news (crew-only) with admin compose; posting notifies the crew (in-app +
email); deleted on disband.

**Independent Test**: Post news → crew sees it newest-first + gets notified; a team member not in the
party is refused; non-admin has no composer.

### Tests for User Story 7

- [X] T065 [P] [US7] Integration test `.../Parties/PartyNewsTests.cs`: admin-only compose (403 otherwise), crew-only read (decliners/non-members 404), newest-first pagination, and fan-out of `PartyNews` notifications + emails to In members except the author.

### Implementation for User Story 7

- [X] T066 [US7] Implement `PartyNewsService.ListAsync/CreateAsync` in `backend/Services/Parties/PartyNewsService.cs`: read crew-gated via `PartyAccess.IsCrew`; create party-admin-only; on create, fan out `NotificationType.PartyNews` (dedupe `party-news:{postId}`) + `PartyEmailService.SendPartyNewsEmailAsync` to In members minus author.
- [X] T067 [P] [US7] Create `backend/EmailTemplates/party-news.html` extending base templates (author, party = team @ event, body, CTA).
- [X] T068 [US7] Implement `PartyEmailService.SendPartyNewsEmailAsync`.
- [X] T069 [US7] Add `GET /parties/{id}/news` and `POST /parties/{id}/news` to `PartiesController`.
- [X] T070 [P] [US7] Implement `party.service.ts` `listNews`, `postNews`.
- [X] T071 [US7] Create `frontend/.../features/parties/party-news/party-news.component.{ts,html,css}`: feed + admin composer, newest-first, empty state; reachable from party-manage.

**Checkpoint**: Private party news with crew notifications.

---

## Phase 10: User Story 8 — Invite party co-admins (Priority: P3)

**Goal**: Team-scoped co-admin invites (link + targeted + member search), accept grants party-admin
powers; last-admin guard.

**Independent Test**: Copy link + invite a team member; accept grants powers; non-team-member refused;
last admin cannot step down.

### Tests for User Story 8

- [X] T072 [P] [US8] Integration test `.../Parties/PartyCoAdminTests.cs`: link create/rotate/revoke, targeted invite (team-member-only, already-admin/already-invited guards), member-search scoping, accept grants Admin (cap-checked; 409 if full), decline, last-admin guard.

### Implementation for User Story 8

- [X] T073 [US8] Implement `PartyInvitationService` in `backend/Services/Parties/PartyInvitationService.cs` mirroring `EventInvitationService`: `GetActiveLink`, `CreateOrRotateLink`, `Revoke`, `CreateTargeted` (target must be a current team member, not already admin), `ListPending`, `SearchMembers` (scoped to the party's `TeamMembership`), `GetPreview`, `Accept` (grant `Admin` role on the invitee's `PartyMember`, join In if room else 409), `Decline`; reuse `EventOptions.InviteLinkTtlDays`.
- [X] T074 [US8] Add co-admin endpoints to `PartiesController` (`.../invitations/link` GET/POST, `.../invitations` GET/POST, `.../invitations/{invitationId}` DELETE, `.../invitations/member-search`).
- [X] T075 [US8] Create `backend/Controllers/PartyInvitationsController.cs` for the token flow (`GET /party-invitations/{token}`, `POST .../accept`, `POST .../decline`).
- [X] T076 [P] [US8] Create `backend/EmailTemplates/party-coadmin-invite.html` and implement `PartyEmailService.SendCoAdminInviteEmailAsync`.
- [X] T077 [P] [US8] Implement the co-admin methods in `party.service.ts`.
- [X] T078 [US8] Create `frontend/.../features/parties/party-invitations/party-invitations.component.{ts,html,css}` (link + member search + pending list) and `party-invite-accept/party-invite-accept.component.{ts,html,css}` (preview + accept/decline); reachable from party-manage and the `/party-invite/:token` route.

**Checkpoint**: Shared party administration, team-scoped.

---

## Phase 11: User Story 9 — Disband the party (Priority: P3)

**Goal**: Guarded, irreversible disband: hard-delete party + news + invites, unpin the request, and
withdraw the event entry if applied; team/roster/badges untouched.

**Independent Test**: Disband an applied party → party + news gone, request unpinned, team withdrawn
from event, team/roster/badges unchanged; non-admin cannot disband.

### Tests for User Story 9

- [X] T079 [P] [US9] Integration test `.../Parties/DisbandTests.cs`: party-admin-only; cascade deletes members/news/invitations; applied party also deletes the `EventSignup` (event withdrawal, no auto-promote); team membership + badges untouched; non-admin 403.

### Implementation for User Story 9

- [X] T080 [US9] Implement `PartyService.DisbandAsync(partyId, actor)`: party-admin; if `Applied`, delete the linked `EventSignup`; hard-delete the `Party` (cascade removes members/news/invitations); verify team + memberships untouched.
- [X] T081 [US9] Add `DELETE /parties/{id}` to `PartiesController`.
- [X] T082 [P] [US9] Implement `party.service.ts` `disband`.
- [X] T083 [US9] Add the danger-zone "Disband this party" section with explicit confirmation to `party-manage` (irreversible copy per the wireframe); on success navigate back to the team space.

**Checkpoint**: Full party lifecycle complete.

---

## Phase 12: Polish & Cross-Cutting Concerns

- [ ] T084 [P] Edit `backend/Data/DevDataSeeder.cs` to seed a sample teams-only event with `RosterCap` and a partly-filled party (In/Declined/No-response mix) for local testing of read-only screens.
- [ ] T085 [P] Frontend zoneless component specs (no `fakeAsync`) for `party-create`, `party-manage`, `party-news`, and the team-detail pinned card and event-detail join-actions edits.
- [X] T086 Complete `specs/016-event-parties/checklists/ui-review.md` against the diff for every new/edited UI surface (Enter/Manage party, pinned card, Manage hub desktop+mobile, news, co-admin, disband); resolve DESIGN.md conflicts (DESIGN.md wins) and fix findings. **Done**: reconciled all 5 party components + edited surfaces to DESIGN.md tokens (coral `brand` CTAs, sand text/surface ramp, status tokens, named spacing/radii, sentence case); frontend builds clean.
- [X] T087 [P] Add edge-case integration coverage not tied to one story: member leaving the team drops out of all party groups; roster-cap bounds (<5 refused) at event creation; individuals-only events never expose parties.
- [ ] T088 Run the quickstart validation (`specs/016-event-parties/quickstart.md`) end-to-end against the local stack (Scenarios A–D) and fix any gaps; confirm build/lint/test green (`dotnet test`, `npx nx test web`, `npx nx lint web`).

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (P1)** → no deps.
- **Foundational (P2)** → after Setup; **blocks all stories** (schema, roster cap in event creation, guards, DI, DTOs, client scaffolding).
- **User stories (P3–P11)** → after Foundational. Recommended order follows priority: US1→US2→US3 (P1 MVP), then US4, US5, US6 (P2), then US7, US8, US9 (P3).
- **Polish (P12)** → after the targeted stories are complete.

### Story dependencies & independence

- **US1** foundational to the rest (creates the party) — start here. The roster cap it displays/snapshots is already delivered in Foundational (T008–T011).
- **US2** depends on US1 (fan-out on form) but is independently testable (assert notifications/card).
- **US3** depends on a party existing (US1) and the request surface (US2 card) for the UI, but the join/decline/leave services + roster reads test independently.
- **US4** depends on US3 (roster exists) for meaningful management.
- **US5** depends on US1 (party) + reuses feature-006 capacity; removes direct team-join.
- **US6** hardens/exposes the cap behavior introduced in US3 — pair them if staffing allows.
- **US7/US8/US9** each depend only on a party existing (US1) + admin management (US4) and are otherwise independent of one another.

### Parallel opportunities

- Foundational entity tasks **T003–T007** are all [P] (separate files); **T009**, **T014/T015**, **T018/T019**, **T020/T021** likewise.
- Within a story, `[P]` tasks (client `party.service.ts` methods, email templates, test files) run alongside the backend service work once its endpoints' shapes are fixed.
- With multiple developers after Foundational: Dev A → US1→US2→US3; Dev B → US4→US5→US6; Dev C → US7→US8→US9.

---

## Implementation Strategy

### MVP (P1 stories)

1. Phase 1 Setup → Phase 2 Foundational (schema/roster-cap/guards/DI/scaffolding).
2. US1 (form) → US2 (request) → US3 (answer). **STOP & VALIDATE**: a team admin can form a party,
   the team is notified, and members can join/decline — the core gathering loop. Demo-able.

### Incremental delivery

3. US4 (manage) → US5 (apply, replacing direct join) → US6 (cap safety) — the full P2 slice makes
   parties enter events.
4. US7 (news) → US8 (co-admins) → US9 (disband) — the P3 tools complete the lifecycle.
5. Polish: seed data, specs, UI review, quickstart validation.

---

## Notes

- `[P]` = different files, no dependency on an incomplete task.
- Every party mutation is authorized server-side via `PartyAccess`/`TeamMembershipGuard` (never trust
  the client); all lists paginate; reads use `AsNoTracking` + projections.
- Write each story's integration tests to fail first, then implement.
- Commit after each task or logical group; keep the branch green.
- Mirror the events/teams slices for structure, error shaping (problem responses), and test style.
