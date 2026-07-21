# Phase 0 Research: Lazy Direct-Message Creation

All questions resolved from the codebase; no open NEEDS CLARIFICATION.

## R1. Where creation happens today, and how to make it lazy

**Decision**: Move DM creation out of "start/open" and into a new **create-and-send**
operation invoked by the first message.

**Rationale**: Today `ChatConversationService.StartDirectAsync`
([ChatConversationService.cs:67](../../backend/Services/Chat/ChatConversationService.cs))
inserts the `Conversation` row on open and returns it, so the empty thread shows in the
inbox immediately. `SendAsync` ([ChatMessageService.cs:51](../../backend/Services/Chat/ChatMessageService.cs))
requires an existing `conversationId`. Introducing a path that ensures-or-creates the
DM *and* writes the first message together means a DM only ever exists once it has a
message — no empty rows, nothing to clean up, and the inbox needs no "hide empties"
filter.

**Alternatives considered**: (a) Hide empty DMs in the inbox query — leaves orphan rows
and needs `SummariseAsync` decoupling; rejected in favor of the owner-chosen lazy model.
(b) Delete-on-leave — fragile and risks the archive/hard-delete gotchas; rejected.

## R2. Race-safe create-on-send (FR-006)

**Decision**: Extract the existing race-safe create-or-get into `EnsureDirectAsync`
(block check → look up by `DirectPairKey` → insert → on `DbUpdateException` resolve to
the winner) and call it from the first-message path.

**Rationale**: `StartDirectAsync` already solves the concurrent-create race with the
unique filtered index on `DirectPairKey` plus a `DbUpdateException` catch that resolves
to the winning row ([:100-121](../../backend/Services/Chat/ChatConversationService.cs#L100)).
Reusing that exact logic keeps the one-DM-per-pair guarantee on the new path — two
first sends (two devices, or both participants at once) converge on one conversation,
then both messages land in it.

**Alternatives considered**: A DB upsert/`ON CONFLICT` — the EF catch-and-resolve
pattern is already established here and battle-tested; no reason to diverge.

## R3. New endpoint shape

**Decision**: `POST /api/v1/chat/direct/{targetUserId}/messages` with body `{ body }`,
returning `201 { conversationId, message }`. Rate-limited with the **ChatStart** policy.

**Rationale**: The target is addressed by user id (the chat domain works in user ids).
Returning both the new `conversationId` and the created `MessageDto` lets the client
navigate to the real conversation after the first send. It must carry the ChatStart
rate limit because it is now the conversation-creation surface that open reach would
otherwise let one account fan out across the community (the guard that
`POST /conversations` carries today, [ChatConversationsController.cs:69](../../backend/Controllers/ChatConversationsController.cs#L69)).
Orchestration lives on `ChatConversationService` (which already depends on
`IChatMessageService`, so ensure-then-send introduces no new dependency cycle).

**Alternatives considered**: Extending `POST /conversations` to accept an initial body —
overloads a well-named endpoint and muddies group vs DM; a dedicated route is clearer.

## R4. What happens to `POST /conversations` (Start) — REVISED during implementation

**Decision**: **Leave Start unchanged** (single participant still create-or-gets a DM;
groups unchanged). Refactor it only to share the extracted `EnsureDirectAsync` (no
behavior change). The client simply stops calling Start for a *new* DM — it routes to
compose and creates via the first-message endpoint.

**Rationale (revised)**: FR-009 is scoped to *normal use* (the client UI), which is fully
satisfied by the client never creating a DM on open. Neutering the single-participant
Start path would break the shared test helper `ChatTestSupport.StartDirectAsync` and the
~10 suites that use it to set up a DM — large churn for marginal benefit. The residual is
that an authenticated caller could still create an empty DM by calling the API directly;
this is rate-limited (`ChatStart`, 10/min), not reachable through the UI, and low-harm
(no message content). Accepted as an out-of-normal-use edge; a future hardening could
neuter it if desired.

**Alternatives considered**: Neutering single-participant Start — spec-faithful to the
letter but high test churn and risk for a non-normal-use vector; deferred.

## R5. Client compose flow + entry points

**Decision**: Add a route `chat/compose/:handle` rendering a new `ChatComposeComponent`
(target header + empty thread + composer). The **021 Message action** and the **chat
new-message picker** route a *new* DM there; an *existing* DM opens `/chat/:id` directly.
On first send, `ChatService.sendDirect(targetUserId, body)` calls the new endpoint and
the component navigates (`replaceUrl: true`) to `/chat/:conversationId`.

**Rationale**: Both entry points already resolve the person via search and hold
`existingConversationId` (021) / the picked `PersonHit` (new-message), so the
open-vs-compose decision is free. A dedicated compose component avoids threading a
"draft with no id" mode through the complex `ChatConversationComponent`
([chat-conversation.component.ts](../../frontend/apps/web/src/app/features/chat/chat-conversation/chat-conversation.component.ts),
which takes a required `conversationId` input). `:handle` in the URL makes the compose
view reload-safe (it re-resolves the target via chat search if router state is absent);
`replaceUrl` means the browser Back button skips the spent compose URL. Groups (2+ in
the picker) keep using `start()` unchanged.

**Alternatives considered**: A draft mode inside `ChatConversationComponent` — more
conditionals in the busiest component; rejected for blast radius.

## R6. No side effects in draft (FR-004 / edge cases)

**Decision**: The compose view emits nothing that presumes a conversation — no typing
signal (there is no conversation id to signal against), no read receipts, no inbox
entry. Leaving simply navigates away; nothing was written.

**Rationale**: Typing/read/unread are all keyed on a conversation id, which does not
exist until first send. This falls out of the compose component not owning a conversation.

## R7. Block enforcement at send (FR-005)

**Decision**: Block is checked inside `EnsureDirectAsync` (before create) — the same
`ChatGuard.IsBlockedBetweenAsync` check `StartDirectAsync` uses today — and `SendAsync`
independently re-checks block for DMs. A blocked pair therefore cannot bring a DM into
existence.

**Rationale**: Reuses existing, tested block logic at exactly the moment creation would
occur; no new security surface.

## R8. Inbox "hide empties" filter — not needed

**Decision**: Do **not** add an inbox filter for empty DMs.

**Rationale**: With lazy creation, no empty DM is ever created through the client, so
FR-009 holds without a filter. Pre-existing empty DM rows (created before this change)
are explicitly out of scope (spec Assumptions); a one-time cleanup is a possible
follow-up, not part of this feature.
