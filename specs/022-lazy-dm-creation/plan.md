# Implementation Plan: Lazy Direct-Message Creation

**Branch**: `022-lazy-dm-creation` | **Date**: 2026-07-21 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/022-lazy-dm-creation/spec.md`

## Summary

Make direct-message conversations **lazy**: a DM is created only when the first
message is sent, not when the chat is opened. Opening a chat with a not-yet-messaged
player becomes a transient client **compose** view that persists nothing; the first
send atomically ensures-or-creates the one-per-pair conversation and posts the message.
Existing DMs still open directly. Groups and team/party chats are untouched.

Core mechanism: a new **first-message endpoint** creates-and-sends in one server
operation, reusing the existing race-safe create-or-get on the unique `DirectPairKey`
(so concurrent first sends still resolve to one conversation — FR-006). The chat
"new message" flow and the feature-021 profile **Message** action route a *new* DM to
the compose view (they already know `existingConversationId` from search, so they open
the existing thread directly when one exists). No database or schema change.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript / Angular (Nx), zoneless (frontend)

**Primary Dependencies**: EF Core (existing `Conversation`/`DirectPairKey` + unique index),
SignalR (existing realtime), Angular signals/Router; existing `ChatService`

**Storage**: PostgreSQL — **no migration**. Reuses the `Conversations` table, its unique
`DirectPairKey` filtered index, and `ConversationParticipants`.

**Testing**: xUnit integration tests (backend chat suites); Jest (frontend, zoneless)

**Target Platform**: Web (mobile + desktop chat surface)

**Project Type**: Web application (backend + frontend)

**Performance Goals**: Sending a first message is one round trip (ensure+send atomic).
Opening a new-DM compose makes no write and no conversation row.

**Constraints**: One-DM-per-pair uniqueness must hold on the create-on-send path under
concurrency (FR-006); block enforced at send time (FR-005); groups/team/party unchanged
(FR-007); the creation surface keeps the open-reach abuse rate limit.

**Scale/Scope**: Community-scale chat; one new endpoint + one new compose view + edits
to two existing entry points (new-message flow, 021 Message action).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Security-First / Never Trust the Client** — PASS. Block is enforced server-side
  at ensure/send (a blocked pair cannot create a DM by sending, FR-005); target user
  ids are validated (real, non-banned) as `StartAsync` does today; the first-message
  endpoint carries the **open-reach abuse rate limit** that `POST /conversations`
  (Start) has, since it becomes the conversation-creation surface (spec FR-049a).
- **II. Thin Controllers, Service-Centric** — PASS. The new endpoint is thin; the
  ensure-then-send orchestration lives in the chat service layer behind its interface.
- **III. Disciplined Data Access** — PASS. No new entity/migration. Reuses the race-safe
  create-or-get on the unique `DirectPairKey` (insert + `DbUpdateException` → resolve to
  the winner), the same guarantee the eager path relies on today.
- **IV. Auth & Session** — PASS. Endpoints stay `[Authorize]`; no auth change.
- **V. Environment Parity** — PASS. Pure code change, identical across environments.
- **VI. Conventions & Tooling** — PASS. New compose component keeps separate
  `.html`/`.css`/`.ts`; no scripts added.
- **Quality Gate 7 (UI/Design compliance)** — APPLIES. New compose view UI →
  instantiate `specs/022-lazy-dm-creation/checklists/ui-review.md` and verify against
  DESIGN.md (it reuses the existing conversation composer styling).

**Result**: No violations. Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/022-lazy-dm-creation/
├── plan.md · research.md · data-model.md · quickstart.md
├── contracts/            # the new first-message endpoint + the amended Start behavior
├── checklists/requirements.md (done) · ui-review.md (impl)
└── tasks.md              # /speckit-tasks
```

### Source Code (repository root)

```text
backend/
├── Services/Chat/ChatConversationService.cs   # EXTRACT EnsureDirectAsync (race-safe create-or-get);
│                                               # ADD SendFirstDirectAsync (ensure + _messages.Send);
│                                               # STOP single-participant Start from creating (existing-only)
├── Services/Chat/IChatConversationService.cs   # add SendFirstDirectAsync
├── Controllers/ChatConversationsController.cs  # ADD POST chat/direct/{targetUserId}/messages (ChatStart rate limit)
├── Dtos/Chat/ChatDtos.cs                        # ADD DirectMessageSentDto { conversationId, message }
└── tests/.../Chat/*                             # lazy behavior: no-create-on-open, create-on-send, race, block-at-send, existing-opens

frontend/apps/web/src/app/
├── app.routes.ts                                # ADD chat/compose/:handle
├── features/chat/chat-compose/                  # NEW compose view (target header + empty thread + composer)
│   └── chat-compose.component.{ts,html,css,spec.ts}
├── core/services/chat.service.ts                # ADD sendDirect(targetUserId, body); DMs no longer use start()
├── core/models/chat.models.ts                   # ADD DirectMessageSent
├── features/chat/chat-new/chat-new.component.ts # single-person new DM → compose route; existing → open; groups → start (unchanged)
└── features/profile/components/quick-actions/profile-quick-actions.component.ts  # 021 Message: new-DM branch → compose route (was chat.start)

specs/019-chat/  (amendment note: DMs are created on first send, not on open)
```

**Structure Decision**: Existing web-app layout. One new backend endpoint + service
method, one new frontend compose component + route, and small edits to the two DM entry
points. A dedicated compose component (rather than a draft mode bolted onto the complex
`ChatConversationComponent`) keeps the blast radius small.

## Complexity Tracking

No constitution violations; section intentionally empty.
