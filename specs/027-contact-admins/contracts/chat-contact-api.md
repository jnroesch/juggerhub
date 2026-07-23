# API Contract: Contact the Admins (027)

New endpoints on the existing chat controller
(`ChatConversationsController`, base `api/v{version}/chat`, `[Authorize]` JWT). Thin controllers →
`IChatConversationService`; entities → DTO via Mapster. Non-member/absent target ⇒ **404** (never 403).
All shapes reuse feature-019 chat DTOs where possible.

## New / reused DTOs

```csharp
// Reuses the 019/022 pattern. Either reuse DirectMessageSentDto, or add a parallel:
public sealed record InquiryMessageSentDto(ConversationSummaryDto Conversation, MessageDto Message);

// Request body for a first inquiry message (mirrors SendMessageRequest).
public sealed record SendMessageRequest(string Body);   // existing

// Optional resolve response (see GET below).
public sealed record InquiryThreadRefDto(Guid? ConversationId);
```

`ConversationSummaryDto` / `MessageDto` are unchanged; `ConversationKind` in them now includes
`TeamInquiry` / `EventInquiry`, and `ConversationSummaryDto.Name` is already viewer-appropriate
(team/event name for the requester, requester name for an admin) via the service projection.

---

## POST `/chat/contact/team/{teamId:guid}/messages`

Send the first (or a first-of-thread) message to a team's admins, creating the `TeamInquiry` thread if
none exists for (caller, team). **Idempotent on the thread**: a second call reuses the existing thread.

- **Auth**: required. **Rate limit**: `RateLimitPolicies.ChatStart` (same as `Start`/`SendDirect`).
- **Path**: `teamId` — the target team.
- **Body**: `SendMessageRequest { body }`.
- **Server checks** (all server-side):
  - team exists and is visible to the caller → else **404**;
  - caller is **not** a team admin → else **409/400** `"You're already an admin of this team."`
    (FR-002);
  - body non-empty/within length (delegated to `SendAsync`).
- **201 Created** → `InquiryMessageSentDto` (thread summary + the sent message);
  `Location: /api/v1/chat/conversations/{conversationId}`.
- **404** target missing/not visible. **409/400** caller is an admin, or team deleted. **429** rate
  limited. **401** unauthenticated.

## POST `/chat/contact/event/{eventId:guid}/messages`

As above for an event → `EventInquiry`. Additional check: the event is **not cancelled** (a cancelled
event's inquiry threads are archived/closed) → else **409** `"This event is closed."`.

## GET `/chat/contact/team/{teamId:guid}` and `/chat/contact/event/{eventId:guid}` (optional, recommended)

Resolve whether the caller already has an inquiry thread for this target, so the entry-point button can
deep-link into the existing conversation instead of opening a fresh compose (FR-004 UX).

- **200** → `InquiryThreadRefDto { conversationId }` (`null` when none exists yet).
- Never creates anything. Returns `null` rather than 404 so the button can branch without error noise.
- Does **not** reveal admin identities or messages — only whether *the caller's own* thread exists.

---

## Behaviour notes (contract-level)

- **Naming in responses**: the service sets `ConversationSummaryDto.Name` per viewer — team name /
  event title for the requester (FR-009), requester display name for an admin (FR-010). Avatar
  likewise (team crest / event icon vs. requester avatar). Tag is derived client-side from `Kind`.
- **Membership & fan-out**: admins are resolved from the live roster; a newly-granted admin's first
  inbox/GET call surfaces the thread with history from their grant (FR-019). No participant row is
  created for admins on send.
- **Read/typing/mute/hide/members**: the existing generic endpoints
  (`/conversations/{id}/read|typing|state|members`, `GET /conversations/{id}`) work unchanged for
  inquiry threads. `GetDetailAsync` reports `CanLeave = false`, `CanAddMembers = false` (mirrored
  kind), mute/hide available (FR-017).
- **Blocking**: not applicable; the block endpoints are untouched and inquiry kinds are excluded from
  the DM-only block path (FR-016).
- **Archived thread**: reads succeed; `read`/`typing`/send return the existing
  `"This chat is closed."` conflict.

## OpenAPI / allowlist

These are authenticated endpoints under the already-`[Authorize]` chat controller — no
`AllowAnonymous`, no additions to any anonymous allowlist (cf. feature 026's FallbackPolicy note).
