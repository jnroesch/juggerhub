---
description: "Task list for feature: Lazy Direct-Message Creation"
---

# Tasks: Lazy Direct-Message Creation

**Input**: Design documents from `specs/022-lazy-dm-creation/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/)

**Tests**: Included — backend integration tests prove the create-on-send behavior
(FR-002/005/006) and the Start narrowing (FR-009); frontend unit tests cover the compose
view + entry-point routing.

**Organization**: By user story. The **create-on-send backend capability** is
foundational (both stories depend on it). US1 (open + leave = nothing) is the compose
view + entry routing; US2 (first send creates + delivers) wires the send. Both edit the
new compose component, so they are sequential.

**Path base**: `backend/` and `frontend/apps/web/src/app`.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: different files, no dependency. Stories: US1 / US2 (Setup/Foundational/Polish unlabeled).

---

## Phase 1: Setup

- [X] T001 Confirm baseline green on the branch: `dotnet build` + chat `dotnet test`, and `npx nx lint web` + `npx nx test web`, before changes.

---

## Phase 2: Foundational (Blocking Prerequisites — create-on-send capability)

**Purpose**: The atomic ensure-or-create + send path both stories rely on. Backend only.

**⚠️ CRITICAL**: The user-facing stories cannot work until this compiles.

- [X] T002 In `backend/Services/Chat/ChatConversationService.cs`, extract `EnsureDirectAsync(Guid callerId, Guid otherId, CancellationToken)` from `StartDirectAsync`: block check via `ChatGuard.IsBlockedBetweenAsync`, look up by `DirectPairKey`, insert if absent, and on `DbUpdateException` resolve to the winning row (the existing race-safe logic). Return the conversation id (or a blocked/invalid outcome).
- [X] T003 In `ChatConversationService.cs` + `backend/Services/Chat/IChatConversationService.cs`, add `SendFirstDirectAsync(Guid callerId, Guid targetUserId, string body, CancellationToken)`: validate the target is a real, non-banned user (as `StartAsync` does), call `EnsureDirectAsync`, then `_messages.SendAsync(callerId, conversationId, body)`; return `ChatResult<DirectMessageSentDto>` with the conversation id + created message.
- [X] T004 [P] Add `DirectMessageSentDto(Guid ConversationId, MessageDto Message)` to `backend/Dtos/Chat/ChatDtos.cs`.
- [X] T005 In `backend/Controllers/ChatConversationsController.cs`, add `POST chat/direct/{targetUserId:guid}/messages` (`[EnableRateLimiting(RateLimitPolicies.ChatStart)]`, authenticated) that binds `SendMessageRequest`, calls `_conversations.SendFirstDirectAsync`, and returns `201` with the `DirectMessageSentDto` (or the mapped failure).
- [X] T006 In `ChatConversationService.StartDirectAsync`, refactor to call the extracted `EnsureDirectAsync` (block check + create-or-get) then summarise — **behavior unchanged** (single-participant Start still create-or-gets; groups unchanged). (Revised from "narrow Start"; see research R4 — kept to avoid breaking the shared test helper for a non-normal-use vector.)
- [X] T007 Build the backend analyzer-clean (`dotnet build JuggerHub.Api.csproj`, warnings-as-errors).

**Checkpoint**: Backend compiles; DMs can be created only via first send; Start no longer creates a DM.

---

## Phase 3: User Story 1 - Opening a chat and leaving leaves nothing behind (Priority: P1) 🎯 MVP

**Goal**: Opening a chat with a not-yet-messaged player is a transient compose view that
persists nothing; leaving writes nothing.

**Independent Test**: Open a compose chat with a never-messaged player, leave without
sending; no conversation exists and neither inbox changed.

- [X] T008 [US1] Add route `chat/compose/:handle` to the `chat` children in `frontend/apps/web/src/app/app.routes.ts` (before the `:conversationId` route), lazy-loading `ChatComposeComponent`.
- [X] T009 [US1] Create `ChatComposeComponent` (`.ts`/`.html`/`.css`) under `frontend/apps/web/src/app/features/chat/chat-compose/`: read `:handle` (and optional router `state` `{ userId, displayName }`); render the recipient header + a calm empty-thread affordance + the composer (reuse the conversation composer styling); resolve `userId` via `chat.search(handle)` when not supplied; emit NO typing/read/inbox side effects and create nothing on load.
- [X] T010 [US1] Update `frontend/apps/web/src/app/features/profile/components/quick-actions/profile-quick-actions.component.ts` `message()`: keep the existing-conversation branch (navigate `/chat/:id`); replace the `chat.start(...)` new-DM branch with `router.navigate(['/chat/compose', handle], { state: { userId, displayName } })`.
- [X] T011 [US1] Update `frontend/apps/web/src/app/features/chat/chat-new/chat-new.component.ts`: a single picked person with `existingConversationId` opens `/chat/:id`; otherwise navigate to `/chat/compose/:handle` (with state); groups (2+) still call `chat.start` unchanged.
- [X] T012 [P] [US1] Frontend unit tests: `chat-compose.component.spec.ts` renders the recipient + composer and issues no start/create call on load; updated `profile-quick-actions.component.spec.ts` asserts new-DM → compose route and existing → `/chat/:id`; `chat-new` single-new → compose, group → start.
- [X] T013 [US1] (N/A — Start behavior is unchanged per revised R4, so there is no Start-narrowing to test. US1's "no create on open" is proven by the frontend tests in T012 — the compose entry issues no create call. Existing Start tests continue to cover its unchanged create-or-get.)

**Checkpoint**: Opening a new DM persists nothing; existing DMs still open; entry points route correctly.

---

## Phase 4: User Story 2 - Sending the first message creates and delivers (Priority: P1)

**Goal**: The first send materialises the DM and delivers it to both inboxes.

**Independent Test**: Compose to a never-messaged player, send → a real conversation with
the message exists in both inboxes; reopening returns it.

- [X] T014 [US2] Add `DirectMessageSent` (`{ conversationId, message }`) to `frontend/apps/web/src/app/core/models/chat.models.ts` and `sendDirect(targetUserId: string, body: string): Observable<DirectMessageSent>` to `frontend/apps/web/src/app/core/services/chat.service.ts` (POST `chat/direct/{targetUserId}/messages`), upserting the conversation locally on success.
- [X] T015 [US2] Wire `ChatComposeComponent` send: call `chat.sendDirect(userId, body)`, disable while in flight, and on success `router.navigate(['/chat', conversationId], { replaceUrl: true })`; surface a friendly message on block (403) / rate-limit (429) / failure without navigating.
- [X] T016 [P] [US2] Frontend unit test: compose send calls `sendDirect` and navigates (replaceUrl) to the new conversation; a refusal surfaces an error and does not navigate.
- [X] T017 [US2] Backend integration tests (chat): first send creates exactly one conversation containing the message and it appears for both participants; an existing DM is reused (no duplicate); a blocked pair's first send is refused and creates nothing; two concurrent first sends resolve to a single conversation (race).

**Checkpoint**: Lazy creation is correct end-to-end; no empty DM is ever produced.

---

## Phase 5: Polish & Cross-Cutting Concerns

- [X] T018 [P] Amend `specs/019-chat/` docs: note that direct conversations are created on first message send, not on open (spec note + data-model/contract note where the eager behavior is described).
- [X] T019 Instantiate `specs/022-lazy-dm-creation/checklists/ui-review.md` from the template and verify the compose view against DESIGN.md (Gate 7): recipient header, empty-thread affordance, one primary send control, send/error states.
- [X] T020 Run the [quickstart.md](quickstart.md) scenarios, then final verification: `dotnet build` + chat `dotnet test`, `npx nx lint web` + `npx nx test web` — all green.

---

## Dependencies & Execution Order

- **Setup (P1)** → **Foundational (P2, backend create-on-send + Start narrowing)** blocks both stories.
- **US1 (P3)** after Foundational; **US2 (P4)** after US1 (both edit `ChatComposeComponent` — US1 builds it, US2 adds send).
- **Polish (P5)** after US1 + US2.

### Within phases

- P2: T002 → T003 (uses EnsureDirect) → T005 (uses SendFirstDirect); T004 [P] (DTO, distinct file); T006 alongside T002 (same file, sequence with it); T007 build last.
- US1: T008 (route) + T009 (component) → T010/T011 (entry points) → T012 (tests) + T013 (backend test).
- US2: T014 (service/model) → T015 (compose send) → T016 (test); T017 backend tests independent.

### Parallel Opportunities

- T004 (DTO) parallel with other backend edits. T018 (019 docs) parallel with polish. Test tasks (T012/T016/T017) parallel with each other where files differ. The compose component `.ts`/`.html` are shared across US1/US2 → sequential.

---

## Implementation Strategy

### MVP scope

**Foundational + US1 + US2** together are the minimum coherent slice — lazy creation is
only correct with both the compose (no create on open) and the first-send (create on
send) halves. Ship them as one increment; US1 alone (compose that can't yet send) is not
independently shippable here.

### Order

1. Backend create-on-send + Start narrowing (P2) → build green.
2. Compose view + entry routing (US1).
3. Send wiring (US2) → both frontend + backend tests green.
4. Polish: 019 amendment, UI review, quickstart, full verification.

---

## Notes

- No database or schema change; reuses the `Conversation`/`DirectPairKey` uniqueness for
  create-on-send race safety (FR-006).
- Block is enforced server-side at ensure/send (FR-005); the first-message endpoint keeps
  the open-reach abuse rate limit (the creation surface).
- DMs only — group/team/party creation is untouched (FR-007).
- Amends feature 021 (the Message action now routes a new DM to compose) — why 022 is
  stacked on the 021 branch.
