---
description: "Task list for Events"
---

# Tasks: Events

**Input**: Design documents from `/specs/006-events/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/openapi.yaml, quickstart.md

**Tests**: INCLUDED — the plan's Testing section requires xUnit integration + Jest unit + Playwright e2e; the constitution's quality gates expect them.

**Organization**: Tasks are grouped by user story (US1–US8) for independent implementation and testing. Backend namespace `JuggerHub`; frontend Nx app at `frontend/apps/web/src/app`. Shared `PaginationRequest`/`PagedResult<T>`, the email pipeline (`IEmailSender`/`IEmailTemplateService`), and the 005 invitation enums (`InvitationKind`/`InvitationStatus`) already exist and are reused.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US8 (Setup/Foundational/Polish carry no story label)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Enums, options, and model skeletons used across stories.

- [x] T001 [P] Create event enums (`EventType {Tournament,Workshop,Other}`, `LocationKind {InPerson,Virtual}`, `ParticipantMode {Teams,Individuals}`, `EventStatus {Published,Cancelled}`, `SignupStatus {Joined,AwaitingApproval,Waitlisted}`) in `backend/Entities/EventEnums.cs` (reuse `InvitationKind`/`InvitationStatus` from `TeamEnums.cs`)
- [x] T002 [P] Add `EventOptions` (InviteLinkTtlDays=7, name/description/limit bounds, news/contact body caps) in `backend/Common/EventOptions.cs`; bind in `backend/Program.cs` with safe defaults
- [ ] T003 [P] Create the frontend event model skeleton (enums + DTO interfaces) in `frontend/apps/web/src/app/core/models/event.models.ts`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Schema, DTOs, guards, and service seams every story depends on.

**⚠️ CRITICAL**: No user-story work begins until this phase is complete.

- [x] T004 Extend `Event : BaseEntity` in `backend/Entities/Event.cs` — **keep** existing `Name` and `Location` (`Location` stays as the legacy free-text display used by `ActivityItemDto`); add `Type`+`CustomTypeLabel?`, `Description`, `StartsAt`/`EndsAt` (**replace** `Date`), `LocationKind` + `VenueName?`/`Street?`/`PostalCode?`/`City?`/`Country?` + `VirtualLink?`, `ParticipantMode`, `ParticipationLimit`, `IsPaid`+`FeeAmount?`/`FeeCurrency?`/`FeeRecipientName?`/`FeeIban?`/`FeePaymentDeadline?`, `Status`+`CancelledDate?`, and Signups/Admins/Invitations/Contacts/News navs
- [x] T005 [P] Create `EventSignup : BaseEntity` (EventId, UserId?, TeamId?, Status, PaymentConfirmedDate? + Event/User?/Team? navs) in `backend/Entities/EventSignup.cs`
- [x] T006 [P] Create `EventAdmin : BaseEntity` (EventId, UserId, AddedDate + navs) in `backend/Entities/EventAdmin.cs`
- [x] T007 [P] Create `EventAdminInvitation : BaseEntity` (EventId, Kind, Token, Status, ExpiresDate, CreatedByUserId, TargetUserId? + navs) in `backend/Entities/EventAdminInvitation.cs`
- [x] T008 [P] Create `EventContact : BaseEntity` (EventId, Name, Role, Phone?, Email? + nav) in `backend/Entities/EventContact.cs`
- [x] T009 [P] Create `EventNewsPost : BaseEntity` (EventId, AuthorUserId, Body + navs) in `backend/Entities/EventNewsPost.cs`
- [x] T010 Configure DbSets + model config in `backend/Data/AppDbContext.cs`: replace the `Event` block (StartsAt index; string lengths); `EventSignup` **CHECK `(UserId IS NULL) <> (TeamId IS NULL)`** + partial-unique `(EventId,UserId)` and `(EventId,TeamId)` + index `(EventId,Status)` + FK cascades to User/Team/Event; unique `EventAdmin (EventId,UserId)` + indexes `(EventId)`/`(UserId)`; unique `EventAdminInvitation.Token` + partial-unique active-link + pending-targeted + index `(EventId)`; `EventContact (EventId)` index; `EventNewsPost (EventId,CreatedDate)` index; Event→signups/admins/invitations/contacts/news **cascade** (depends on T004–T009)
- [x] T011 Update `EventActivityService` and `Services/Teams/TeamActivityService` to order by / project `Event.StartsAt` (was `Event.Date`), mapping to `ActivityItemDto`'s `DateOnly` in memory after materialization so the activity DTO shape is unchanged; then **grep the whole solution (incl. the integration-test project and `DevDataSeeder`) for every remaining `Event.Date` / `.Date` reference and update it to `StartsAt`** so the build and existing 003/005 activity tests stay green (depends on T004)
- [x] T012 Generate EF migration `AddEvents` in `backend/Data/Migrations/` (alters Events, creates 5 tables) and confirm it applies on a fresh DB (depends on T010)
- [x] T013 [P] Create event DTOs (`CreateEventRequest`, `EditEventRequest`, `SignupRequest`, `CreateContactRequest`, `CreateTargetedInviteRequest`, `EventDetailDto`, `EventPublicDto`, `ViewerRelationDto`, `SignupDto`, `EventContactDto`, `EventNewsDto`, `EventAdminDto`, `EventInvitationDto`, `InviteLinkDto`, `InvitableUserDto`, `InvitePreviewDto`) in `backend/Dtos/Events/EventDtos.cs`
- [ ] T014 [P] Add `EventMapping` Mapster config (entities → DTOs; a public projection that never leaks pending-invite tokens or user emails) in `backend/Services/Events/EventMapping.cs`; register in `AddMappingConfig`
- [x] T015 Implement `EventAdminGuard` (resolve whether a user is an admin of an event id, single query; mirrors `TeamMembershipGuard`) in `backend/Services/Events/EventAdminGuard.cs` (depends on T004–T012)
- [x] T016 Implement `EventCapacity` helper (occupied-count = Joined+AwaitingApproval; `SELECT … FOR UPDATE` on the event row inside a `Serializable` transaction; decide Joined/AwaitingApproval/Waitlisted) in `backend/Services/Events/EventCapacity.cs` (depends on T004–T012)
- [x] T017 Define `IEventService` + `EventService` skeleton (create, get detail+viewer, edit, cancel) in `backend/Services/Events/`; register DI in `backend/Program.cs` (depends on T013–T016)
- [x] T018 Define `IEventSignupService` + skeleton (signup, withdraw, list-group, approve, promote, remove) in `backend/Services/Events/`; register DI (depends on T013–T016)
- [ ] T019 [P] Define `IEventAdminService` + skeleton (list admins, remove, step-down) in `backend/Services/Events/`; register DI (depends on T013–T015)
- [ ] T020 [P] Define `IEventInvitationService` + skeleton (link get/rotate/revoke, targeted create, list, user-search, preview, accept, decline) in `backend/Services/Events/`; register DI (depends on T013–T015)
- [ ] T021 [P] Define `IEventNewsService` + `IEventContactService` skeletons (public read paged, admin write) in `backend/Services/Events/`; register DI (depends on T013–T015)
- [ ] T022 [P] Add frontend event models (Detail/Public, ViewerRelation, Signup, Contact, News, Admin, Invitation, InviteLink, InvitablePreview + enums) and an `EventService` skeleton (signals) in `frontend/apps/web/src/app/core/models/event.models.ts` and `frontend/apps/web/src/app/core/services/event.service.ts`

**Checkpoint**: Schema migrates, activity still green, service seams exist — stories can begin.

---

## Phase 3: User Story 1 - Create an event with the guided wizard (Priority: P1) 🎯 MVP

**Goal**: Any signed-in user creates an event through the stepped wizard and becomes its first admin.

**Independent Test**: Walk the wizard for an in-person paid teams-only event and a virtual free individuals-only event → each created with the entered details, creator is sole admin, lands on `/events/{id}`; invalid inputs at each step refused server-side.

### Tests for User Story 1 ⚠️

- [x] T023 [P] [US1] Integration tests (create in-person paid teams-only + virtual free individuals-only; creator becomes admin; end<start → 400; in-person missing country → 400; virtual missing link → 400; limit ≤ 0 → 400; paid missing recipient/IBAN → 400; `Other` type requires custom label; unauthenticated → 401) in `backend/tests/JuggerHub.Api.IntegrationTests/Events/CreateEventTests.cs`

### Implementation for User Story 1

- [x] T024 [US1] Implement `EventService.CreateAsync` (server-side validation of name/dates/location-by-kind/mode/limit/fee; create `Event` + creator `EventAdmin` via explicit `DbSet.Add` in one transaction) in `backend/Services/Events/EventService.cs` (depends on T017)
- [x] T025 [US1] Create `EventsController` with `POST /api/v1/events` (auth → 201 `EventDetailDto`, 400 validation) in `backend/Controllers/EventsController.cs` (depends on T024)
- [ ] T026 [US1] Implement `event.service.ts` `createEvent` in `frontend/apps/web/src/app/core/services/event.service.ts` (depends on T022)
- [ ] T027 [US1] Create the `event-create` wizard (steps: type&name, when, where [in-person/virtual toggle], who-can-join + limit, fee, review) with round-knob progress mirroring onboarding, in `frontend/apps/web/src/app/features/events/event-create/` (`event-create.component.{ts,html,css}` + `steps/`) (depends on T026)
- [ ] T028 [US1] Add guarded route `/events/new` (in shell, `authGuard`) + an "Events" / "Create event" entry point in the sidebar/dashboard in `frontend/apps/web/src/app/app.routes.ts` and `frontend/apps/web/src/app/layout/` (depends on T027)
- [ ] T029 [P] [US1] Unit test for wizard step validators (end≥start; country required for in-person; link required for virtual; positive limit; paid requires recipient+IBAN) in `frontend/apps/web/src/app/features/events/event-create/event-create.component.spec.ts`

**Checkpoint**: A user can create an event and is its admin; MVP foundation in place.

---

## Phase 4: User Story 2 - View the public event page (Priority: P1)

**Goal**: Anyone (incl. logged-out) opens `/events/{id}` and sees the event details, the three participant groups, news, and contacts; a signed-in viewer additionally gets their relationship (admin? my signup? teams I can enter).

**Independent Test**: As a logged-out visitor, the page renders all public fields, groups, news, contacts, and a full/cancelled event shows the right state; anonymous access needs no auth.

### Tests for User Story 2 ⚠️

- [x] T030 [P] [US2] Integration test: `GET /events/{id}` anonymous returns public detail (address for in-person / link for virtual; fee block; occupiedSpots/isFull) with **no** admin internals; a signed-in caller also gets `viewer` (isAdmin, mySignupStatus, teamsICanEnter); unknown id → 404; cancelled event marked cancelled in `backend/tests/JuggerHub.Api.IntegrationTests/Events/EventDetailTests.cs`
- [x] T031 [P] [US2] Integration test: `GET /events/{id}/participants?group=joined|awaiting|waitlist` (anonymous, paginated, bounded; correct grouping by `SignupStatus`) in `backend/tests/JuggerHub.Api.IntegrationTests/Events/ParticipantListTests.cs`

### Implementation for User Story 2

- [x] T032 [US2] Implement `EventService.GetDetailAsync` (anonymous public projection + occupied/isFull; when a userId is present, compute `ViewerRelationDto` — admin via `EventAdminGuard`, my signup, teams the caller administers matching the mode) in `backend/Services/Events/EventService.cs` (depends on T017, T015)
- [x] T033 [US2] Implement `EventSignupService.ListGroupAsync` (paged by `SignupStatus`; project user handle/displayName or team slug/name) in `backend/Services/Events/EventSignupService.cs` (depends on T018)
- [x] T034 [US2] Add endpoints to `EventsController`: `GET {id}` and `GET {id}/participants` (both `[AllowAnonymous]`, paginated). `GET {id}` is **optional-auth** — anonymous by default, but when a valid auth cookie is present it reads the user id and populates `viewer`; `TryGetUserId` returning false ⇒ anonymous projection with an empty `viewer` — in `backend/Controllers/EventsController.cs` (depends on T032, T033)
- [ ] T035 [US2] Add `event.service.ts` `getEvent` + `listParticipants` methods in `frontend/apps/web/src/app/core/services/event.service.ts` (depends on T022)
- [ ] T036 [US2] Create the `event-detail` public page (header: name/type/dates/location/fee; latest news; three participant groups; contacts) at `/events/:id` in `frontend/apps/web/src/app/features/events/event-detail/event-detail.component.{ts,html,css}` (depends on T035)
- [ ] T037 [P] [US2] Create `participant-groups`, `news-feed`, `contacts-list`, and `join-actions` (placeholder wiring) child components with friendly empty states in `frontend/apps/web/src/app/features/events/event-detail/components/*.{ts,html,css}`
- [ ] T038 [US2] Add route `/events/:id` **in the shell without `authGuard`** (public, like the dashboard) in `frontend/apps/web/src/app/app.routes.ts` (depends on T036)
- [ ] T039 [P] [US2] Extend `DevDataSeeder` with demo events (in-person paid teams-only tournament with joined/awaiting/waitlist, virtual free individuals-only workshop, one cancelled), creator `EventAdmin`, `EventContact`s (location host, caterer), `EventNewsPost`s; seed `EventParticipation` against seeded events so profile/team **activity** still renders (Development only) in `backend/Data/DevDataSeeder.cs` (depends on T012)

**Checkpoint**: The event page renders publicly; anonymous visitors see everything needed to decide.

---

## Phase 5: User Story 3 - Sign up for an event (join or waiting list) (Priority: P1)

**Goal**: A signed-in user joins as themselves (individuals-only) or enters a team they administer (teams-only); routing is free→joined, paid→awaiting, full→waitlist; capacity is atomic; withdrawal never auto-promotes.

**Independent Test**: Join a free individuals-only event → Joined; sign up for a paid event → AwaitingApproval with pay-to info; when full → Waitlisted; withdraw → spot released, nobody promoted; mismatches/duplicates refused.

### Tests for User Story 3 ⚠️

- [x] T040 [P] [US3] Integration test: routing (free+open→Joined, paid+open→AwaitingApproval, full→Waitlisted); mode mismatch → 400/403; team entry requires caller administers the team (else 403); duplicate signup → 409; withdraw releases spot with no auto-promotion; cancelled/ended event → 409 in `backend/tests/JuggerHub.Api.IntegrationTests/Events/SignupTests.cs`
- [x] T041 [P] [US3] Integration test: **capacity race** — N concurrent sign-ups for the last spot → exactly one occupies it, the rest Waitlisted; occupied never exceeds the limit in `backend/tests/JuggerHub.Api.IntegrationTests/Events/CapacityRaceTests.cs`

### Implementation for User Story 3

- [x] T042 [US3] Implement `EventSignupService.SignupAsync` (validate mode + team-admin authority via the 005 team role check; route via `EventCapacity` row-lock txn; block on cancelled/ended; duplicate guard) and `WithdrawAsync` (participant or team-admin; release spot; never promote) in `backend/Services/Events/EventSignupService.cs` (depends on T018, T016)
- [x] T043 [US3] Add endpoints to `EventsController`: `POST {id}/signup` (auth, `SignupRequest`) and `DELETE {id}/signup/{signupId}` (auth) in `backend/Controllers/EventsController.cs` (depends on T042)
- [ ] T044 [US3] Add `event.service.ts` `signup` + `withdraw` methods in `frontend/apps/web/src/app/core/services/event.service.ts` (depends on T022)
- [ ] T045 [US3] Implement the `join-actions` component (join / join waiting list when full / withdraw; team picker for teams-only from `viewer.teamsICanEnter`; show pay-to recipient/IBAN/deadline for awaiting-approval) in `frontend/apps/web/src/app/features/events/event-detail/components/join-actions.component.{ts,html,css}` (depends on T044)

**Checkpoint**: The core loop (create → view → sign up / waitlist) works end to end for a second user.

---

## Phase 6: User Story 4 - Administer participants and the waiting list (Priority: P2)

**Goal**: An admin approves paid sign-ups, promotes from the waiting list into open spots (manual only), and removes participants; plus edits event details under the mode/limit guards.

**Independent Test**: Approve an awaiting entry → Joined; promote a waitlist entry into a freed spot (refused if full); remove joined/waitlist entries; edit raises the limit but can't lower below occupied nor switch mode with sign-ups present; non-admins refused.

### Tests for User Story 4 ⚠️

- [ ] T046 [P] [US4] Integration test: approve (awaiting→Joined, sets PaymentConfirmedDate); promote (free→Joined / paid→AwaitingApproval; 409 when no open spot); remove (releases spot, no auto-promotion); **non-admin → 403** for all in `backend/tests/JuggerHub.Api.IntegrationTests/Events/ParticipantAdminTests.cs`
- [ ] T047 [P] [US4] Integration test: edit — raise limit OK; lower below occupied → 409/400; change `participantMode` with sign-ups present → 409; other fields editable; non-admin → 403 in `backend/tests/JuggerHub.Api.IntegrationTests/Events/EditEventTests.cs`

### Implementation for User Story 4

- [ ] T048 [US4] Implement `EventSignupService.ApproveAsync` / `PromoteAsync` / `RemoveAsync` (admin-gated via `EventAdminGuard`; promote re-checks capacity under the row lock; no auto-promotion anywhere) in `backend/Services/Events/EventSignupService.cs` (depends on T042, T015, T016)
- [ ] T049 [US4] Implement `EventService.EditAsync` (admin-gated; mode locked when any signup exists; limit ≥ current occupied; re-validate location/fee by kind) in `backend/Services/Events/EventService.cs` (depends on T017)
- [ ] T050 [US4] Add endpoints to `EventsController`: `POST {id}/participants/{signupId}/approve`, `POST {id}/participants/{signupId}/promote`, and `PATCH {id}` (edit). **Do not add a second delete route** — admin-remove reuses the single `DELETE {id}/signup/{signupId}` from T043; the service branches authorization (participant/team-admin **or** event-admin) so one endpoint serves both withdraw and admin-remove in `backend/Controllers/EventsController.cs` (depends on T048, T049)
- [ ] T051 [US4] Add `event.service.ts` `approve`/`promote`/`removeParticipant`/`editEvent` methods in `frontend/apps/web/src/app/core/services/event.service.ts` (depends on T022)
- [ ] T052 [US4] Create the `event-manage` screen (three groups with approve/promote/remove; edit-details form) + route `/events/:id/manage` (`authGuard`) and a "Manage" affordance on `event-detail` shown only when `viewer.isAdmin` in `frontend/apps/web/src/app/features/events/event-manage/event-manage.component.{ts,html,css}` and `app.routes.ts` (depends on T051)

**Checkpoint**: Admins can run a paid, full event; edits are guarded; nothing auto-fills.

---

## Phase 7: User Story 5 - Post news updates (Priority: P2)

**Goal**: Admins post news; everyone reads it newest-first on the public page.

**Independent Test**: Admin posts → appears at the top for all viewers (incl. logged-out); non-admin has no compose affordance and a direct post is refused; empty feed shows a friendly state.

### Tests for User Story 5 ⚠️

- [ ] T053 [P] [US5] Integration test: `POST {id}/news` admin-only (non-admin → 403); `GET {id}/news` anonymous, paginated, newest-first, empty state in `backend/tests/JuggerHub.Api.IntegrationTests/Events/NewsTests.cs`

### Implementation for User Story 5

- [ ] T054 [US5] Implement `EventNewsService` (public `GetFeedAsync` paged newest-first with author displayName; admin `PostAsync`) in `backend/Services/Events/EventNewsService.cs` (depends on T021, T015)
- [ ] T055 [US5] Add endpoints to `EventsController`: `GET {id}/news` (anon, paged) and `POST {id}/news` (admin) in `backend/Controllers/EventsController.cs` (depends on T054)
- [ ] T056 [US5] Add `event.service.ts` `listNews`/`postNews` and wire a compose affordance into `news-feed` shown only to admins in `frontend/apps/web/src/app/core/services/event.service.ts` and `event-detail/components/news-feed.component.{ts,html,css}` (depends on T022)

**Checkpoint**: Event news works read-for-all, write-for-admins.

---

## Phase 8: User Story 6 - Manage event contacts (Priority: P2)

**Goal**: Admins maintain a free-form contacts list (name + role + phone and/or email) shown publicly.

**Independent Test**: Add a contact (≥1 method) → shows publicly; add with neither method → refused; update/remove reflected; non-admin CUD refused.

### Tests for User Story 6 ⚠️

- [ ] T057 [P] [US6] Integration test: `GET {id}/contacts` anonymous paged; `POST`/`PATCH`/`DELETE` admin-only (non-admin → 403); neither phone nor email → 400 in `backend/tests/JuggerHub.Api.IntegrationTests/Events/ContactsTests.cs`

### Implementation for User Story 6

- [ ] T058 [US6] Implement `EventContactService` (public `ListAsync` paged; admin `AddAsync`/`UpdateAsync`/`RemoveAsync` with the ≥1-method rule) in `backend/Services/Events/EventContactService.cs` (depends on T021, T015)
- [ ] T059 [US6] Add endpoints to `EventsController`: `GET {id}/contacts` (anon), `POST {id}/contacts`, `PATCH {id}/contacts/{contactId}`, `DELETE {id}/contacts/{contactId}` (admin) in `backend/Controllers/EventsController.cs` (depends on T058)
- [ ] T060 [US6] Add `event.service.ts` contact CRUD methods and create the `event-contacts` admin screen + route `/events/:id/contacts` (`authGuard`), with `contacts-list` rendering on the public page in `frontend/apps/web/src/app/core/services/event.service.ts`, `frontend/apps/web/src/app/features/events/event-contacts/event-contacts.component.{ts,html,css}`, and `app.routes.ts` (depends on T022)

**Checkpoint**: Contacts are public, admin-managed, and validated.

---

## Phase 9: User Story 7 - Invite co-admins to help administer (Priority: P2)

**Goal**: An admin invites co-admins by shareable link or targeted email; accepting grants full admin powers; the event always keeps ≥1 admin.

**Independent Test**: Copy/rotate/revoke the link; search a user → targeted invite emailed (Mailpit); accept → co-admin can edit/news/manage; last admin can't step down/be removed; non-admins refused.

### Tests for User Story 7 ⚠️

- [ ] T061 [P] [US7] Integration test: link create/rotate (≤1 active) + revoke + 7-day expiry; targeted create emails a `/event-invite/{token}` link + duplicate/already-admin guards; preview Usable/Expired/Invalid; **accept grants `EventAdmin`** (idempotent if already admin); decline; all admin-gated in `backend/tests/JuggerHub.Api.IntegrationTests/Events/CoAdminInviteTests.cs`
- [ ] T062 [P] [US7] Integration test: **last-admin guard** — removing/stepping-down the sole admin → 409 (incl. the concurrent "two admins remove each other" race → ≥1 admin remains); non-admin remove → 403 in `backend/tests/JuggerHub.Api.IntegrationTests/Events/EventAdminsTests.cs`

### Implementation for User Story 7

- [ ] T063 [US7] Implement `EventInvitationService` (link get/rotate/revoke, targeted create with dup/already-admin guards, list paged, user-search with `UserRelation`, anonymous preview, `AcceptAsync` → add `EventAdmin` idempotently, decline) — token via `RandomNumberGenerator`, expiry from `EventOptions` in `backend/Services/Events/EventInvitationService.cs` (depends on T020)
- [ ] T064 [US7] Create `EventEmailService` (render template, build `{FrontendBaseUrl}/event-invite/{token}`, send via `IEmailSender`) + `event-admin-invite.html` (extends base header/footer) in `backend/Services/Email/EventEmailService.cs` and `backend/EmailTemplates/`; add the template method to `IEmailTemplateService`/impl; register DI; wire into targeted-invite create (depends on T063)
- [ ] T065 [US7] Implement `EventAdminService` (list admins paged; remove/step-down through the event-row-lock last-admin guard, mirroring `TeamService.MutateMembershipAsync`) in `backend/Services/Events/EventAdminService.cs` (depends on T019, T016)
- [ ] T066 [US7] Add endpoints: on `EventsController` — `GET/POST {id}/invitations/link`, `GET/POST {id}/invitations`, `DELETE {id}/invitations/{invitationId}`, `GET {id}/invitations/user-search`, `GET {id}/admins`, `DELETE {id}/admins/{userId}` (all admin); new `EventInvitationsController` — `GET /api/v1/event-invitations/{token}` (anon), `POST …/accept`, `POST …/decline` (auth) in `backend/Controllers/EventsController.cs` and `backend/Controllers/EventInvitationsController.cs` (depends on T063, T065)
- [ ] T067 [US7] Add `event.service.ts` invitation + admin methods and create the `event-admins` screen (admins list, copy link, user-search invite, step-down) + route `/events/:id/admins` (`authGuard`) in `frontend/apps/web/src/app/core/services/event.service.ts`, `frontend/apps/web/src/app/features/events/event-admins/event-admins.component.{ts,html,css}`, and `app.routes.ts` (depends on T022)
- [ ] T068 [US7] Create the `event-invite-accept` component (full-screen, **outside** shell): anonymous preview + Accept (co-admin) / Decline + expired/invalid state + sign-in/register return handling, at `/event-invite/:token` in `frontend/apps/web/src/app/features/events/event-invite-accept/event-invite-accept.component.{ts,html,css}` and `app.routes.ts` (depends on T067)

**Checkpoint**: Shared administration works; the event can never drop below one admin.

---

## Phase 10: User Story 8 - Cancel an event (Priority: P3)

**Goal**: An admin cancels irreversibly; the page stays up marked cancelled; sign-ups/approvals/promotions are refused; joined + waiting are emailed.

**Independent Test**: Cancel a seeded event with participants → cancelled state shown, all sign-up/waitlist/approve/promote refused, Mailpit shows a cancellation email to each joined/awaiting/waiting recipient (individuals + team admins), no reactivate path; non-admin refused.

### Tests for User Story 8 ⚠️

- [ ] T069 [P] [US8] Integration test: admin cancel sets Cancelled + refuses subsequent signup/approve/promote (409); page still readable; **emails sent** to every joined/awaiting/waiting recipient (individual users + team admins) — assert `IEmailSender` invoked; non-admin → 403; no reactivate endpoint in `backend/tests/JuggerHub.Api.IntegrationTests/Events/CancelEventTests.cs`

### Implementation for User Story 8

- [ ] T070 [US8] Implement `EventService.CancelAsync` (admin-gated; set `Status=Cancelled`+`CancelledDate`; collect recipient emails — individual signups' users + team signups' team admins — and send `event-cancelled.html` best-effort via `EventEmailService`) in `backend/Services/Events/EventService.cs` and add `event-cancelled.html` to `backend/EmailTemplates/` + template method (depends on T017, T064)
- [ ] T071 [US8] Gate `SignupAsync`/`ApproveAsync`/`PromoteAsync` on `Status != Cancelled` (and not ended) — confirm the refusals wired in T042/T048 cover cancelled (depends on T070)
- [ ] T072 [US8] Add `POST /api/v1/events/{id}/cancel` (admin) to `EventsController`, and the danger-zone cancel (with confirm) to `event-manage` + `event.service.ts` `cancelEvent` in `backend/Controllers/EventsController.cs`, `frontend/apps/web/src/app/features/events/event-manage/event-manage.component.{ts,html,css}`, and `frontend/apps/web/src/app/core/services/event.service.ts` (depends on T070)

**Checkpoint**: Cancellation is irreversible, read-only for sign-ups, and notifies everyone.

---

## Phase 11: Polish & Cross-Cutting Concerns

- [ ] T073 [P] DESIGN.md conformance + responsiveness pass (phone ~375px, desktop ~1280px; empty/loading/error states; one primary CTA per view; wizard round-knob progress matches onboarding) across all event components
- [ ] T074 Playwright e2e `events.spec.ts` — create wizard → view as visitor → sign up as 2nd user → approve/promote/remove → post news + contact → invite co-admin (accept as 3rd user) → cancel, desktop + mobile in `frontend/apps/web-e2e/src/events.spec.ts`
- [ ] T075 [P] Reconcile `.env.sample` / `appsettings*.json` / `docker-compose.yml` for any `Events:*` config; confirm the global `JsonStringEnumConverter` serializes the new enums by name
- [ ] T076 Run `/speckit-analyze` cross-artifact consistency; confirm the `Event.Date`→`StartsAt`/`EndsAt` change leaves profile (003) and team (005) activity green; note events-index deferral as intentional scope
- [ ] T077 Run `quickstart.md` Scenarios A–H end-to-end (Docker) and record results

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: depends on Setup; **blocks all stories**. Entities T004–T009 parallel; then T010→T012 sequential (T011 depends only on T004); T013–T014, T019–T022 parallel; T015/T016→T017/T018.
- **US1 (Phase 3)**: depends on Foundational. **MVP core.**
- **US2 (Phase 4)**: depends on Foundational; reads an event (US1 for data) but is code-independent.
- **US3 (Phase 5)**: depends on Foundational + `EventCapacity`; completes the core loop with US1/US2.
- **US4 (Phase 6)**: depends on US3 (acts on existing sign-ups) + `EventCapacity`.
- **US5 (Phase 7)** / **US6 (Phase 8)**: depend on Foundational + `EventAdminGuard`; independent of each other.
- **US7 (Phase 9)**: depends on Foundational + `EventAdminService`/`EventCapacity`; reuses the email pipeline.
- **US8 (Phase 10)**: depends on US7's `EventEmailService` (for the template pattern) and US3/US4 (to refuse sign-ups/admissions when cancelled).
- **Polish (Phase 11)**: depends on the delivered stories.

### Story Independence

US1 creates. US2 reads publicly. US3 signs up. US4 administers participants + edits. US5 news, US6 contacts (parallelizable). US7 shares administration. US8 cancels. Each is independently testable once Foundational is done; later stories integrate without breaking earlier ones.

### Within Each Story

- Tests written first and expected to FAIL before implementation.
- Backend: entities/config → guard/capacity/service → endpoints; then frontend service → component → route.

### Parallel Opportunities

- Setup: T001–T003 all [P].
- Foundational: entities T005–T009 [P] (T004 first — Event drives config); then T010→T012; T013–T014, T019–T022 [P].
- Each story's `[P]` tests run together; a story's backend and frontend `[P]` tasks proceed in parallel. US5 and US6 can be built in parallel.

---

## Parallel Example: Foundational entities

```bash
Task: "Create EventSignup entity in backend/Entities/EventSignup.cs"
Task: "Create EventAdmin entity in backend/Entities/EventAdmin.cs"
Task: "Create EventAdminInvitation entity in backend/Entities/EventAdminInvitation.cs"
Task: "Create EventContact entity in backend/Entities/EventContact.cs"
Task: "Create EventNewsPost entity in backend/Entities/EventNewsPost.cs"
```

---

## Implementation Strategy

### MVP First (US1 + US2 + US3)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. US1 (create) → 4. US2 (public page) → 5. US3 (sign up / waitlist) → **STOP & VALIDATE**: a user creates an event, a visitor views it, a second user signs up / waitlists → demo.

### Incremental Delivery

Foundation → US1 (create) → US2 (view) → US3 (sign up) → US4 (participant admin + edit) → US5 (news) → US6 (contacts) → US7 (co-admins) → US8 (cancel) → Polish. Each story adds value without breaking the previous.

---

## Notes

- [P] = different files, no incomplete-task dependency. [Story] labels map to spec.md US1–US8.
- **Security invariants to keep green** (contracts/README): the public page/lists carry only public event fields — no pending-invite tokens or user emails (FR-008/011/033); sign-up requires auth + mode/team-admin authority (FR-012/013); every admin action authorized server-side by `EventAdmin` (FR-022/033, SC-002); capacity never exceeded under concurrency (FR-016, SC-004); no auto-promotion — approve/promote are explicit (FR-020, SC-005); last-admin guard atomic (FR-029, SC-009); cancelled/ended events refuse sign-ups/admissions (FR-032, SC-007); contacts require ≥1 method (FR-025); all lists paginated (FR-034).
- **Spec drift / cross-feature to flag at PR**: `Event.Date`→`StartsAt`/`EndsAt` touches the 003/005 activity readers — keep profile and team activity green (T011). The rich `Event` **extends** the existing minimal one; `EventSignup` (live registration) is intentionally separate from `EventParticipation` (activity). No events-index page this iteration (clarified). Reactivation of a cancelled event is intentionally unsupported.
- Commit after each task or logical group; stop at any checkpoint to validate independently.
