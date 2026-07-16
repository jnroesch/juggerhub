# Tasks: Chat

**Feature**: 019-chat | **Branch**: `019-chat`
**Input**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/chat-api.md](./contracts/chat-api.md),
[quickstart.md](./quickstart.md)

Tests are included — the spec's Independent Tests and Success Criteria demand server-side
authorization coverage (SC-004, SC-005, SC-006), per-viewer unfurl scoping (SC-007), read-marker
correctness (SC-008) and bounded lists (SC-010). Every one of those is a security property, not a
nicety, so it gets a test.

`[P]` = parallelizable (different files, no incomplete-task dependency). Paths are repo-relative.
Mirrors the trainings (018), parties (016) and notifications (010) slices throughout.

---

## Phase 1: Setup

- [X] T001 Confirm branch `019-chat` is checked out, the stack is up (`docker compose up -d`) and existing migrations are applied (`dotnet ef database update` from `backend/`).
- [X] T002 [P] Confirm `specs/019-chat/checklists/ui-review.md` exists (instantiated during planning) and is the copy filled in during the frontend phases.

---

## Phase 2: Foundational (blocking prerequisites — MUST complete before any user story)

### Data layer

- [X] T003 [P] Add `backend/Entities/ChatEnums.cs` — `ConversationKind { Direct, Group, Team, Party }`, `ConversationState { Active, Archived }`, `ChatMessageKind { Member, System }`, `ChatLinkKind { None, Player, Team, Event, Training }`, `ChatSystemEvent { Joined, Left, Removed, GroupCreated }` (XML docs; serialized by name via the global `JsonStringEnumConverter`).
- [X] T004 [P] Add `backend/Entities/Conversation.cs` (`BaseEntity`: `Kind`, `Name?`, `TeamId?`, `PartyId?`, `State`, `LastMessageDate?`, `DirectPairKey?`; nav `Team?`, `Party?`, `ICollection<ConversationParticipant>`, `ICollection<ChatMessage>`) per data-model.md.
- [X] T005 [P] Add `backend/Entities/ConversationParticipant.cs` (`BaseEntity`: `ConversationId`, `UserId`, `LastReadMessageId?`, `IsMuted`, `IsHidden`, `JoinedDate`, `LeftDate?`; nav `Conversation`, `User`) per data-model.md.
- [X] T006 [P] Add `backend/Entities/ChatMessage.cs` (`BaseEntity`: `ConversationId`, `SenderId?`, `Kind`, `Body`, `IsDeleted`, `SystemEvent?`, `SystemSubjectUserId?`, `LinkKind`, `LinkTargetId?`; nav `Conversation`, `Sender?`) per data-model.md.
- [X] T007 [P] Add `backend/Entities/UserBlock.cs` (`BaseEntity`: `BlockerUserId`, `BlockedUserId`; nav `Blocker`, `Blocked`) per data-model.md.
- [X] T008 Edit `backend/Data/AppDbContext.cs` — add the four `DbSet`s; configure per data-model.md: `Conversation` unique filtered indexes on `TeamId` and `PartyId` (one chat per team/party), unique filtered index on `DirectPairKey` `WHERE "Kind" = 0`, `Name` max 120, FKs `Team`/`Party` restrict; `ConversationParticipant` **unique `(ConversationId, UserId)`** + index `(UserId)`, FK `Conversation` cascade / `User` restrict; `ChatMessage` composite index `(ConversationId, Id)` + index `(SenderId)`, `Body` max 2000, FK `Conversation` cascade / `Sender` restrict; `UserBlock` **unique `(BlockerUserId, BlockedUserId)`** + index `(BlockedUserId)`, both FKs restrict (no cascade — a deleted user must not silently drop blocks).
- [X] T009 Create the EF migration `AddChat` (`dotnet ef migrations add AddChat` from `backend/`); verify Up/Down match data-model.md (four tables, all unique indexes present). **No data backfill** — team/party chats are ensured on access (research §4).

### Backend shared building blocks

- [X] T010 [P] Add `backend/Services/Chat/ChatResults.cs` — `ChatOutcome { Ok, NotFound, Forbidden, Invalid, Conflict, RateLimited }` and `ChatResult`/`ChatResult<T>` (mirror `PartyResults.cs`/`TrainingResults.cs`).
- [X] T011 [P] Add `backend/Services/Chat/ChatConstants.cs` — `MaxMessageLength = 2000`, `MaxGroupMembers = 50`, `InboxPageSize = 20`, `MessagePageSize = 30`, `TypingExpirySeconds = 5` (research §8), each with a comment citing its reasoning.
- [X] T012 Add `backend/Services/Chat/ChatGuard.cs` — the single home of the membership predicate (data-model R5). Resolves `ChatAccess` (`ConversationId`, `Kind`, `State`, `IsMember`, `TeamId?`, `PartyId?`) in one query: `Direct`/`Group` ⇒ participant row exists with `LeftDate == null`; `Team` ⇒ `TeamMemberships.Any(...)`; `Party` ⇒ `PartyMembers.Any(Status == In)`. Non-member ⇒ null (caller returns **404**, never 403 — FR-048). Also exposes `ResolveParticipantUserIdsAsync(conversationId)` for realtime fan-out and `IsBlockedBetweenAsync(a, b)` (symmetric — data-model R16). Mirror `PartyGuard`/`TrainingGuard`.
- [X] T013 [P] Add `backend/Dtos/Chat/ChatDtos.cs` with every record from contracts/chat-api.md (`ConversationSummaryDto`, `ConversationDetailDto`, `ConversationAvatarDto`, `LastMessageDto`, `MessageDto`, `LinkCardDto`, `MemberDto`, `CreateConversationRequest`, `AddMembersRequest`, `SendMessageRequest`, `MarkReadRequest`, `PatchConversationStateRequest`, `ChatSearchResultDto`, `MessageSearchHitDto`, `PersonHitDto`, `BlockedUserDto`, `MessagePageDto`).
- [X] T014 [P] Add service interfaces in `backend/Services/Chat/`: `IChatConversationService`, `IChatMessageService`, `IChatSearchService`, `IChatBlockService` (returning `ChatResult`/`ChatResult<T>` and `PagedResult<T>` per contracts).
- [X] T015 Edit `backend/Program.cs` — register `ChatGuard` + the four chat services (scoped), alongside the trainings/parties registrations.
- [X] T016 Edit `backend/Program.cs` — add `builder.Services.AddRateLimiter(...)` with three named fixed-window policies partitioned on the **authenticated user id** (research §7): `chat-start` 10/min, `chat-send` 30/min, `chat-typing` 30/min; rejection status `429`. Add `app.UseRateLimiter()` in the pipeline **after** authentication (the partition key needs the identity). This is **new shared infrastructure** — no rate limiter exists today.

### Frontend foundation

- [ ] T017 [P] Add `frontend/apps/web/src/app/core/models/chat.models.ts` — TS interfaces/enums mirroring the DTOs (`ConversationKind`, `ConversationState`, `ChatMessageKind`, `ChatLinkKind`, `Conversation`, `ChatMessage`, `LinkCard`, `ChatMember`, `ChatSearchResult`).
- [X] T018 Edit `frontend/apps/web/src/app/layout/nav-model.ts` — add `'chat'` to `NavId`, append `{ id: 'chat', label: 'Chat', path: '/chat' }` to `NAV_DESTINATIONS`, and handle it in `isActiveDestination` (`path.startsWith('/chat')`). Reuse the existing `badgeText()` for the "9+" cap — do **not** add a second badge convention (FR-018).
- [X] T019 [P] Edit `frontend/apps/web/src/app/layout/nav-model.spec.ts` — cover the new destination and its active-matching.
- [ ] T020 Edit `frontend/apps/web/src/app/app.routes.ts` — add `/chat` (inbox) and `/chat/:conversationId` (open conversation, addressable per FR-046) under `authGuard`, lazy-loaded to the chat feature.

**Checkpoint**: schema + guard + DI + nav exist. No user-visible behaviour yet.

---

## Phase 3: User Story 1 — Two players hold a 1:1 conversation (P1) 🎯 MVP

**Goal**: start a DM, send, receive, unread badge, read on open, delete own message — all over REST.
**Independent test**: two users; A starts a DM with B and sends; B sees the badge, opens (badge clears), replies; A sees it. Works with a manual refresh — no realtime yet.

### Backend

- [X] T021 [US1] Add `backend/Services/Chat/ChatConversationService.cs` — `StartAsync(callerId, participantUserIds, name)`: 1 id ⇒ `Direct` (compute `DirectPairKey` from the **ordered** pair; catch the unique violation and **return the existing conversation with `Ok`** rather than a duplicate — FR-008); reject self-only (`Invalid`); reject when blocked either way (`Forbidden` — FR-031). Uses `DbSet.Add` for participants explicitly (the known client-GUID nav-insert gotcha).
- [X] T022 [US1] Extend `ChatConversationService` — `GetInboxAsync(callerId, pagination)`: projected + `AsNoTracking`, ordered `LastMessageDate DESC`, excluding `IsHidden` rows and blocked-counterpart `Direct` rows (data-model R19); derives the display name/avatar per kind; computes `unreadCount` per row via the R8 keyset predicate. **Paginated** (FR-006).
- [X] T023 [US1] Extend `ChatConversationService` — `GetUnreadTotalAsync(callerId)` per data-model R9 (sum over non-muted, non-hidden, member-of conversations).
- [X] T024 [US1] Extend `ChatConversationService` — `GetDetailAsync` + `GetMembersAsync` (paginated, caller marked `isYou`), 404 via `ChatGuard` when not a member.
- [X] T025 [US1] Extend `ChatConversationService` — `MarkReadAsync(callerId, conversationId, lastReadMessageId)`: sets the marker, **never moves it backwards** (a stale tab must not resurrect unread), idempotent; creates the lazy participant-state row if absent (data-model R7).
- [X] T026 [US1] Add `backend/Services/Chat/ChatMessageService.cs` — `SendAsync(callerId, conversationId, body)`: `ChatGuard` membership ⇒ 404; `Archived` ⇒ `Conflict` (FR-027); body trim/empty/2000-char validation ⇒ `Invalid` (FR-010); `Direct` + blocked ⇒ `Forbidden` (FR-031); persists and updates `Conversation.LastMessageDate`.
- [X] T027 [US1] Extend `ChatMessageService` — `GetPageAsync(callerId, conversationId, before?, take)`: **keyset** `WHERE ConversationId = x AND Id < before ORDER BY Id DESC LIMIT take`, `AsNoTracking` + projection, returns `nextBefore`; projects `readState` (Sent/Read) for the caller's own messages in a `Direct` conversation from the other participant's `LastReadMessageId` (data-model R8/§3); deleted messages project `isDeleted: true`, `body: ""`.
- [X] T028 [US1] Extend `ChatMessageService` — `DeleteAsync(callerId, messageId)`: sender-only ⇒ else `Forbidden` (FR-050a); system lines not deletable; sets `IsDeleted` and **clears `Body`/`LinkKind`/`LinkTargetId`** (data-model R12); refreshes `LastMessageDate`/preview so the inbox stops showing the deleted content (FR-050c).
- [X] T029 [US1] Add `backend/Controllers/ChatConversationsController.cs` — thin: `GET /api/v1/chat/conversations`, `GET …/unread-count`, `POST /conversations` (`[EnableRateLimiting("chat-start")]`), `GET /conversations/{id}`, `GET /conversations/{id}/members`, `POST /conversations/{id}/read`. Maps entities → DTOs with Mapster. Returns 404 (not 403) for non-membership.
- [X] T030 [US1] Add `backend/Controllers/ChatMessagesController.cs` — thin: `GET /conversations/{id}/messages`, `POST /conversations/{id}/messages` (`[EnableRateLimiting("chat-send")]`), `DELETE /messages/{id}`.

### Backend tests

- [X] T031 [P] [US1] Add `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatConversationTests.cs` — start-DM creates one conversation; **starting again returns the same one, not a duplicate** (FR-008); self-only rejected; inbox ordering + pagination bounded (SC-010); a non-member `GET /conversations/{id}` returns **404, not 403** (FR-048, SC-004).
- [X] T032 [P] [US1] Add `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatMessageTests.cs` — send/receive round-trip; empty + whitespace + >2000 rejected (FR-010); non-member send ⇒ 404; **ordering is by UUIDv7 `Id` and identical for both viewers**, including two sends in the same tick (FR-011); keyset paging returns every message exactly once with no gap or repeat.
- [X] T033 [P] [US1] Add `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatReadStateTests.cs` — unread rises on receive and clears on read; marking read **never moves the marker backwards**; unread never goes negative when a counted message is deleted; DM read receipt flips Sent→Read only when the *other* participant reads (FR-017, SC-008).
- [X] T034 [P] [US1] Add `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatDeleteTests.cs` — sender deletes ⇒ 204, body gone for **both** viewers, tombstone keeps its ordinal position; **non-sender member ⇒ 403** (FR-050a); non-member ⇒ 404; deleted message drops out of the inbox preview (FR-050c); **no PATCH route exists** (FR-050b).
- [X] T034a [US1] Handle deleted/banned senders (feature 013) in `ChatMessageService.GetPageAsync` + the inbox preview projection — a soft-deleted or banned user's past messages stay readable with a **neutral placeholder identity**, never null and never a crash; history is not rewritten (spec Edge Cases; data-model "Deleted / banned users"). Add coverage to `ChatDeleteTests.cs`: a message from a soft-deleted user still renders in a shared conversation with the placeholder, and a `POST /conversations` targeting them is refused.
- [X] T034b [P] [US1] Add `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatDoesNotTouchAlertsTests.cs` — **the negative requirement from the clarification session, which nothing else in this plan would catch** (FR-051, FR-051a, SC-014): receiving an unread chat message leaves the **Alerts** unread count (`GET /api/v1/notifications/unread-count`) **unchanged** and creates **no `Notification` row**; assert `NotificationType`/`NotificationCategory` gained no chat member. This is a regression guard against a future change quietly wiring chat into the Alerts spine.

### Frontend

- [ ] T035 [US1] Add `frontend/apps/web/src/app/core/services/chat.service.ts` — signal-based client mirroring `notification.service.ts`: `unreadCount`, `conversations`, `messages` signals; REST for inbox/unread/start/detail/members/messages/send/read/delete; `withCredentials`. **REST-only in this task** — the socket lands in US2.
- [ ] T036 [P] [US1] Add `frontend/apps/web/src/app/features/chat/chat-inbox/` (`.ts`/`.html`/`.css` separate — constitution VI) — rows with avatar, name, preview, mono time, unread badge; warm empty state per wireframe 9a ("No messages yet" + start action + the team-chat hint); `+` button.
- [ ] T037 [P] [US1] Add `frontend/apps/web/src/app/features/chat/chat-conversation/` — thread with **coral own-bubbles** / `surface-muted` others (DESIGN.md wins over the wireframe's blue — research §11), mono times, Sent/Read receipt **as text not bare color** (CHK037), composer with send, back-scroll paging, own-message delete control only.
- [ ] T038 [P] [US1] Add `frontend/apps/web/src/app/features/chat/chat-new/` — the new-chat sheet: pick one person ⇒ DM (no name field); teammates listed first, search reaches **any** player (FR-049).
- [ ] T039 [US1] Wire the Chat nav badge to `chatService.unreadCount` in `top-nav` + `bottom-nav`, reusing `badgeText()`.
- [ ] T040 [P] [US1] Add `frontend/apps/web/src/app/core/services/chat.service.spec.ts` — inbox load, unread signal, send, optimistic-vs-server reconciliation, delete. Angular 21 zoneless — **no `fakeAsync`** (per the 014 convention).

**Checkpoint**: the 1:1 loop works end-to-end on refresh. This is the MVP.

---

## Phase 4: User Story 2 — Messages arrive live (P1)

**Goal**: typing, live delivery, new-message divider + jump pill, live read receipts.
**Independent test**: two sessions, same conversation — typing shows and self-expires; a send lands with no refresh; scrolled-up delivery shows the divider + pill without moving scroll; Sent→Read flips live.

### Backend

- [ ] T041 [P] [US2] Add `backend/Services/Chat/Realtime/IChatRealtime.cs` — `PushMessageCreatedAsync`, `PushMessageDeletedAsync`, `PushUnreadCountAsync`, `PushConversationUpsertedAsync`, `PushTypingAsync`. Transport-agnostic seam (mirrors `INotificationRealtime`) so services stay testable without a socket **and a backplane can slot in later without touching producers** (research §10).
- [ ] T042 [US2] Add `backend/Services/Chat/Realtime/ChatHub.cs` — clone of `NotificationHub`: `[Authorize(JwtBearer)]`, joins **only** `user:{sub}` from the *validated* token, aborts on a missing subject, **no client-invokable server methods** (push-only — research §1).
- [ ] T043 [US2] Add `backend/Services/Chat/Realtime/SignalRChatRealtime.cs` — `IHubContext<ChatHub>`; fan-out resolves participants via `ChatGuard.ResolveParticipantUserIdsAsync` **server-side** and pushes to each `user:{id}` group; event names per contracts (`chatMessageCreated`, `chatMessageDeleted`, `chatUnreadCountChanged`, `chatConversationUpserted`, `chatTyping`).
- [ ] T044 [US2] Edit `backend/Program.cs` — register `IChatRealtime` → `SignalRChatRealtime` (singleton, as 010 does) and `app.MapHub<ChatHub>("/hubs/chat")`.
- [ ] T045 [US2] Wire pushes into `ChatMessageService.SendAsync`/`DeleteAsync` and `ChatConversationService.MarkReadAsync`/`StartAsync` — every push **best-effort after** the durable save, never instead of it (FR-023).
- [ ] T046 [US2] Add typing: `ChatConversationService.SignalTypingAsync(callerId, conversationId)` (membership check, `Archived` ⇒ `Conflict`, **persists nothing**) + `POST /conversations/{id}/typing` on `ChatConversationsController` with `[EnableRateLimiting("chat-typing")]`; pushes `chatTyping` to the **other** participants with a 5 s expiry (research §2).

### Backend tests

- [ ] T047 [P] [US2] Add `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatRealtimeTests.cs` — with a fake `IChatRealtime`: a send pushes to **every other participant and not the sender**; **a non-member is never pushed to** (FR-022); a delete pushes `chatMessageDeleted`; read pushes `chatUnreadCountChanged` to the reader's own group only; typing pushes to others, persists nothing, and 409s on an archived conversation.
- [ ] T048 [P] [US2] Extend `ChatRealtimeTests` — **the FR-023 guarantee**: with realtime stubbed to a no-op (simulating a dead socket), a plain REST load still returns the full history, correct unread and correct read state (SC-011). Live is an enhancement, never the source of truth.

### Frontend

- [ ] T049 [US2] Extend `chat.service.ts` — a `HubConnection` to `/hubs/chat` following `notification.service.ts` exactly: connect on auth, tear down on sign-out, **re-seed over REST on connect/reconnect** so a dropped socket self-heals; handle all five events into the signals; debounced typing POST (≤ 1 per 3 s) with a client-side expiry timer so a dead typist's indicator always clears.
- [ ] T050 [US2] Extend `chat-conversation` — live insert; **"new messages" divider + jump-to-latest pill with a count when scrolled away, without moving the scroll position** (FR-021); pin-to-bottom when already at the latest; tapping the pill jumps + marks read; typing bubble (`•••`).
- [ ] T051 [P] [US2] Extend `chat-inbox` — live row updates (preview/time/badge) and the per-row typing indicator ("Lena is typing…", named in group conversations).
- [ ] T052 [P] [US2] Extend `chat.service.spec.ts` — hub event handling, reconnect re-seed, typing debounce + expiry.

**Checkpoint**: chat is live. US1 + US2 = a genuinely usable chat.

---

## Phase 5: User Story 3 — Named groups (P2)

**Goal**: create a named group, sender labels, add people, leave, system lines.
**Independent test**: create "Weekend crew" with two others; all three see labelled messages; add a fourth (system line, appears in their inbox); a member leaves (system line, group survives).

- [ ] T053 [US3] Extend `ChatConversationService.StartAsync` — ≥2 ids ⇒ `Group`: **non-empty name required** (`Invalid` — FR-009); cap at `MaxGroupMembers` (`Invalid`); writes a `GroupCreated` system line.
- [ ] T054 [US3] Extend `ChatConversationService` — `AddMembersAsync`: `Group` only (`Invalid` for Direct/Team/Party — FR-026, US3 #9); any member may add (no admin role — spec Assumptions); **already-a-member is a no-op** (no duplicate row, no second system line — relies on the unique index); cap enforced; `Archived` ⇒ `Conflict`; writes a `Joined` system line per added user; pushes `chatConversationUpserted` to the newcomer.
- [ ] T055 [US3] Extend `ChatConversationService` — `LeaveAsync`: `Group` only (**`Team`/`Party` ⇒ `Invalid`** — FR-026); sets `LeftDate` rather than deleting the row (data-model R6); writes a `Left` system line; the group survives even when the leaver created it (US3 #7).
- [ ] T056 [US3] Add a `WriteSystemMessageAsync` helper on `ChatMessageService` — `Kind = System`, **`SenderId = null`** (data-model R13), `SystemEvent` + `SystemSubjectUserId`; the **client** renders the wording so it stays translatable and consistent.
- [ ] T057 [US3] Extend `ChatConversationsController` — `POST /conversations/{id}/members`, `DELETE /conversations/{id}/members/me`.
- [ ] T058 [P] [US3] Add `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatGroupTests.cs` — 1 id ⇒ Direct / ≥2 ⇒ Group; blank group name rejected; cap enforced at 51; **duplicate add is a no-op** (one member, one system line); leave keeps the group alive for the rest **including when the creator leaves**; a one-member group is still usable (US3 #8); add/leave rejected on Team/Party/Direct.
- [ ] T059 [P] [US3] Extend `chat-new` — Group/One-person segmented control, name field appearing at ≥2 selections, selected chips, teammates list (wireframe 9e).
- [ ] T060 [P] [US3] Add `frontend/apps/web/src/app/features/chat/chat-details/` — members with "you" marked, `+ Add`, Leave (groups only), shared items; **quiet system-line rendering** in the thread (muted, centered, no bubble — CHK038); sender labels on others' group messages.

**Checkpoint**: manual groups work.

---

## Phase 6: User Story 4 — Team & party chats appear by themselves (P2)

**Goal**: derived, roster-mirrored auto chats with TEAM/PARTY tags; archive on disband/delete.
**Independent test**: join a team ⇒ its chat is already in the inbox with the roster; leave ⇒ access refused (404); disband a party ⇒ chat readable, sends 409.

- [ ] T061 [US4] Extend `ChatConversationService` — `EnsureForTeamAsync(teamId)` / `EnsureForPartyAsync(partyId)`: idempotent create, relying on the **unique filtered index** to resolve a concurrent double-create (catch + re-read). This is what satisfies FR-024's backfill for pre-existing teams/parties **without a migration** (research §4).
- [ ] T062 [US4] Extend `GetInboxAsync` — union the caller's derived team/party conversations (via `TeamMemberships` / `PartyMembers` `Status == In`, which already includes `ViaMarket` guests — spec Assumptions), ensuring each on first sight; project the `TEAM`/`PARTY` tag and the derived name/avatar. Still **paginated** — auto chats must not bypass the inbox's bounds (edge case).
- [ ] T063 [US4] Extend `ChatGuard` — confirm the `Team`/`Party` branches of R5 read the roster live on **every** request, so removal revokes access with no sync step. Add `EnsureParticipantStateAsync` for the lazy mute/hide/read-marker row (data-model R7) — **state only, never authority**.
- [ ] T064 [US4] Hook system lines onto roster changes: team join/leave/remove in `backend/Services/Teams/` and party join/leave in `backend/Services/Parties/` write `Joined`/`Left`/`Removed` system lines to the corresponding conversation (best-effort — a failed system line must **never** fail the roster change itself).
- [ ] T065 [US4] Archive on lifecycle end — **a snapshot, not a flag** (data-model **R3a**; see the drift note there). Add `ChatConversationService.ArchiveForTeamAsync`/`ArchiveForPartyAsync` which, **before** the team/party row is hard-deleted: (1) materialise the derived roster into real `ConversationParticipant` rows, (2) freeze the display name into `Conversation.Name`, (3) null `TeamId`/`PartyId`, (4) set `State = Archived`. Call them from `PartyService.DisbandAsync` and `TeamService.DeleteAsync` **before** the delete. Without step 1 the archived chat is readable by **nobody** — the cascaded roster it derives membership from is gone — which would silently break FR-027. Leave `Kind` alone so the TEAM/PARTY tag survives. Verify `SendAsync`/`AddMembersAsync`/`SignalTypingAsync` reject `Archived` with `Conflict` and emit no realtime.
- [ ] T065a [US4] Add coverage to `ChatArchiveTests.cs` for R3a specifically: after a disband, a **former party member can still read the history** (the snapshot worked) and a **non-member still cannot** (404); the party row is genuinely gone (the `Restrict` FK did not block the disband); and a team delete behaves the same. This is the test that would have caught the flaw in the original flag-only design.
- [ ] T066 [P] [US4] Add `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatTeamPartyTests.cs` — **the headline security test**: joining a team grants chat access with the full roster; **leaving/removal revokes it — a direct `GET` returns 404** (FR-025, SC-004); rejoining restores access + history; a party chat includes a `ViaMarket` guest; **leave/add on a Team/Party chat ⇒ 400** (FR-026); ensure-on-access materialises the chat for a **pre-existing** team (FR-024); a one-member team chat is usable.
- [ ] T067 [P] [US4] Add `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatArchiveTests.cs` — disbanding a party archives its chat: reads succeed, `POST …/messages` ⇒ 409, typing ⇒ 409, no realtime emitted; archive is **one-way**.
- [ ] T068 [P] [US4] Extend `chat-inbox` — TEAM/PARTY eyebrow-styled pill tags (CHK032); 2×2 avatar cluster for groups, round for DMs; archived rows marked and their composer hidden in `chat-conversation`.
- [ ] T069 [P] [US4] Extend `chat-details` — **no Leave control** for Team/Party; mute + hide offered in its place (FR-026, US4 #4).

**Checkpoint**: all four conversation kinds work.

---

## Phase 7: User Story 5 — Mute, hide, block (P2)

**Goal**: the safety layer. Load-bearing, because DM reach is open (FR-049).
**Independent test**: mute ⇒ no badge but rows still update; hide ⇒ leaves the inbox; block ⇒ DM refused server-side even via the raw API, while a shared group keeps working.

- [ ] T070 [US5] Extend `ChatConversationService` — `PatchStateAsync(callerId, conversationId, isMuted?, isHidden?)` for **every** kind (this is what stands in for leave on Team/Party); creates the lazy state row; pushes `chatUnreadCountChanged` so the badge converges immediately.
- [ ] T071 [US5] Add `backend/Services/Chat/ChatBlockService.cs` — `ListAsync` (paginated), `BlockAsync` (idempotent via the unique index; **self-block ⇒ `Invalid`** — data-model R20), `UnblockAsync` (history untouched — FR-030).
- [ ] T072 [US5] Enforce blocks on all three paths (data-model R17): `StartAsync` (a fresh DM cannot walk around a block — FR-049b), `SendAsync`, and people search. **Scoped to `Kind == Direct` only** — a block must never touch a Group/Team/Party conversation (FR-032). Filter the blocked counterpart's DM out of the blocker's inbox (R19).
- [ ] T073 [US5] Add block/state endpoints to the controllers: `PATCH /conversations/{id}/state`, `GET /chat/blocks`, `POST /chat/blocks`, `DELETE /chat/blocks/{userId}`.
- [ ] T074 [P] [US5] Add `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatBlockTests.cs` — **SC-005, driving the API directly, not the UI**: a blocked sender's `POST …/messages` ⇒ 403 and nothing is delivered; a fresh `POST /conversations` to the blocker ⇒ 403 (FR-049b); **both users keep participating in a shared group normally** (FR-032); the blocker's inbox hides the DM; unblock restores messaging **with the prior history intact**; blocking is idempotent; self-block ⇒ 400; a blocked player is absent from people search (FR-033).
- [ ] T075 [P] [US5] Add `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatMuteHideTests.cs` — muted conversations are **excluded from the nav total but still update their row** (FR-028); hidden leave the inbox (FR-029); both work on a Team chat (the leave substitute).
- [ ] T076 [P] [US5] Add `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatRateLimitTests.cs` — exceeding `chat-start` (10/min) and `chat-send` (30/min) returns **429 when driving the API directly** (FR-049a, SC-012); limits partition per user, so one user's limit never affects another's.
- [ ] T077 [P] [US5] Extend `chat-details` — mute/hide toggles for every kind; Block/unblock on DMs styled `danger-fg` (not coral — CHK034), with a confirm step.

**Checkpoint**: the feature is safe to expose.

---

## Phase 8: User Story 6 — Search (P3)

**Goal**: messages from your own conversations + people to chat with.
**Independent test**: a term in your conversation is found; a term only in someone else's returns zero, verified by a direct API call.

- [ ] T078 [US6] Add `backend/Services/Chat/ChatSearchService.cs` — `SearchAsync(callerId, q, pagination)`: messages **scoped to the caller's conversations in the query itself** via the `ChatGuard` predicate (never post-filtered — FR-035), `ILike` + `Unaccent` per the existing convention (research §6), excluding deleted messages (FR-050c); people via the existing profile search shape, excluding blocked (FR-033) and the caller; both groups **paginated** (FR-006).
- [ ] T079 [US6] Add `GET /api/v1/chat/search` to `ChatConversationsController` (or a small `ChatSearchController`) — `q` min length 2.
- [ ] T080 [P] [US6] Add `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatSearchTests.cs` — **SC-006, the leak test**: a term present **only** in a conversation the caller is not in returns **zero results and no count** via a direct API call; own-conversation terms are found with a snippet; deleted messages never match; blocked people are absent; results are bounded/paginated; unaccented matching works.
- [ ] T081 [P] [US6] Extend `chat-inbox` — the search bar and the "IN YOUR MESSAGES" / "PEOPLE" grouped results per wireframe 9a; a message hit jumps to its conversation; a person hit opens/starts the DM; plain empty state on no match (not an error).

---

## Phase 9: User Story 7 — Links become cards (P3)

**Goal**: view-only cards for JuggerHub links, resolved per viewer.
**Independent test**: a training link renders a card with name+time; an external URL stays plain text; a viewer without permission sees only the link.

- [ ] T082 [P] [US7] Add `backend/Services/Chat/ChatLinkParser.cs` — pure: body → first `(ChatLinkKind, Guid|string)` by matching **our own route shapes only** — `/u/{handle}`, `/t/{slug}`, `/events/{id}`, `/trainings/sessions/{id}` (research §5; the **real** routes, not the wireframe's `/p/`, `/e/`). Accepts absolute URLs on our own host and relative paths; **makes no network call of any kind** (FR-042 — this is the SSRF prevention, and it is structural).
- [ ] T083 [US7] Add `backend/Services/Chat/ChatLinkResolver.cs` — `(kind, targetId, viewerId)` → `LinkCardDto?`, **re-running the viewer's own permission check** for each kind (training visibility via `TrainingGuard`, team/event/profile via their existing rules); returns **null** ⇒ plain link when the viewer may not see it (FR-040) or the target is gone (FR-041). **Never** stores or reuses a snapshot.
- [ ] T084 [US7] Wire the parser into `ChatMessageService.SendAsync` (store `LinkKind` + `LinkTargetId` only — never the target's fields) and the resolver into `GetPageAsync`/inbox projection, batched per page to avoid an N+1.
- [ ] T085 [P] [US7] Add `backend/tests/JuggerHub.Api.IntegrationTests/Chat/ChatUnfurlTests.cs` — **SC-007, the per-viewer test**: a team-only training link in a DM renders a **card for a member and a plain link for a non-member — same message, two viewers** (FR-040); an external URL ⇒ no card and **no outbound request** (FR-039/FR-042); a deleted target degrades to a plain link with no error (FR-041); a deleted message surrenders its card (FR-050c); all four kinds parse; `<script>` in a body round-trips as **literal text** (FR-014).
- [ ] T086 [P] [US7] Add link-card rendering to `chat-conversation` — card-spec styling, **no action buttons** (view-only — FR-038/CHK039), deep-link through to the item; bodies bound as **text, never HTML** (CHK040).

---

## Phase 10: User Story 8 — Desktop layout (P3)

**Goal**: rail + conversation + details side panel; identical mechanics.
**Independent test**: at desktop width all three panes coexist and the URL tracks the open conversation; at mobile width they are pushed screens; live behaviour is identical.

- [ ] T087 [US8] Add `frontend/apps/web/src/app/features/chat/chat-shell/` — responsive shell: ≥ `lg` ⇒ persistent inbox rail + conversation + optional details panel; below ⇒ separate pushed screens with back. **Layout only — no behavioural branch** (FR-045).
- [ ] T088 [US8] Ensure `/chat/:conversationId` drives selection in both layouts, so the open conversation is linkable and survives a reload (FR-046).
- [ ] T089 [P] [US8] Add a `chat-shell` spec asserting the breakpoint swap and that the same component instances back both layouts (so live behaviour cannot diverge — SC-009).

---

## Phase 11: Polish & cross-cutting

- [ ] T090 Run the **UI review checklist** `specs/019-chat/checklists/ui-review.md` against the full diff — every CHK001–CHK042, recording `file:line` for any failure. **DESIGN.md wins on any conflict**; confirm CHK030 (coral own-bubbles, not the wireframe's blue) and CHK034 (one coral CTA — send; block/leave in `danger-fg`).
- [ ] T091 [P] Accessibility pass — keyboard reach through inbox → thread → composer → details; visible coral focus rings; the typing indicator and unread badge announced to screen readers; receipts conveyed by **text, not color alone** (CHK026/CHK037).
- [ ] T092 [P] Add chat seed data to `backend/Data/DevDataSeeder.cs` — a DM, a named group, the Rheinfeuer team chat with history, and a party chat, matching the quickstart's prerequisites (Ada / Ben / **Zoe with no shared context**, for the open-reach and per-viewer-unfurl scenarios).
- [ ] T093 Walk **every** scenario in [quickstart.md](./quickstart.md) (A–I) against the running stack, including the direct-API security checks in E, F and I that deliberately bypass the UI.
- [ ] T094 Verification: `dotnet test` (backend), `npx nx test web`, `npx nx lint web`, `npx nx build web` — all green, no skips.
- [ ] T095 Confirm no unbounded list survives (SC-010): grep the chat services for any query returning a collection without `Take`/pagination — inbox, members, messages, search, blocks.
- [ ] T096 Re-read research §10 and confirm the **SignalR backplane risk** is surfaced in the PR description for the 015-hosting owner, not silently dropped: in-process SignalR is correct for main's single instance today, and chat breaks visibly (not quietly, like notifications) if AKS ever runs >1 replica without a backplane.

---

## Dependencies

```text
Phase 1 Setup
      ↓
Phase 2 Foundational  ← BLOCKS every user story
      ↓
Phase 3 US1 (P1) ── MVP ──┐
      ↓                   │
Phase 4 US2 (P1)          │  US2 needs US1's services to push from
      ↓                   │
Phase 5 US3 (P2)          │  groups extend US1's conversation service
      ↓                   │
Phase 6 US4 (P2)          │  team/party reuse US3's system lines
      ↓                   │
Phase 7 US5 (P2)          │  block/mute apply to everything above
      ↓                   │
Phase 8 US6 (P3) ─────────┤  search needs conversations to search
Phase 9 US7 (P3) ─────────┤  unfurl needs messages to render
Phase 10 US8 (P3) ────────┘  desktop needs the screens to lay out
      ↓
Phase 11 Polish
```

**Genuinely independent once Phase 3 lands**: US6 (search), US7 (unfurl) and US8 (desktop) touch
disjoint files and can proceed in parallel with each other. US4 depends on US3 only for the system-line
helper (T056). US5 must precede any public exposure of the feature — open DM reach (FR-049) without
block is not shippable.

## Parallel execution examples

- **Phase 2 entities**: T003–T007 are five separate files → run together; T008 (DbContext) joins them, then T009 (migration).
- **Phase 3 tests**: T031–T034 are four separate test files → run together once T021–T030 land.
- **Phase 3 frontend**: T036, T037, T038 are separate component folders → run together after T035 (the service).
- **Phases 8–10**: T078–T081 (search), T082–T086 (unfurl), T087–T089 (desktop) are three disjoint tracks → run together.

## Implementation strategy

**MVP = Phase 1 + Phase 2 + Phase 3 (US1).** That is a working 1:1 chat that requires a refresh — the
whole loop, demonstrable, with server-side authorization already correct.

**First genuinely shippable = + Phase 4 (US2).** Chat that doesn't arrive live isn't chat.

**Minimum safe to expose to users = + Phase 7 (US5).** Because reach is open by decision (FR-049), the
feature must not reach production without block and rate limiting. Do not ship Phases 3–6 publicly
without Phase 7.

**Then** P3s (US6 search, US7 unfurl, US8 desktop) in any order, and Polish.
