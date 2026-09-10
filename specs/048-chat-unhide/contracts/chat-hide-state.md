# Contract: Conversation Hidden State

**Feature**: 048-chat-unhide | **Date**: 2026-09-09
**Amends**: [`specs/019-chat/contracts/chat-api.md`](../../019-chat/contracts/chat-api.md)

## The headline

**The HTTP contract does not change.** No endpoint is added, no route is altered, no request
or response field is added, removed or renamed. This document exists to record that the
*existing* contract already carries un-hide, and to pin the two behaviours that are new.

---

## 1. `PATCH /chat/conversations/{conversationId}/state` — unchanged, newly exercised

**Request** (unchanged):

```jsonc
{ "isMuted": true | false | null,     // omit or null = leave as-is
  "isHidden": true | false | null }   // omit or null = leave as-is
```

**Responses** (unchanged): `204 No Content` · `401` unauthenticated · `404` not a member of
that conversation (a non-member is not told the conversation exists).

**What is new**: the product now sends `{"isHidden": false}`. The server has always accepted
and applied it — see research R1. No server change is required on this path.

| Body | Effect |
|------|--------|
| `{"isHidden": true}` | Hide. Unchanged from today. |
| `{"isHidden": false}` | **Un-hide.** Newly reachable from the UI (FR-001). |
| `{"isMuted": …}` | Mute/unmute. Unchanged, and **never** affected by a hidden patch (FR-005). |
| `{}` | No-op, `204`. |

**Idempotent**: un-hiding a conversation that is not hidden is a `204` no-op, not an error
(spec Edge Cases). Same for hiding one already hidden.

**Not an access control**: a `404` here means *not a member*. Being hidden has never made a
conversation unreachable and still does not (FR-017) — `GET /chat/conversations/{id}` and its
messages endpoints continue to serve a hidden conversation to its members.

---

## 2. `POST /chat/conversations/{conversationId}/messages` — unchanged shape, new side effect

**Request and response shapes are unchanged.** The new behaviour is invisible in the payload:

> Sending a member message clears `IsHidden` for **every member of that conversation who had
> it set, including the sender**, before any unread total is recomputed.

Observable consequences, all through *existing* surfaces:

| Surface | Consequence |
|---------|-------------|
| `GET /chat/conversations` (inbox) | The conversation is now in the caller's list, in `LastMessageDate` order, with its correct `unreadCount` (FR-008). |
| `GET /chat/conversations/{id}` (detail) | `isHidden` now reads `false`. |
| Nav unread total | Includes the conversation from the moment it returns (FR-009) — the clear precedes the recompute (research R3). |
| SignalR `chatMessageCreated` | Unchanged event, unchanged payload. The client's existing "unknown conversation → re-seed inbox" branch does the rest (research R5). |

**Explicitly NOT triggering this side effect** (FR-011): system lines — a member joining or
leaving, a conversation becoming archived — written by `WriteSystemMessageAsync`. That method
already neither pushes realtime nor bumps `LastMessageDate`; it gains nothing here either.

---

## 3. No new realtime event

There is deliberately **no** `conversationUnhidden` hub event. The existing
`chatMessageCreated` push plus the client's inbox re-seed already delivers a returning
conversation complete and correctly ordered (research R5). Adding an event would be a second
mechanism for a case the first already covers.

The explicit toggle converges across a player's own sessions by the mechanism mute already
uses: `PatchStateAsync` pushes the caller's recomputed unread total
(`PushUnreadCountAsync`, L997-999). Unchanged.

---

## 4. Contract test obligations

| # | Assertion | Maps to |
|---|-----------|---------|
| C1 | `{"isHidden": false}` on a hidden conversation returns `204` and it reappears in `GET /chat/conversations` | FR-001 |
| C2 | `{"isHidden": false}` on a conversation that was never hidden returns `204` and changes nothing | Edge case |
| C3 | A patch of `isHidden` leaves `isMuted` unchanged, and vice versa, in all four combinations | FR-005, I2 |
| C4 | A member message returns the conversation to every hidden recipient's inbox **and** to the sender's | FR-007, FR-012 |
| C5 | The nav unread total includes the returned conversation in the same response cycle | FR-009 |
| C6 | A returned conversation that is **also muted** stays out of the unread total | FR-005, Edge case |
| C7 | A system line (join/leave) leaves a hidden conversation hidden | FR-011 |
| C8 | Hiding by one member does not alter any other member's `isHidden` | FR-006, I1 |
| C9 | A hidden conversation is still openable by id, before and after | FR-017 |
| C10 | A blocked-and-hidden DM does not return to the blocker's inbox | Edge case |
