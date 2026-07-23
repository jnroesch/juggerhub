---
description: "Task list for feature 027 — Contact the Admins"
---

# Tasks: Contact the Admins

**Input**: Design documents from `specs/027-contact-admins/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/chat-contact-api.md](contracts/chat-contact-api.md)

**Tests**: Included — this feature extends the feature-019 chat subsystem, which is covered by
`JuggerHub.Api.IntegrationTests/Chat/*` and Angular zoneless component specs. New behavior gets the
same coverage; tests are written alongside (repo norm), not omitted.

**Organization**: Grouped by user story. US1 and US2 are both P1 (US1 = messaging works end-to-end;
US2 = the threads are distinguishable). US3 (P2) = membership follows the roster + archival.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: US1 / US2 / US3 (setup, foundational, polish carry no story label)

## Path Conventions

Web app: backend at `backend/`, frontend at `frontend/apps/web/src/app/`.

---

## Phase 1: Setup

**Purpose**: Working branch and a green baseline before touching the chat subsystem.

- [X] T001 Create branch `feat/027-contact-admins` from `main`; confirm `dotnet build` (backend) and
  `nx build web` (frontend) are green as a baseline.
- [X] T002 Re-read the touch-points named in [plan.md](plan.md) §Project Structure and skim the
  existing chat tests (`backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatTeamPartyTests.cs`,
  `ChatArchiveTests.cs`, `ChatLazyDirectTests.cs`) so new tests follow the same harness
  (`ChatTestSupport`, `FakeChatRealtime`).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The data model + membership derivation every story depends on. No story can function
until inquiry conversations exist and `ChatGuard` can resolve their members.

**⚠️ CRITICAL**: Complete this entire phase before starting any user story.

- [X] T003 Add `TeamInquiry = 4` and `EventInquiry = 5` to `ConversationKind` in
  `backend/Entities/ChatEnums.cs` (append-only; XML-doc both as mirrored kinds — membership derived
  from the team/event admin roster). See data-model E1.
- [X] T004 Add `EventId` (`Guid?`), `RequesterUserId` (`Guid?`), and `Event? Event` /
  `User? Requester` navigations to `backend/Entities/Conversation.cs`; document that `TeamId` is
  reused for `TeamInquiry` and that both new columns are nulled/retained on archival (data-model E2).
- [X] T005 In `backend/Data/AppDbContext.cs` `Entity<Conversation>`: (a) re-scope the existing
  `IX_Conversations_TeamId` filter to `"TeamId" IS NOT NULL AND "Kind" = 2`; (b) add unique filtered
  indexes `(TeamId, RequesterUserId) WHERE "Kind" = 4` and `(EventId, RequesterUserId) WHERE
  "Kind" = 5`; (c) add `Restrict` FKs for `Event` and `Requester`. See data-model R1–R3.
- [X] T006 Generate the EF migration
  (`dotnet ef migrations add ContactAdminsInquiryThreads` in `backend/`); review the generated SQL for
  the re-scoped index (drop+recreate with new filter), the two new indexes, columns, and FKs; ensure
  no unintended table rewrite.
- [X] T007 Extend `ChatAccess` in `backend/Services/Chat/ChatGuard.cs` with `Guid? EventId`,
  `Guid? RequesterUserId`, and `bool IsInquiry => Kind is ConversationKind.TeamInquiry or
  ConversationKind.EventInquiry`; thread the two new fields through `ResolveAsync`'s projection and
  its returned `ChatAccess`.
- [X] T008 Add the inquiry branches to the membership predicate in `ChatGuard.cs` — both the static
  `IsMemberOf(db, userId)` expression and the inline predicate inside `ResolveAsync`: `TeamInquiry` ⇒
  requester **or** `TeamMemberships.Any(Role == Admin)`; `EventInquiry` ⇒ requester **or**
  `EventAdmins.Any`. Keep the archived-snapshot branch first (data-model M1).
- [X] T009 Add inquiry cases to `ChatGuard.ResolveParticipantUserIdsAsync` — `TeamInquiry` ⇒
  `{ RequesterUserId } ∪ team admins`; `EventInquiry` ⇒ `{ RequesterUserId } ∪ event admins`;
  archived falls to the participant-snapshot branch (data-model M2). De-duplicate.

**Checkpoint**: Inquiry conversations are representable and `ChatGuard` resolves their membership and
fan-out. User stories can begin.

---

## Phase 3: User Story 1 — Ask a team's or event's admins a question (Priority: P1) 🎯 MVP

**Goal**: From a team/event page a non-admin player can send a first message that creates one reusable
thread, delivered to every current admin, who can reply — visible back to the player.

**Independent Test**: As a non-admin, send from a team page and (separately) an event page; verify both
current admins receive it in one shared thread and a reply is visible to the player; a second send
reuses the thread.

### Tests for User Story 1

- [X] T010 [P] [US1] Integration test `ChatInquiryTests.cs` in
  `backend/tests/JuggerHub.Api.IntegrationTests/Chat/`: first send creates a `TeamInquiry`, message
  reaches both team admins, thread is reused on second send (one row), and an admin reply is readable
  by the requester. Mirror for `EventInquiry`.
- [X] T011 [P] [US1] Integration test in `ChatInquiryTests.cs`: `SendFirstInquiry` is rejected when the
  caller is already an admin of the target (FR-002), and returns 404 for a missing/invisible target and
  409 for a cancelled event.

### Implementation for User Story 1

- [X] T012 [US1] Add `InquiryMessageSentDto(ConversationSummaryDto Conversation, MessageDto Message)`
  to `backend/Dtos/Chat/ChatDtos.cs` (or reuse `DirectMessageSentDto`); no other DTO shape changes.
- [X] T013 [US1] Extend the inbox projection in
  `backend/Services/Chat/ChatConversationService.cs` (`GetInboxAsync` + `GetDetailAsync` selects) to
  read `EventId`, `RequesterUserId`, `EventTitle = c.Event!.Title`, and
  `RequesterName = c.Requester!.Profile!.DisplayName`, and to construct `ChatAccess` with the new
  fields so cutoffs resolve. (Viewer-appropriate `DisplayName`/avatar is US2 — an interim label is
  fine here.)
- [X] T014 [US1] Implement `EnsureInquiryAsync(callerId, kind, targetId)` in
  `ChatConversationService.cs`: find existing by `(Kind, TeamId|EventId, RequesterUserId)`; create if
  absent; catch `DbUpdateException` and resolve to the winner (mirror `EnsureDirectAsync`/
  `EnsureAutoAsync`). Race-safe via the T005 unique indexes.
- [X] T015 [US1] Implement `SendFirstInquiryAsync(callerId, kind, targetId, body)` in
  `ChatConversationService.cs`: validate target exists/visible and not cancelled; reject if caller is
  an admin of the target (FR-002); `EnsureInquiryAsync` → `IChatMessageService.SendAsync` → return
  `InquiryMessageSentDto` (summary + message). Add signatures to
  `backend/Services/Chat/IChatConversationService.cs`.
- [X] T016 [US1] Add endpoints to `backend/Controllers/ChatConversationsController.cs`:
  `POST chat/contact/team/{teamId:guid}/messages` and `POST chat/contact/event/{eventId:guid}/messages`
  (both `[EnableRateLimiting(RateLimitPolicies.ChatStart)]`, thin → service, 201 + `Location`); plus
  the optional `GET chat/contact/team|event/{id}` resolve returning `InquiryThreadRefDto`
  (per [contracts/chat-contact-api.md](contracts/chat-contact-api.md)).
- [X] T017 [P] [US1] Frontend: add `TeamInquiry`/`EventInquiry` to `ConversationKind` and the request/
  response types in `frontend/apps/web/src/app/core/models/chat.models.ts`.
- [X] T018 [P] [US1] Frontend: add `sendFirstInquiryToTeam(teamId, body)`,
  `sendFirstInquiryToEvent(eventId, body)`, and `resolveInquiryThread(kind, id)` to
  `frontend/apps/web/src/app/core/services/chat.service.ts`.
- [X] T019 [US1] Frontend: wire `chat-compose.component.ts`/`.html` to accept a team/event inquiry
  target and call the new service methods on send (reuse the lazy-DM compose pattern); on an existing
  thread (from `resolveInquiryThread`) navigate straight into it.
- [X] T020 [US1] Frontend: add a **"Contact admins"** action to
  `frontend/apps/web/src/app/features/teams/team-detail/team-detail.component.ts`/`.html`, hidden when
  the viewer is a team admin (FR-002), opening the inquiry compose for the team (DESIGN.md for button
  placement/tokens).
- [X] T021 [US1] Frontend: same **"Contact admins"** action on
  `frontend/apps/web/src/app/features/events/event-detail/event-detail.component.ts`/`.html`, hidden
  for event admins and for cancelled events.

**Checkpoint**: Messaging works end-to-end from both entry points; threads are created-on-send, reused,
delivered to current admins, and repliable. (Labels are not yet viewer-perfect — that's US2.)

---

## Phase 4: User Story 2 — Tell contact threads apart from normal chats (Priority: P1)

**Goal**: Inquiry threads are visually distinct — an **ADMINS** tag, the team/event name for the
player, the player's name for admins — so neither side confuses them with normal groups.

**Independent Test**: With one normal group and one inquiry thread, the inbox shows the inquiry with the
ADMINS tag and the correct per-viewer name/avatar; the normal group shows neither.

### Tests for User Story 2

- [X] T022 [P] [US2] Integration test in `ChatInquiryTests.cs`: the summary/detail `Name` is the team
  name / event title for the requester and the requester's display name for an admin (FR-009/FR-010),
  and the avatar kind differs per viewer.
- [X] T023 [P] [US2] Frontend spec: `chat-inbox.component.spec.ts` asserts `tagFor()` returns `ADMINS`
  for `TeamInquiry`/`EventInquiry` and nothing for `Group`/`Direct`.

### Implementation for User Story 2

- [X] T024 [US2] In `ChatConversationService.cs`, extend `DisplayName(...)` and `BuildAvatar(...)` to
  branch on the inquiry kinds **and the viewer**: requester ⇒ team name/event title + team crest/event
  icon; admin ⇒ requester display name + requester avatar. Pass the caller/requester ids and
  `EventTitle`/`RequesterName` through (from the T013 projection).
- [X] T025 [US2] Ensure archived inquiries keep a sensible frozen `Name` and still resolve an avatar
  (the link is nulled at archival — the name is frozen; verify `DisplayName` falls back to stored
  `Name`).
- [X] T026 [US2] Frontend: extend `tagFor(c)` in
  `frontend/apps/web/src/app/features/chat/chat-inbox/chat-inbox.component.ts` to return `ADMINS` for
  the two inquiry kinds; render it through the existing tag pill in `chat-inbox.component.html`
  (reuse the current `rounded-pill … text-eyebrow` markup — DESIGN.md tokens, no new style).
- [X] T027 [US2] Frontend: confirm `chat-details.component.ts` header shows the same per-viewer name/tag
  for an opened inquiry thread (it reads the detail DTO — verify no `Group`-only assumptions leak).

**Checkpoint**: Both P1 stories done — messaging works and threads are unambiguously distinguishable on
both sides.

---

## Phase 5: User Story 3 — Membership follows the admin roster (Priority: P2)

**Goal**: The admins reachable through a thread always reflect the current roster; a new admin sees
history from their grant; a removed admin loses access; deleting a team / cancelling an event archives
its inquiry threads (read-only, never unreadable).

**Independent Test**: Grant an admin → they gain access from the grant forward; remove an admin → they
404 on next request; delete a team / cancel an event → threads archive and stay readable.

### Tests for User Story 3

- [X] T028 [P] [US3] Integration test in `ChatInquiryTests.cs`: granting admin surfaces the thread with
  history from the grant only (FR-019); removing admin makes the thread 404 (not 403) and drop from the
  inbox (FR-006/FR-012).
- [X] T029 [P] [US3] Extend `ChatArchiveTests.cs`: deleting a team archives its team chat **and** all
  its `TeamInquiry` threads (snapshot readable, sends rejected); cancelling an event archives all its
  `EventInquiry` threads.

### Implementation for User Story 3

- [X] T030 [US3] Add inquiry cases to `ChatGuard.ResolveJoinCutoffAsync` and
  `ResolveJoinCutoffsAsync` (batched): requester ⇒ participant `JoinedDate`; team admin ⇒
  `TeamMembership.JoinedDate`; event admin ⇒ `EventAdmin.AddedDate`; archived ⇒ null (data-model M3).
- [X] T031 [US3] Generalize archival in `ChatConversationService.cs`: extract
  `ArchiveConversationAsync(conversationId)` (snapshot derived roster → freeze `Name` → null
  `TeamId`/`EventId` → set `Archived`) from the current `ArchiveAutoAsync`; keep it idempotent (R3a).
- [X] T032 [US3] Extend `ArchiveForTeamAsync(teamId)` to also archive every `TeamInquiry` for the team,
  and add `ArchiveInquiriesForEventAsync(eventId)`; declare both on
  `IChatConversationService.cs`.
- [X] T033 [US3] Wire `backend/Services/Teams/TeamService.cs` `DeleteAsync` so its existing
  `ArchiveForTeamAsync` call now also covers inquiry threads (via T032) **before** the
  `ExecuteDeleteAsync` — the `Restrict` FK must not block the delete.
- [X] T034 [US3] Wire `backend/Services/Events/EventService.cs` `CancelAsync` to call
  `ArchiveInquiriesForEventAsync(eventId)` as part of cancellation.

**Checkpoint**: All three stories independently functional; membership is correct by construction and
archival is safe.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T035 [P] Instantiate the UI review checklist: copy
  `.specify/templates/ui-review-checklist-template.md` to
  `specs/027-contact-admins/checklists/ui-review.md` and verify the entry-point button, the ADMINS
  tag, and empty/loading/error states against DESIGN.md (DESIGN.md wins on conflict — Gate 7).
- [X] T036 [P] Add a rate-limit assertion (the `ChatStart` policy applies to the two send endpoints)
  and confirm `ChatDoesNotTouchAlertsTests` stays green (no new alert rows — FR-018) in
  `backend/tests/JuggerHub.Api.IntegrationTests/Chat/`.
- [X] T037 Run `specs/027-contact-admins/quickstart.md` scenarios 1–7 against the docker-compose stack
  (drive the real app, not just tests — the 019 verification bar).
- [X] T038 [P] Verify `GetDetailAsync` reports `CanLeave = false` / `CanAddMembers = false` and that
  mute/hide work for inquiry threads (FR-017); add/extend a test if not covered.
- [X] T039 Full verification: `dotnet test` (backend), `nx test web` + `nx lint web` + `nx build web`
  (frontend); apply the migration cleanly on a fresh DB.
- [X] T040 [P] Update the auto-memory: add a `parties/marketplace`-style decisions file for 027 and a
  one-line pointer in `MEMORY.md` (kind naming `TeamInquiry`/`EventInquiry` to dodge the `EventContact`
  clash; reused `TeamId` + re-scoped index; derived admin membership; snapshot archival across N
  threads).

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (P1)** → no deps.
- **Foundational (P2, T003–T009)** → depends on Setup; **BLOCKS all stories** (columns, indexes,
  migration, guard membership + fan-out).
- **US1 (P3)** → after Foundational. The MVP.
- **US2 (P4)** → after US1 (refines the same inbox projection US1 established).
- **US3 (P5)** → after Foundational; independent of US2. Can proceed in parallel with US2 once US1's
  service scaffolding (T013–T015) exists.
- **Polish (P6)** → after the stories it covers.

### Within stories

- Tests alongside implementation (write, watch fail, implement, green).
- Backend model/guard before service; service before controller; controller before frontend wiring.
- US2's `DisplayName`/`BuildAvatar` (T024) depends on US1's projection fields (T013).
- US3's archival (T031) refactors code US1 leaves intact; T033/T034 depend on T032.

### Parallel opportunities

- Foundational: T007 (ChatAccess) and T008/T009 (predicate) touch the same file — sequential; T003/T004
  are different files — parallelizable.
- US1: T017/T018 (frontend models/service) are `[P]` with each other and with backend T012–T016.
- US2: T022 (backend test) and T023 (frontend spec) `[P]`.
- US3: T028/T029 tests `[P]`; T030 (guard) is independent of T031–T034 (service/callers).

---

## Parallel Example: User Story 1

```text
# Backend and frontend scaffolding in parallel once Foundational is done:
T017  Frontend: ConversationKind + types in chat.models.ts
T018  Frontend: chat.service.ts inquiry methods
T010  Backend test: create/reuse/deliver/reply (ChatInquiryTests.cs)
# then the backend service chain sequentially: T012 → T013 → T014 → T015 → T016
```

---

## Implementation Strategy

### MVP (US1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 → **STOP & VALIDATE** (quickstart
   Scenarios 1–3). Messaging to admins works from both pages. Demo-able.

### Incremental delivery

1. Foundational ready → 2. **US1** (messaging MVP) → 3. **US2** (distinguishable tags/names) →
   4. **US3** (roster-follows + archival) → 5. Polish. Each story ships independently testable value.

---

## Notes

- `[P]` = different files, no incomplete-task dependency.
- The only touch to existing schema is the re-scoped `IX_Conversations_TeamId` filter (T005) — review
  the migration for a clean drop+recreate.
- Keep `Kind`/`RequesterUserId` intact through archival so the inbox still tags archived inquiries.
- Commit per task or logical group; keep frontend `.html`/`.css`/`.ts` separate (constitution VI).
